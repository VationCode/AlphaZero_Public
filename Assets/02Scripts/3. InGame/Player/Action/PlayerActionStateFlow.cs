using System;
using UnityEngine;

namespace alpha.ingame.player
{
    public enum EPlayerHitPhase
    {
        None,
        Normal,
        Stagger,
        Knockback,
        Knockdown,
        Lying,
        Standing,
        Dead
    }

    /// <summary>플레이어 전체 행동을 제어하는 피격 반응 상태와 전환을 결정한다.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    public sealed class PlayerActionStateFlow : MonoBehaviour
    {
        private PlayerRoot _core;
        private PlayerHealthModule _health;
        private PlayerHitModule _hit;
        private Transform _owner;
        private EPlayerHitType _currentType;
        private float _phaseEndsAt;
        private float _lyingDuration;
        private float _invulnerableUntil;
        private readonly float[] _reactionImmunityEndsAt = new float[4];

        public EPlayerHitPhase CurrentPhase { get; private set; }
        public bool DidMoveByHitThisFrame { get; private set; }
        public bool IsActionBlocked => CurrentPhase is EPlayerHitPhase.Stagger or
            EPlayerHitPhase.Knockback or EPlayerHitPhase.Knockdown or
            EPlayerHitPhase.Lying or EPlayerHitPhase.Standing or EPlayerHitPhase.Dead;

        public event Action<PlayerHitInfo> HitReceived;
        public event Action<EPlayerHitPhase> PhaseChanged;

        private void Awake()
        {
            PlayerRoot core = GetComponentInParent<PlayerRoot>(true);
            _core = core;
            _owner = core?.transform;
            _health = core?.GetComponentInChildren<PlayerHealthModule>(true);
            _hit = core?.GetComponentInChildren<PlayerHitModule>(true);
            if (_owner == null || _health == null || _hit == null)
                Debug.LogError($"{name}: PlayerRoot, PlayerHealthModule, PlayerHitModule이 필요합니다.", this);
        }

        private void OnEnable()
        {
            if (_health != null) _health.Died += HandleDeath;
        }

        private void OnDisable()
        {
            DidMoveByHitThisFrame = false;
            if (_health != null) _health.Died -= HandleDeath;
            _hit?.StopHitMotion();
            if (CurrentPhase != EPlayerHitPhase.Dead) ChangePhase(EPlayerHitPhase.None);
        }

        public bool TryReceiveHit(in PlayerHitInfo hit)
        {
            if (!isActiveAndEnabled || _owner == null || _health == null ||
                _hit == null || !_hit.isActiveAndEnabled || _health.IsDead ||
                hit.Damage < 0f || !Enum.IsDefined(typeof(EPlayerHitType), hit.Type) ||
                (hit.Damage > 0f && Time.time < _invulnerableUntil) ||
                (hit.Source != null &&
                    (hit.Source == _owner || hit.Source.IsChildOf(_owner)))) return false;

            // 체력과 피해 무적은 실제 피해가 있을 때만 처리한다.
            if (hit.Damage > 0f)
            {
                if (!_health.TryApplyDamage(hit.Damage)) return false;
                _invulnerableUntil = Time.time + _hit.GetDamageInvulnerabilityDuration(hit.Type);
            }
            HitReceived?.Invoke(hit);
            if (_health.IsDead) return true;

            // 피해는 적용하되 면역 중이거나 약한 공격이면 현재 반응을 유지한다.
            if (Time.time < _reactionImmunityEndsAt[(int)hit.Type]) return true;
            if (CurrentPhase != EPlayerHitPhase.None && hit.Type <= _currentType) return true;

            if (CurrentPhase != EPlayerHitPhase.None) BeginReactionImmunity();
            _currentType = hit.Type;
            if (hit.Type is EPlayerHitType.Knockback or EPlayerHitType.Knockdown)
                _core?.LocomotionModule?.StopForForcedMotion();
            _hit.BeginHitMotion(hit);
            // 넉다운은 넘어지는 클립 뒤 누운 상태로 전환하고, 남은 밀림은 누운 상태에서도 진행한다.
            float recovery = Mathf.Max(0.01f, _hit.GetReactionDuration(hit.Type));
            if (hit.Type == EPlayerHitType.Knockback)
                recovery = Mathf.Max(recovery, _hit.HitMotionRemainingDuration);
            switch (hit.Type)
            {
                case EPlayerHitType.Normal:
                    ChangePhase(EPlayerHitPhase.Normal);
                    _phaseEndsAt = Time.time + recovery;
                    break;
                case EPlayerHitType.Stagger:
                    ChangePhase(EPlayerHitPhase.Stagger);
                    _phaseEndsAt = Time.time + recovery;
                    break;
                case EPlayerHitType.Knockback:
                    ChangePhase(EPlayerHitPhase.Knockback);
                    _phaseEndsAt = Time.time + recovery;
                    break;
                case EPlayerHitType.Knockdown:
                    _lyingDuration = _hit.KnockdownLyingDuration;
                    ChangePhase(EPlayerHitPhase.Knockdown);
                    _phaseEndsAt = Time.time + recovery;
                    break;
            }
            return true;
        }

        private void Update()
        {
            DidMoveByHitThisFrame = _hit?.TickHitMotion(Time.deltaTime) == true;
            if (CurrentPhase is EPlayerHitPhase.None or EPlayerHitPhase.Dead) return;
            // Builtin처럼 밀림이 끝난 뒤부터 누운 상태의 회복 시간을 센다.
            if (CurrentPhase == EPlayerHitPhase.Lying && _hit?.IsHitMotionActive == true)
            {
                _phaseEndsAt += Time.deltaTime;
                return;
            }
            if (Time.time < _phaseEndsAt) return;

            switch (CurrentPhase)
            {
                case EPlayerHitPhase.Normal:
                case EPlayerHitPhase.Stagger:
                case EPlayerHitPhase.Knockback:
                    ChangePhase(EPlayerHitPhase.None);
                    break;
                case EPlayerHitPhase.Knockdown:
                    ChangePhase(EPlayerHitPhase.Lying);
                    _phaseEndsAt = Time.time + _lyingDuration;
                    break;
                case EPlayerHitPhase.Lying:
                    ChangePhase(EPlayerHitPhase.Standing);
                    _phaseEndsAt = Time.time + _hit.StandUpDuration;
                    break;
                case EPlayerHitPhase.Standing:
                    ChangePhase(EPlayerHitPhase.None);
                    break;
            }
        }

        private void HandleDeath()
        {
            _hit?.StopHitMotion();
            ChangePhase(EPlayerHitPhase.Dead);
        }

        private void ChangePhase(EPlayerHitPhase next)
        {
            if (CurrentPhase == next) return;
            if (next == EPlayerHitPhase.None && CurrentPhase != EPlayerHitPhase.Dead)
                BeginReactionImmunity();
            CurrentPhase = next;
            if (next == EPlayerHitPhase.None) _currentType = EPlayerHitType.Normal;
            PhaseChanged?.Invoke(next);
        }

        private void BeginReactionImmunity()
        {
            float duration = _hit.GetReactionImmunityDuration(_currentType);
            int index = (int)_currentType;
            _reactionImmunityEndsAt[index] = Mathf.Max(
                _reactionImmunityEndsAt[index], Time.time + duration);
        }
    }
}
