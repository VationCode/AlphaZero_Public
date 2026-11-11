using System;
using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>보스 Combat·Locomotion 상태를 Animator의 공격·이동 표현으로 연결한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BossAnimationView : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private BossLocomotionAudioView _locomotionAudio;
        [SerializeField] private string _idleState = "Idle";
        [SerializeField] private string _deathState = "Death";
        [SerializeField] private string _chaseState = "Walk_F";
        [SerializeField] private string _attack1State = "Attack1";
        [SerializeField] private string _attack2State = "Attack2";
        [SerializeField] private string _attack3State = "Attack3";
        [SerializeField] private string _attack4State = "Attack4";
        [SerializeField] private string _attack5State = "Attack5";
        [SerializeField] private string _attack6State = "Attack6";
        [SerializeField] private string _attack7State = "Attack7";
        [SerializeField] private string _attack8State = "Attack8";

        private BossActionStateFlow _actionStateFlow;
        private BossCombatFlow _flow;
        private BossLocomotionStateFlow _locomotionStateFlow;
        private bool _awaitingAttackEnd;
        private string _currentAttackState;
        private Transform _dashMotionRoot;
        private Transform _dashPivotRoot;
        private float _dashRootRestZ;
        private float _dashPivotRestZ;
        private bool _dashCompensating;
        private bool _dashActive;
        private float _dashStartRootZ;
        private float _dashStartNormalizedTime;
        private float _dashEndNormalizedTime;
        private string _dashAnimationState;
        private string _activeMovementId;
        private EBossAttackMovementType _activeMovementType;
        private bool _jumpActive;
        private Transform _jumpJoint;
        private Transform _jumpPivot;
        private float _jumpStartJointY;
        private Vector3 _jumpJointAnimatedPosition;
        private Vector3 _jumpPivotAnimatedPosition;
        private bool _jumpPoseCompensated;
        private float _jumpStartNormalizedTime;
        private float _jumpEndNormalizedTime;
        private readonly List<Vector3> _attack7SpawnPositions = new();

        public bool IsReady => _animator != null && _animator.runtimeAnimatorController != null;
        public event Action AttackAnimationFinished;
        public event Action<string> MovementStarted;
        // ID, 진행률, 애니메이션 높이, 애니메이션 전방 이동량.
        public event Action<string, float, float, float> MovementProgressChanged;
        public event Action<string> MovementEnded;

        public void NotifyEffect(string eventKey, bool active)
        {
            string attackId = _flow?.CurrentAnimationAttackId;
            if (string.IsNullOrEmpty(attackId)) return;
            if (active && _flow.CurrentState != EBossCombatState.Attacking) return;
            GetEffectView(attackId)?.SetEffect(eventKey, active);
        }

        public void NotifyTargetingStart() => _flow?.CaptureTargetingPosition();

        public void NotifyHitStart() => _flow?.BeginHitWindow();
        public void NotifyHitEnd() => _flow?.EndHitWindow();

        public void NotifyEffectSpawn(string eventKey) => _flow?.BeginEffectSpawn(eventKey);
        public void NotifyFootstep() => _locomotionAudio?.PlayFootstep();

        // 클립의 공격 ID에 해당하는 패턴 View로 생성 요청을 전달한다.
        private void HandleEffectSpawnRequested(string attackId, string eventKey)
        {
            if (_flow == null || _flow.CurrentState != EBossCombatState.Attacking ||
                _flow.CurrentAnimationAttackId != attackId) return;

            BossAttackEffectView effects = GetEffectView(attackId);
            if (effects != null && effects.IsRandomTargetArea(eventKey))
            {
                if (attackId == "Attack7" &&
                    effects.TryGetSpawnSpacing(eventKey, out float spacing) &&
                    _flow.TryGetAttack7SpawnPositions(spacing, _attack7SpawnPositions))
                    effects.SpawnEffectAtPositions(eventKey, _attack7SpawnPositions);
                return;
            }

            bool hasTargetPosition = _flow.TryGetTargetingPosition(out Vector3 position);
            effects?.SpawnEffect(eventKey, hasTargetPosition, position);
        }

        private BossAttackEffectView GetEffectView(string attackId)
        {
            return _flow != null && _flow.TryGetPattern(attackId, out BossAttackPattern pattern)
                ? pattern.GetComponent<BossAttackEffectView>() : null;
        }

        private void StopAllEffects()
        {
            if (_flow == null) return;
            foreach (BossAttackPattern pattern in _flow.AttackPatterns)
                if (pattern != null)
                    pattern.GetComponent<BossAttackEffectView>()?.StopAll();
        }

        // 클립 이벤트의 ID를 현재 패턴의 이동 설정과 연결한다.
        public void NotifyMovementStart(AnimationEvent animationEvent)
        {
            string movementId = animationEvent?.stringParameter;
            if (_flow?.CurrentState != EBossCombatState.Attacking ||
                string.IsNullOrWhiteSpace(_flow.CurrentAnimationAttackId) ||
                string.IsNullOrWhiteSpace(movementId) || _activeMovementId != null ||
                !_flow.TryGetAnimationMovement(movementId,
                    out BossAttackMovementSettings movement) ||
                !TryGetEventInterval(animationEvent, movementId,
                    out float startTime, out float endTime)) return;

            if (movement.Type == EBossAttackMovementType.Jump)
            {
                _jumpJoint ??= _animator.transform.Find("root/MainJoint");
                _jumpPivot ??= _animator.transform.Find("Pivot_for_MainJoint");
                if (_jumpJoint == null)
                {
                    Debug.LogError($"{name}: 점프용 root/MainJoint를 찾지 못했습니다.", this);
                    return;
                }
                _jumpStartJointY = _jumpJoint.localPosition.y;
                _jumpStartNormalizedTime = startTime;
                _jumpEndNormalizedTime = endTime;
                _jumpActive = true;
            }
            else if (movement.Type == EBossAttackMovementType.Dash)
            {
                if (!_dashCompensating) BeginDashCompensation();
                if (_dashMotionRoot == null) return;
                _dashStartRootZ = _dashMotionRoot.localPosition.z;
                _dashStartNormalizedTime = startTime;
                _dashEndNormalizedTime = endTime;
                _dashActive = true;
            }
            else return;

            _activeMovementId = movementId;
            _activeMovementType = movement.Type;
            MovementStarted?.Invoke(movementId);
        }

        public void NotifyMovementEnd(AnimationEvent animationEvent)
        {
            string movementId = animationEvent?.stringParameter;
            if (string.IsNullOrEmpty(movementId) ||
                movementId != _activeMovementId) return;

            if (_activeMovementType == EBossAttackMovementType.Dash &&
                _dashActive && _dashMotionRoot != null)
            {
                // 종료 이벤트의 마지막 클립 좌표를 물리에 전달한 뒤 돌진을 확정한다.
                MovementProgressChanged?.Invoke(movementId, 1f, 0f,
                    GetAnimatedDashDistance());
                _dashActive = false;
            }
            else if (_activeMovementType == EBossAttackMovementType.Jump)
                _jumpActive = false;

            _activeMovementId = null;
            MovementEnded?.Invoke(movementId);
            RestoreJumpJointPose();
        }

        private bool TryGetEventInterval(AnimationEvent animationEvent, string movementId,
            out float startNormalizedTime, out float endNormalizedTime)
        {
            startNormalizedTime = 0f;
            endNormalizedTime = 0f;
            AnimationClip clip = animationEvent?.animatorClipInfo.clip;
            if (clip == null || clip.length <= 0f)
            {
                Debug.LogError($"{name}: 공격 이벤트의 애니메이션 클립을 읽지 못했습니다.", this);
                return false;
            }

            float startTime = animationEvent.time;
            float endTime = float.PositiveInfinity;
            foreach (AnimationEvent clipEvent in clip.events)
            {
                if (clipEvent.functionName ==
                        nameof(BossAttackAnimationEventRelay.MovementEnd) &&
                    clipEvent.stringParameter == movementId &&
                    clipEvent.time > startTime)
                    endTime = Mathf.Min(endTime, clipEvent.time);
            }
            if (float.IsPositiveInfinity(endTime))
            {
                Debug.LogError($"{name}: {clip.name} 클립에 {movementId} MovementEnd 이벤트가 없습니다.", this);
                return false;
            }

            startNormalizedTime = startTime / clip.length;
            endNormalizedTime = endTime / clip.length;
            return true;
        }

        public void Bind(BossActionStateFlow actionStateFlow, BossCombatFlow flow,
            BossLocomotionStateFlow locomotionStateFlow)
        {
            if (_actionStateFlow != null)
                _actionStateFlow.StateChanged -= HandleActionStateChanged;
            if (_flow != null) _flow.StateChanged -= HandleStateChanged;
            if (_flow != null) _flow.AttackStageChanged -= HandleAttackStageChanged;
            if (_flow != null) _flow.EffectSpawnRequested -= HandleEffectSpawnRequested;
            if (_locomotionStateFlow != null)
                _locomotionStateFlow.StateChanged -= HandleLocomotionStateChanged;
            _actionStateFlow = actionStateFlow;
            _flow = flow;
            _locomotionStateFlow = locomotionStateFlow;
            if (isActiveAndEnabled && _actionStateFlow != null)
                _actionStateFlow.StateChanged += HandleActionStateChanged;
            if (isActiveAndEnabled && _flow != null)
            {
                _flow.StateChanged += HandleStateChanged;
                _flow.AttackStageChanged += HandleAttackStageChanged;
                _flow.EffectSpawnRequested += HandleEffectSpawnRequested;
            }
            if (isActiveAndEnabled && _locomotionStateFlow != null)
                _locomotionStateFlow.StateChanged += HandleLocomotionStateChanged;
        }

        private void Start()
        {
            if (_actionStateFlow?.CurrentState == EBossActionState.Dead)
                HandleActionStateChanged(EBossActionState.Dead);
            else if (_flow != null) HandleStateChanged(_flow.CurrentState);
        }

        private void OnEnable()
        {
            if (_flow == null) return;
            if (_actionStateFlow != null)
            {
                _actionStateFlow.StateChanged -= HandleActionStateChanged;
                _actionStateFlow.StateChanged += HandleActionStateChanged;
            }
            _flow.StateChanged -= HandleStateChanged;
            _flow.AttackStageChanged -= HandleAttackStageChanged;
            _flow.EffectSpawnRequested -= HandleEffectSpawnRequested;
            _flow.StateChanged += HandleStateChanged;
            _flow.AttackStageChanged += HandleAttackStageChanged;
            _flow.EffectSpawnRequested += HandleEffectSpawnRequested;
            if (_locomotionStateFlow != null)
            {
                _locomotionStateFlow.StateChanged -= HandleLocomotionStateChanged;
                _locomotionStateFlow.StateChanged += HandleLocomotionStateChanged;
            }
            if (_actionStateFlow?.CurrentState == EBossActionState.Dead)
                HandleActionStateChanged(EBossActionState.Dead);
            else HandleStateChanged(_flow.CurrentState);
        }

        private void OnDisable()
        {
            bool restoreDashPose = _dashCompensating;
            StopAllEffects();
            _awaitingAttackEnd = false;
            _currentAttackState = null;
            _dashCompensating = false;
            _dashActive = false;
            _jumpActive = false;
            _activeMovementId = null;
            RestoreJumpJointPose();
            if (restoreDashPose)
            {
                if (_dashMotionRoot != null) ResetRootZ(_dashMotionRoot, _dashRootRestZ);
                if (_dashPivotRoot != null) ResetRootZ(_dashPivotRoot, _dashPivotRestZ);
            }
            if (_locomotionStateFlow != null)
                _locomotionStateFlow.StateChanged -= HandleLocomotionStateChanged;
            if (_actionStateFlow != null)
                _actionStateFlow.StateChanged -= HandleActionStateChanged;
            if (_flow == null) return;
            _flow.StateChanged -= HandleStateChanged;
            _flow.AttackStageChanged -= HandleAttackStageChanged;
            _flow.EffectSpawnRequested -= HandleEffectSpawnRequested;
        }

        private void LateUpdate()
        {
            UpdateJumpAnimation();
            UpdateDashAnimation();
            if (!_awaitingAttackEnd || !IsReady || _animator.IsInTransition(0)) return;

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != Animator.StringToHash(_currentAttackState) ||
                state.normalizedTime < 1f) return;

            _awaitingAttackEnd = false;
            _currentAttackState = null;
            AttackAnimationFinished?.Invoke();
        }

        private void UpdateJumpAnimation()
        {
            if (!_jumpActive || _activeMovementId == null ||
                _activeMovementType != EBossAttackMovementType.Jump) return;
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != Animator.StringToHash(_currentAttackState)) return;

            float progress = Mathf.InverseLerp(_jumpStartNormalizedTime,
                _jumpEndNormalizedTime, state.normalizedTime);
            // MainJoint의 실제 상승량을 물리 루트에 넘긴다. 웅크림과 착지는 지면에 둔다.
            float jointRise = _jumpJoint.localPosition.y - _jumpStartJointY;
            float height = Mathf.Max(0f, _jumpJoint.parent
                .TransformVector(Vector3.up * jointRise).y);
            MovementProgressChanged?.Invoke(_activeMovementId, progress, height, 0f);

            // 물리 몸체에 옮긴 애니메이션 높이만 관절에서 제거한다.
            float appliedHeight = _flow?.CurrentAnimatedJumpHeight ?? 0f;
            _jumpJointAnimatedPosition = _jumpJoint.localPosition;
            _jumpJoint.localPosition -= _jumpJoint.parent
                .InverseTransformVector(Vector3.up * appliedHeight);
            if (_jumpPivot != null)
            {
                _jumpPivotAnimatedPosition = _jumpPivot.localPosition;
                _jumpPivot.localPosition -= _jumpPivot.parent
                    .InverseTransformVector(Vector3.up * appliedHeight);
            }
            _jumpPoseCompensated = true;
        }

        private void RestoreJumpJointPose()
        {
            if (!_jumpPoseCompensated) return;
            if (_jumpJoint != null)
                _jumpJoint.localPosition = _jumpJointAnimatedPosition;
            if (_jumpPivot != null)
                _jumpPivot.localPosition = _jumpPivotAnimatedPosition;
            _jumpPoseCompensated = false;
        }

        private void UpdateDashAnimation()
        {
            if (!_dashCompensating || _dashMotionRoot == null) return;

            // 애니메이션 전방 이동량을 물리에 넘긴 뒤 자식 root의 중복 이동을 지운다.
            if (_dashActive && _activeMovementId != null &&
                _activeMovementType == EBossAttackMovementType.Dash)
            {
                AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
                if (state.shortNameHash == Animator.StringToHash(_currentAttackState))
                {
                    float progress = Mathf.InverseLerp(_dashStartNormalizedTime,
                        _dashEndNormalizedTime, state.normalizedTime);
                    MovementProgressChanged?.Invoke(_activeMovementId, progress, 0f,
                        GetAnimatedDashDistance());
                }
            }

            ResetRootZ(_dashMotionRoot, _dashRootRestZ);
            if (_dashPivotRoot != null)
                ResetRootZ(_dashPivotRoot, _dashPivotRestZ);

            // 돌진 클립에서 다음 상태로 블렌딩하는 동안에도 이동을 중복 적용하지 않는다.
            if (!_animator.IsInTransition(0) &&
                _animator.GetCurrentAnimatorStateInfo(0).shortNameHash !=
                    Animator.StringToHash(_dashAnimationState))
                _dashCompensating = false;
        }

        private float GetAnimatedDashDistance()
        {
            if (_dashMotionRoot == null) return 0f;
            float localDistance = _dashMotionRoot.localPosition.z -
                _dashStartRootZ;
            float worldScale = _dashMotionRoot.parent != null
                ? _dashMotionRoot.parent.TransformVector(Vector3.forward).magnitude
                : 1f;
            return localDistance * worldScale;
        }

        private static void ResetRootZ(Transform root, float restZ)
        {
            Vector3 position = root.localPosition;
            position.z = restZ;
            root.localPosition = position;
        }

        private void BeginDashCompensation()
        {
            if (_dashMotionRoot == null)
            {
                _dashMotionRoot = _animator.transform.Find("root");
                if (_dashMotionRoot == null)
                {
                    Debug.LogError($"{name}: 돌진용 root Transform을 찾지 못했습니다.", this);
                    return;
                }
                _dashRootRestZ = _dashMotionRoot.localPosition.z;
                _dashPivotRoot = _animator.transform.Find("Pivot_for_root");
                if (_dashPivotRoot != null)
                    _dashPivotRestZ = _dashPivotRoot.localPosition.z;
            }
            _dashCompensating = true;
            _dashActive = false;
            _dashAnimationState = _currentAttackState;
        }

        private void HandleAttackStageChanged()
        {
            StopAllEffects();
            HandleStateChanged(EBossCombatState.Attacking);
        }

        private void HandleActionStateChanged(EBossActionState state)
        {
            if (state != EBossActionState.Dead) return;

            StopAllEffects();
            _locomotionAudio?.StopFootsteps();
            _awaitingAttackEnd = false;
            _currentAttackState = null;
            _jumpActive = false;
            _dashActive = false;
            _activeMovementId = null;
            RestoreJumpJointPose();
            if (_dashCompensating)
            {
                if (_dashMotionRoot != null) ResetRootZ(_dashMotionRoot, _dashRootRestZ);
                if (_dashPivotRoot != null) ResetRootZ(_dashPivotRoot, _dashPivotRestZ);
                _dashCompensating = false;
            }
            if (!IsReady || !_animator.isActiveAndEnabled) return;
            // 사망 상태가 전투 Idle 이벤트보다 우선하도록 전용 상태를 직접 재생한다.
            _animator.CrossFadeInFixedTime(_deathState, 0.1f, 0, 0f);
        }

        private void HandleStateChanged(EBossCombatState state)
        {
            if (_actionStateFlow?.CurrentState == EBossActionState.Dead ||
                !IsReady || !_animator.isActiveAndEnabled) return;
            bool isChasing = state == EBossCombatState.Idle &&
                _locomotionStateFlow?.CurrentState == EBossLocomotionState.Chasing;
            string attackId = state == EBossCombatState.Attacking
                ? _flow?.CurrentAnimationAttackId : null;
            if (state != EBossCombatState.Attacking) StopAllEffects();
            _jumpActive = false;
            _dashActive = false;
            _activeMovementId = null;
            RestoreJumpJointPose();
            _currentAttackState = attackId switch
            {
                "Attack1" => _attack1State,
                "Attack2" => _attack2State,
                "Attack3" => _attack3State,
                "Attack4" => _attack4State,
                "Attack5" => _attack5State,
                "Attack6" => _attack6State,
                "Attack7" => _attack7State,
                "Attack8" => _attack8State,
                _ => null
            };
            if (attackId != null && _flow?.CurrentPattern?
                    .HasMovementType(EBossAttackMovementType.Dash) == true)
                BeginDashCompensation();
            _awaitingAttackEnd = !string.IsNullOrEmpty(_currentAttackState);
            string animationState = state switch
            {
                EBossCombatState.Idle => isChasing ? _chaseState : _idleState,
                EBossCombatState.AttackRecovery => _idleState,
                EBossCombatState.Attacking => _currentAttackState ?? _chaseState,
                _ => null
            };
            if (string.IsNullOrEmpty(animationState)) return;
            if (state == EBossCombatState.Idle && !isChasing)
            {
                if (!_animator.IsInTransition(0) &&
                    _animator.GetCurrentAnimatorStateInfo(0).shortNameHash ==
                    Animator.StringToHash(_idleState)) return;
                _animator.Play(animationState, 0, 0f);
            }
            else if (state == EBossCombatState.Attacking && !_animator.IsInTransition(0) &&
                     _animator.GetCurrentAnimatorStateInfo(0).shortNameHash ==
                     Animator.StringToHash(animationState))
                _animator.Play(animationState, 0, 0f);
            else
                _animator.CrossFadeInFixedTime(animationState,
                    isChasing ? 0.05f : 0.15f, 0, 0f);
        }

        private void HandleLocomotionStateChanged(EBossLocomotionState state)
        {
            if (_flow?.CurrentState == EBossCombatState.Idle)
                HandleStateChanged(_flow.CurrentState);
        }
    }
}
