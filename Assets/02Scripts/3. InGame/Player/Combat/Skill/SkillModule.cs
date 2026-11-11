using System;
using System.Collections.Generic;
using alpha.ingame.item;
using alpha.ingame.skill;
using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>1~4번 슬롯의 버프와 설치형 스킬 실행을 연결한다.</summary>
    [RequireComponent(typeof(SkillStateFlow))]
    [DefaultExecutionOrder(100)]
    public sealed class SkillModule : MonoBehaviour
    {
        private sealed class ScheduledAudio
        {
            public readonly SkillData Skill;
            public readonly float StartedAt;
            public readonly SkillAudioCue[] Cues;
            public readonly bool[] Played;

            public ScheduledAudio(SkillData skill)
            {
                Skill = skill;
                StartedAt = Time.time;
                IReadOnlyList<SkillAudioCue> configured = skill.ActivationAudioCues;
                Cues = new SkillAudioCue[configured.Count];
                for (int index = 0; index < configured.Count; index++) Cues[index] = configured[index];
                Played = new bool[Cues.Length];
            }
        }

        private sealed class ActiveDeployable
        {
            public readonly SkillData Skill;
            public readonly GameObject Instance;
            private readonly SummonLifetime _summon;
            private readonly ShieldInstance _shield;

            public ActiveDeployable(SkillData skill, GameObject instance,
                SummonLifetime summon, ShieldInstance shield)
            {
                Skill = skill;
                Instance = instance;
                _summon = summon;
                _shield = shield;
            }

            public float Progress => Instance == null ? 0f :
                _summon != null ? _summon.ActiveProgress :
                _shield != null ? _shield.ActiveProgress : 0f;
        }

        [SerializeField] private SkillData[] _slots = new SkillData[4];

        private readonly Dictionary<SkillData, float> _readyAt = new();
        private readonly List<ScheduledAudio> _scheduledAudio = new();
        private readonly List<ActiveDeployable> _activeDeployables = new();
        private SkillStateFlow _flow;
        private PlayerRoot _playerCore;
        private DeployableSkillData _activeSkill;
        private SkillPlacementPreview _preview;
        private Pose _castPose;
        private int _castingSlot;
        private bool _castSpawned;
        private float _castTimeoutAt;
        private WeaponBuffSkillData _weaponBuff;
        private float _weaponBuffEndsAt;
        private GunWeapon _buffEffectGun;
        private BeamPodSummon _beamPodGroup;
        private int _beamPodSlot;

        public float FireRateMultiplier => IsWeaponBuffActive ? _weaponBuff.FireRateMultiplier : 1f;
        public float DamageMultiplier => IsWeaponBuffActive ? _weaponBuff.DamageMultiplier : 1f;
        // 포드가 남아 있는 동안에만 해당 슬롯의 상태색을 표시한다.
        public int ActiveBeamPodSlot => _beamPodGroup != null && _beamPodGroup.HasActivePods
            ? _beamPodSlot : 0;
        public bool IsBeamPodAttacking => _beamPodGroup != null && _beamPodGroup.IsAttacking;
        private bool IsWeaponBuffActive => _weaponBuff != null && Time.time < _weaponBuffEndsAt;

        // 쿨타임과 별도로 실제로 살아 있는 스킬 효과의 남은 비율을 돌려준다.
        public float GetActiveProgress(int slot)
        {
            SkillData skill = GetSkill(slot);
            if (skill == null) return 0f;
            if (skill is WeaponBuffSkillData buff)
                return _weaponBuff == buff && IsWeaponBuffActive
                    ? Mathf.Clamp01((_weaponBuffEndsAt - Time.time) / buff.Duration) : 0f;
            if (skill is BeamPodSkillData)
                return _beamPodGroup != null && _beamPodSlot == slot
                    ? _beamPodGroup.ActiveProgress : 0f;

            float progress = 0f;
            for (int i = _activeDeployables.Count - 1; i >= 0; i--)
            {
                ActiveDeployable active = _activeDeployables[i];
                float remaining = active.Progress;
                if (remaining <= 0f)
                {
                    _activeDeployables.RemoveAt(i);
                    continue;
                }
                // 같은 슬롯의 설치물이 겹치면 남은 비율이 가장 큰 것을 표시한다.
                if (active.Skill == skill) progress = Mathf.Max(progress, remaining);
            }
            return progress;
        }

        // 실제로 받아들인 스킬 선택을 UI에 알린다.
        public event Action<int> SkillSelected;
        // 위치 선택과 포드 공격 명령에만 버튼 선택음을 재생한다.
        public event Action<int> SkillSelectionSoundRequested;
        public event Action<int> SkillCanceled;
        // 실제 스킬 실행이 성공하고 쿨다운이 시작된 뒤 알린다.
        public event Action<int> SkillUsed;

        private void Awake()
        {
            _flow = GetComponent<SkillStateFlow>();
            _playerCore = GetComponentInParent<PlayerRoot>(true);
        }

        private void OnEnable()
        {
            _flow.SkillRequested += OnSkillRequested;
            _flow.ExecuteConfirmRequested += OnExecuteConfirmRequested;
            _flow.ExecuteCanceled += OnExecuteCanceled;
            _flow.CancelRequested += OnCancelRequested;
            for (int slot = 1; slot <= 4; slot++) PublishSlot(slot);
        }

        private void OnDisable()
        {
            _flow.SkillRequested -= OnSkillRequested;
            _flow.ExecuteConfirmRequested -= OnExecuteConfirmRequested;
            _flow.ExecuteCanceled -= OnExecuteCanceled;
            _flow.CancelRequested -= OnCancelRequested;
            _flow.CancelExecute();
            _playerCore?.CombatActionFlow?.EndSkill();
            ClearActiveSkill();
            StopWeaponBuff();
            if (_beamPodGroup != null) Destroy(_beamPodGroup.gameObject);
            _beamPodGroup = null;
            _beamPodSlot = 0;
            _scheduledAudio.Clear();
            _activeDeployables.Clear();
            _playerCore?.SkillDisplayChannel.Clear();
        }

        private void LateUpdate()
        {
            UpdateWeaponBuff();
            UpdateScheduledAudio();
            if (_flow.IsCasting)
            {
                UpdateCast();
                return;
            }

            if (_activeSkill == null || _preview == null ||
                _flow.CurrentState != ESkillState.Execute) return;

            bool canPlace = SkillPlacementValidator.TryGetPlacement(_activeSkill,
                _playerCore.transform, Camera.main, out Pose pose, out bool hasTarget);
            _preview.Show(pose, hasTarget, canPlace);
        }

        private SkillData GetSkill(int slot) =>
            slot >= 1 && slot <= 4 && _slots != null && slot <= _slots.Length
                ? _slots[slot - 1] : null;

        private float GetRemainingCooldown(int slot)
        {
            SkillData skill = GetSkill(slot);
            return skill != null && _readyAt.TryGetValue(skill, out float readyAt)
                ? Mathf.Max(0f, readyAt - Time.time) : 0f;
        }

        private void OnSkillRequested(int slot)
        {
            SkillData requested = GetSkill(slot);
            if (requested is BeamPodSkillData && _beamPodGroup != null &&
                _beamPodGroup.HasActivePods)
            {
                // 소환 후의 3번 키는 쿨다운과 관계없이 포드 명령으로 전달한다.
                EBeamPodCommandResult result = _beamPodGroup.AdvanceCommand();
                if (result == EBeamPodCommandResult.Cancel) SkillCanceled?.Invoke(slot);
                else if (result == EBeamPodCommandResult.Attack)
                {
                    SkillSelected?.Invoke(slot);
                    SkillSelectionSoundRequested?.Invoke(slot);
                }
                return;
            }
            if (requested == null || GetRemainingCooldown(slot) > 0f ||
                _playerCore == null || !CanCastOnGround()) return;

            if (requested is WeaponBuffSkillData buff)
            {
                ActivateWeaponBuff(slot, buff);
                return;
            }

            if (requested is BeamPodSkillData beamPod)
            {
                ActivateBeamPods(slot, beamPod);
                return;
            }

            if (requested is not DeployableSkillData skill || skill.Prefab == null) return;
            if (_playerCore.CombatActionFlow?.TryBeginSkill() != true) return;

            if (skill.ActivationMode == ESkillActivationMode.Self)
            {
                // 위치 선택 없이 시전을 시작하고 입력 시점의 플레이어 위치를 보관한다.
                Transform owner = _playerCore.transform;
                Quaternion rotation = Quaternion.Euler(0f, owner.eulerAngles.y, 0f);
                Pose pose = new Pose(owner.position + rotation * skill.PlacementOffset, rotation);
                _activeSkill = skill;
                _playerCore.CombatModule?.CancelSecondary();
                if (!TryBeginCast(slot, pose, true))
                {
                    ClearActiveSkill();
                    _playerCore.CombatActionFlow.EndSkill();
                }
                else SkillSelected?.Invoke(slot);
                return;
            }

            if (!_flow.BeginExecute(slot))
            {
                _playerCore.CombatActionFlow.EndSkill();
                return;
            }
            _activeSkill = skill;
            CanonTargeting turret = skill is TurretSkillData
                ? skill.Prefab.GetComponentInChildren<CanonTargeting>(true) : null;
            _preview = new SkillPlacementPreview(skill.Prefab, skill.ClearanceRadius,
                turret != null ? turret.Range : 0f);
            _playerCore.CombatModule?.CancelSecondary();
            SkillSelected?.Invoke(slot);
            SkillSelectionSoundRequested?.Invoke(slot);
        }

        private void ActivateWeaponBuff(int slot, WeaponBuffSkillData buff)
        {
            GunWeapon gun = _playerCore.EquipmentModule?.CurrentWeapon?.GetComponent<GunWeapon>();
            if (gun == null || _playerCore.CombatActionFlow?.TryBeginSkill() != true) return;

            // 강화는 즉시 발동하고 공격 행동 잠금은 바로 해제한다.
            if (_weaponBuff != null) StopWeaponBuff();
            _weaponBuff = buff;
            _weaponBuffEndsAt = Time.time + buff.Duration;
            ApplyMagazineBuff(buff.MagazineCapacityMultiplier);
            UpdateWeaponBuff();
            PlayActivationCues(buff, new Pose(_playerCore.transform.position,
                _playerCore.transform.rotation));
            _readyAt[buff] = Time.time + buff.Cooldown;
            PublishSlot(slot);
            SkillSelected?.Invoke(slot);
            SkillUsed?.Invoke(slot);
            _playerCore.CombatActionFlow.EndSkill();
        }

        private void ActivateBeamPods(int slot, BeamPodSkillData skill)
        {
            BeamPodSummon prefab = skill.BeamPodGroupPrefab;
            if (prefab == null || _playerCore.CombatActionFlow?.TryBeginSkill() != true) return;

            _playerCore.CombatModule?.CancelSecondary();
            if (_beamPodGroup != null) Destroy(_beamPodGroup.gameObject);
            _beamPodGroup = null;
            _beamPodSlot = 0;
            Transform owner = _playerCore.transform;
            Quaternion rotation = Quaternion.Euler(0f, owner.eulerAngles.y, 0f);
            Vector3 spawnPosition = owner.position + rotation * prefab.transform.localPosition;
            BeamPodSummon summon = Instantiate(prefab, spawnPosition, rotation);
            if (summon.Activate(skill, _playerCore))
            {
                _beamPodGroup = summon;
                _beamPodSlot = slot;
                PlayActivationCues(skill, new Pose(summon.transform.position, summon.transform.rotation));
                _readyAt[skill] = Time.time + skill.Cooldown;
                PublishSlot(slot);
                SkillSelected?.Invoke(slot);
                SkillUsed?.Invoke(slot);
            }
            else Destroy(summon.gameObject);
            _playerCore.CombatActionFlow.EndSkill();
        }

        private void UpdateWeaponBuff()
        {
            if (_weaponBuff == null) return;
            if (!IsWeaponBuffActive)
            {
                StopWeaponBuff();
                return;
            }

            GunWeapon current = _playerCore?.EquipmentModule?.CurrentWeapon?.GetComponent<GunWeapon>();
            if (current == _buffEffectGun) return;
            _buffEffectGun?.SetSkillEffect(false);
            _buffEffectGun = current;
            _buffEffectGun?.SetSkillEffect(true, _weaponBuffEndsAt - Time.time);
        }

        private void StopWeaponBuff()
        {
            if (_weaponBuff != null) CancelScheduledAudio(_weaponBuff);
            _buffEffectGun?.SetSkillEffect(false);
            _buffEffectGun = null;
            _playerCore?.EquipmentModule?.MainWeapon?.GetComponent<GunWeapon>()?.ClearMagazineBuff();
            _playerCore?.EquipmentModule?.SubWeapon?.GetComponent<GunWeapon>()?.ClearMagazineBuff();
            _weaponBuff = null;
            _weaponBuffEndsAt = 0f;
        }

        private void ApplyMagazineBuff(float multiplier)
        {
            _playerCore.EquipmentModule?.MainWeapon?.GetComponent<GunWeapon>()?.ApplyMagazineBuff(multiplier);
            _playerCore.EquipmentModule?.SubWeapon?.GetComponent<GunWeapon>()?.ApplyMagazineBuff(multiplier);
        }

        private void OnExecuteConfirmRequested(int slot)
        {
            if (_activeSkill == null || _flow.CurrentState != ESkillState.Execute ||
                _flow.SelectedSlot != slot || GetSkill(slot) != _activeSkill ||
                _playerCore == null || !CanCastOnGround()) return;

            Camera camera = Camera.main;
            if (!SkillPlacementValidator.TryGetPlacement(_activeSkill, _playerCore.transform,
                    camera, out Pose pose)) return;

            TryBeginCast(slot, pose, false);
        }

        private bool TryBeginCast(int slot, Pose pose, bool self)
        {
            _castPose = pose;
            _castingSlot = slot;
            _castSpawned = false;

            if (_activeSkill.CastAnimationClip == null)
            {
                // 모션이 없는 스킬은 확정 시점에 바로 실행한다.
                if (!TrySpawn())
                {
                    if (_flow.IsExecuting) _flow.CancelExecute();
                    return false;
                }
                FinishCast();
                _playerCore.CombatActionFlow?.EndSkill();
                return true;
            }

            AnimationView animation = _playerCore.AnimationView;
            if (animation == null || !animation.PlaySkill(_activeSkill.AnimationState,
                    _activeSkill.CastAnimationClip, _activeSkill.CastDuration))
            {
                Debug.LogWarning($"{nameof(SkillModule)}: {_activeSkill.name} 스킬 모션을 재생할 수 없습니다.", this);
                return false;
            }

            _castTimeoutAt = Time.time + _activeSkill.CastDuration + 2f;
            if (!(self ? _flow.BeginSelfCast(slot) : _flow.BeginCast()))
            {
                animation.StopSkill();
                return false;
            }

            _preview?.Dispose();
            _preview = null;

            return true;
        }

        private bool CanCastOnGround()
        {
            if (_playerCore?.LocomotionModule?.IsGrounded != true) return false;
            ELocomotionState state = _playerCore.LocomotionStateFlow.CurrentState;
            return state == ELocomotionState.Idle || state == ELocomotionState.Move;
        }

        private void UpdateCast()
        {
            if (_activeSkill == null || _playerCore?.AnimationView == null)
            {
                _flow.CancelExecute();
                return;
            }

            // 예기치 않은 Animator 전환이 발생해도 이동 잠금이 남지 않게 한다.
            if (Time.time > _castTimeoutAt)
            {
                Debug.LogWarning($"{nameof(SkillModule)}: 스킬 모션이 종료되지 않아 실행을 중단합니다.", this);
                if (_castSpawned) FinishCast();
                else _flow.CancelExecute();
                return;
            }

            // 쉴드는 Excute 이후 대기 시간이 끝나면 스킬 레이어도 내려 이동 모션을 보인다.
            if (_activeSkill is ShieldSkillData && _castSpawned)
            {
                if (!_flow.IsMovementLocked) FinishCast();
                return;
            }

            if (!_playerCore.AnimationView.TryGetSkillProgress(out float progress)) return;

            if (progress < 1f) return;
            if (!_castSpawned)
            {
                Debug.LogWarning($"{nameof(SkillModule)}: {_activeSkill.name} 실행 이벤트 없이 스킬 모션이 끝났습니다.", this);
                _flow.CancelExecute();
                return;
            }

            FinishCast();
        }

        // 현재 시전 중인 스킬의 키 설정으로 실행/오디오 이벤트를 구분한다.
        public void HandleAnimationEvent(string eventKey, AudioSource source)
        {
            if (!_flow.IsCasting || _activeSkill == null || string.IsNullOrEmpty(eventKey)) return;

            if (string.Equals(eventKey, _activeSkill.ExecuteEventKey, StringComparison.Ordinal))
            {
                if (!_castSpawned && !TrySpawn()) _flow.CancelExecute();
                return;
            }

            AudioClip clip = string.Equals(eventKey, _activeSkill.Audio1EventKey, StringComparison.Ordinal)
                ? _activeSkill.Audio1
                : string.Equals(eventKey, _activeSkill.Audio2EventKey, StringComparison.Ordinal)
                    ? _activeSkill.Audio2 : null;
            PlaySkillAudio(clip, source);
        }

        private bool TrySpawn()
        {
            GameObject instance = Instantiate(_activeSkill.Prefab, _castPose.position, _castPose.rotation);
            // 설치형 공격도 소환한 플레이어의 치명타 설정을 사용한다.
            instance.GetComponentInChildren<CanonAttack>(true)?.BindDamageResolver(
                _playerCore.CombatModule);
            if (!_activeSkill.TryActivateInstance(instance))
            {
                Destroy(instance);
                return false;
            }

            SummonLifetime summon = instance.GetComponent<SummonLifetime>();
            ShieldInstance shieldInstance = instance.GetComponent<ShieldInstance>();
            if (summon != null || shieldInstance != null)
                _activeDeployables.Add(new ActiveDeployable(_activeSkill, instance,
                    summon, shieldInstance));

            _castSpawned = true;
            if (_activeSkill is ShieldSkillData shield)
            {
                _flow.ScheduleMovementUnlock(shield.MoveDelayAfterExecute);
                _castTimeoutAt = Mathf.Max(_castTimeoutAt,
                    Time.time + shield.MoveDelayAfterExecute + 2f);
            }
            PlayActivationCues(_activeSkill, _castPose);
            _readyAt[_activeSkill] = Time.time + _activeSkill.Cooldown;
            PublishSlot(_castingSlot);
            SkillUsed?.Invoke(_castingSlot);
            return true;
        }

        private void PlayActivationCues(SkillData skill, Pose pose)
        {
            CancelScheduledAudio(skill);
            if (skill.ActivationAudioCues.Count > 0)
            {
                _scheduledAudio.Add(new ScheduledAudio(skill));
                UpdateScheduledAudio(); // 지연 0초인 소리는 발동 프레임에 재생한다.
            }
            if (skill.ActivationEffectPrefab == null) return;

            GameObject effect = Instantiate(skill.ActivationEffectPrefab, pose.position, pose.rotation);
            float lifetime = skill.ActivationEffectLifetime;
            effect.GetComponentInChildren<BondageChainEffectLifetime>()?.Activate(lifetime);
            Destroy(effect, lifetime);
        }

        private void UpdateScheduledAudio()
        {
            for (int scheduleIndex = _scheduledAudio.Count - 1; scheduleIndex >= 0; scheduleIndex--)
            {
                ScheduledAudio schedule = _scheduledAudio[scheduleIndex];
                if (schedule.Skill is WeaponBuffSkillData &&
                    (schedule.Skill != _weaponBuff || !IsWeaponBuffActive))
                {
                    _scheduledAudio.RemoveAt(scheduleIndex);
                    continue;
                }

                bool pending = false;
                SkillAudioCue[] cues = schedule.Cues;
                for (int cueIndex = 0; cueIndex < cues.Length; cueIndex++)
                {
                    if (schedule.Played[cueIndex]) continue;
                    SkillAudioCue cue = cues[cueIndex];
                    if (cue == null || cue.Clip == null)
                    {
                        schedule.Played[cueIndex] = true;
                        continue;
                    }

                    if (Time.time - schedule.StartedAt < cue.Delay)
                    {
                        pending = true;
                        continue;
                    }

                    schedule.Played[cueIndex] = true;
                    PlaySkillAudio(cue.Clip, null, cue.Volume);
                }

                if (!pending) _scheduledAudio.RemoveAt(scheduleIndex);
            }
        }

        private void CancelScheduledAudio(SkillData skill)
        {
            _scheduledAudio.RemoveAll(schedule => schedule.Skill == skill);
        }

        private void PlaySkillAudio(AudioClip clip, AudioSource source = null, float volume = 1f)
        {
            if (clip == null) return;
            if (source == null && _playerCore?.CurrentModel != null)
                source = _playerCore.CurrentModel
                    .GetComponentInChildren<SkillAnimationEventRelay>(true)?.AudioSource;
            if (source != null && source.isActiveAndEnabled)
                source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void FinishCast()
        {
            ClearActiveSkill();
            _flow.CompleteExecute();
        }

        private void OnExecuteCanceled(int slot) => ClearActiveSkill();

        private void OnCancelRequested(int slot) => SkillCanceled?.Invoke(slot);

        private void ClearActiveSkill()
        {
            _playerCore?.AnimationView?.StopSkill();
            _preview?.Dispose();
            _preview = null;
            _activeSkill = null;
            _castingSlot = 0;
            _castSpawned = false;
        }

        private void PublishSlot(int slot)
        {
            if (_playerCore == null) return;
            SkillData skill = GetSkill(slot);
            float endTime = skill != null && _readyAt.TryGetValue(skill, out float readyAt)
                ? readyAt : 0f;
            _playerCore.SkillDisplayChannel.Publish(new SkillSlotDisplayState(slot, skill?.Icon,
                skill?.Cooldown ?? 0f, endTime));
        }
    }
}
