using System;
using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>피격 종류별 반응 수치와 넉백·넉다운 밀림을 계산·실행한다.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-160)]
    public sealed class PlayerHitModule : MonoBehaviour
    {
        [Serializable]
        private struct HitTiming
        {
            [Tooltip("피격 반응 시간입니다. 넉다운에서는 넘어지는 반응 시간입니다.")]
            [SerializeField, Min(0f)] private float _reactionDuration;
            [Tooltip("피해를 받은 뒤 모든 공격의 체력 감소를 막는 시간입니다.")]
            [SerializeField, Min(0f)] private float _damageInvulnerabilityDuration;
            [Tooltip("반응 종료 후 같은 종류의 반응만 막는 시간입니다.")]
            [SerializeField, Min(0f)] private float _reactionImmunityDuration;

            public float ReactionDuration => _reactionDuration;
            public float DamageInvulnerabilityDuration => _damageInvulnerabilityDuration;
            public float ReactionImmunityDuration => _reactionImmunityDuration;

            public HitTiming(float reactionDuration, float damageInvulnerabilityDuration,
                float reactionImmunityDuration)
            {
                _reactionDuration = reactionDuration;
                _damageInvulnerabilityDuration = damageInvulnerabilityDuration;
                _reactionImmunityDuration = reactionImmunityDuration;
            }

            public void Validate()
            {
                _reactionDuration = Mathf.Max(0f, _reactionDuration);
                _damageInvulnerabilityDuration = Mathf.Max(0f, _damageInvulnerabilityDuration);
                _reactionImmunityDuration = Mathf.Max(0f, _reactionImmunityDuration);
            }
        }

        [SerializeField] private HitTiming _normal = new(0.15f, 0.1f, 0.35f);
        [SerializeField] private HitTiming _stagger = new(0.35f, 0.15f, 0.6f);
        [SerializeField] private HitTiming _knockback = new(1.5f, 1.5f, 0.6f);
        [Tooltip("넉백 피격에서 플레이어가 밀리는 거리입니다.")]
        [SerializeField, Min(0f)] private float _knockbackDistance = 4f;
        [Tooltip("넉백 기본 거리를 이동하는 시간입니다. 추가 거리는 같은 속도로 이동합니다.")]
        [SerializeField, Min(0f)] private float _knockbackMoveDuration = 1f;

        [SerializeField] private HitTiming _knockdown = new(1.1f, 1.5f, 1.5f);
        [Tooltip("넉다운 피격에서 플레이어가 넘어지는 동안 밀리는 거리입니다.")]
        [SerializeField, Min(0f)] private float _knockdownDistance = 12f;
        [Tooltip("넉다운 기본 거리를 이동하는 시간입니다. 추가 거리는 같은 속도로 이동합니다.")]
        [SerializeField, Min(0f)] private float _knockdownMoveDuration = 1.2f;
        [Tooltip("넉다운 반응이 끝난 뒤 누운 상태를 유지하는 시간입니다.")]
        [SerializeField, Min(0f)] private float _knockdownLyingDuration = 1.5f;
        [SerializeField, Min(0f)] private float _standUpDuration = 0.95f;
        [Tooltip("피격 이동이 너무 빠르게 튀지 않도록 제한하는 최대 속도입니다.")]
        [SerializeField, Min(0.01f)] private float _maximumKnockbackSpeed = 10f;

        private PlayerMotionModule _motion;
        private Transform _owner;
        private float _hitMotionRemaining;
        private float _hitMotionSpeed;
        private Vector3 _hitMotionDirection;

        public bool IsHitMotionActive => _hitMotionRemaining > 0f;
        public float HitMotionRemainingDuration => _hitMotionRemaining;
        public float KnockdownLyingDuration => _knockdownLyingDuration;
        public float StandUpDuration => _standUpDuration;
        public float GetReactionDuration(EPlayerHitType type) => GetTiming(type).ReactionDuration;
        public float GetDamageInvulnerabilityDuration(EPlayerHitType type) =>
            GetTiming(type).DamageInvulnerabilityDuration;
        public float GetReactionImmunityDuration(EPlayerHitType type) =>
            GetTiming(type).ReactionImmunityDuration;

        private void Awake()
        {
            PlayerRoot core = GetComponentInParent<PlayerRoot>(true);
            _owner = core?.transform;
            _motion = core?.GetComponent<PlayerMotionModule>();
            if (_owner == null || _motion == null)
                Debug.LogError($"{name}: PlayerRoot와 PlayerMotionModule이 필요합니다.", this);
        }

        public void BeginHitMotion(in PlayerHitInfo hit)
        {
            _hitMotionRemaining = 0f;
            if (_owner == null || _motion == null ||
                (hit.Type != EPlayerHitType.Knockback && hit.Type != EPlayerHitType.Knockdown)) return;

            float baseDistance = hit.Type == EPlayerHitType.Knockback
                ? _knockbackDistance : _knockdownDistance;
            float duration = hit.Type == EPlayerHitType.Knockback
                ? _knockbackMoveDuration : _knockdownMoveDuration;

            Vector3 direction = hit.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f && hit.Source != null)
                direction = _owner.position - hit.Source.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = _owner.forward;
            _hitMotionDirection = direction.normalized;
            float minimumDistance = hit.MinimumDistance;
            if (hit.ContactBodies != null && _motion.TryGetContactClearanceDistance(
                    hit.ContactBodies, _hitMotionDirection, hit.ContactPadding,
                    out float contactDistance))
                minimumDistance = Mathf.Max(minimumDistance, contactDistance);
            // 자신의 충돌체를 기준으로 몸체 밖까지 필요한 거리를 기본 밀림과 비교한다.
            float distance = Mathf.Max(baseDistance + hit.AdditionalDistance,
                minimumDistance);
            // 충돌 지점을 바라본 채 반대 방향으로 밀린다.
            _motion.FaceHitDirection(-_hitMotionDirection);
            if (distance <= 0f) return;
            if (hit.MoveDurationOverride <= 0f && duration <= 0f) return;
            float requestedSpeed = hit.MoveDurationOverride > 0f
                ? distance / hit.MoveDurationOverride
                : baseDistance > 0f ? baseDistance / duration : distance / duration;
            _hitMotionSpeed = Mathf.Min(requestedSpeed,
                Mathf.Max(0.01f, _maximumKnockbackSpeed));
            _hitMotionRemaining = distance / _hitMotionSpeed;
        }

        public void StopHitMotion() => _hitMotionRemaining = 0f;
        private void OnDisable() => StopHitMotion();

#if UNITY_EDITOR
        // 인스펙터 테스트도 실제 공격과 같은 피격 경로를 통과한다.
        public bool TryReceiveTestHit(EPlayerHitType type)
        {
            if (!Application.isPlaying) return false;
            PlayerRoot core = GetComponentInParent<PlayerRoot>(true);
            if (core == null) return false;
            PlayerHitInfo hit = new(1f, type, null, -core.transform.forward);
            return core.TryReceiveHit(hit);
        }
#endif

        // Action State Flow가 일반 입력보다 먼저 호출해 한 프레임에 한 번만 움직인다.
        public bool TickHitMotion(float deltaTime)
        {
            if (_hitMotionRemaining <= 0f) return false;
            float step = Mathf.Min(Mathf.Max(0f, deltaTime), _hitMotionRemaining);
            _hitMotionRemaining -= step;
            _motion?.MoveByHit(_hitMotionDirection * (_hitMotionSpeed * step),
                deltaTime);
            return true;
        }

        private HitTiming GetTiming(EPlayerHitType type) => type switch
        {
            EPlayerHitType.Normal => _normal,
            EPlayerHitType.Stagger => _stagger,
            EPlayerHitType.Knockback => _knockback,
            EPlayerHitType.Knockdown => _knockdown,
            _ => default
        };

        private void OnValidate()
        {
            _knockdownLyingDuration = Mathf.Max(0f, _knockdownLyingDuration);
            _standUpDuration = Mathf.Max(0f, _standUpDuration);
            _knockbackDistance = Mathf.Max(0f, _knockbackDistance);
            _knockbackMoveDuration = Mathf.Max(0f, _knockbackMoveDuration);
            _knockdownDistance = Mathf.Max(0f, _knockdownDistance);
            _knockdownMoveDuration = Mathf.Max(0f, _knockdownMoveDuration);
            if (_maximumKnockbackSpeed <= 0f) _maximumKnockbackSpeed = 10f;
            _normal.Validate();
            _stagger.Validate();
            _knockback.Validate();
            _knockdown.Validate();
        }
    }
}
