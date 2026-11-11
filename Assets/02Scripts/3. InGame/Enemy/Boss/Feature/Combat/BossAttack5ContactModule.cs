using System;
using alpha.ingame.player;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>Attack5의 실제 몸체 접촉과 측면 피격 방향을 판정한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossAttackAreaModule))]
    [RequireComponent(typeof(BossAttackInstance))]
    public sealed class BossAttack5ContactModule : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _contactMoveDuration = 0.3f;
        [SerializeField, Min(0f)] private float _contactPadding = 0.3f;

        private BossAttackAreaModule _area;
        private BossAttackInstance _attack;
        private Transform _source;
        private Func<Transform> _targetProvider;
        private BoxCollider[] _groundBodyColliders = Array.Empty<BoxCollider>();
        private Vector3 _pathForward;

        public void Bind(Transform source, Func<Transform> targetProvider,
            BoxCollider[] groundBodyColliders)
        {
            _area = GetComponent<BossAttackAreaModule>();
            _attack = GetComponent<BossAttackInstance>();
            _source = source;
            _targetProvider = targetProvider;
            _groundBodyColliders = groundBodyColliders ?? Array.Empty<BoxCollider>();
        }

        private void OnEnable()
        {
            _pathForward = _source != null
                ? Vector3.ProjectOnPlane(_source.forward, Vector3.up) : Vector3.forward;
            if (_pathForward.sqrMagnitude <= 0.0001f)
                _pathForward = Vector3.forward;
            _pathForward.Normalize();
        }

        private void FixedUpdate()
        {
            if (_attack?.IsHitWindowActive != true ||
                _source == null || _area == null) return;

            Transform target = _targetProvider?.Invoke();
            if (target == null || !target.gameObject.activeInHierarchy) return;
            CharacterController player = target.GetComponentInParent<CharacterController>() ??
                target.GetComponentInChildren<CharacterController>();
            PlayerRoot receiver = player != null
                ? player.GetComponentInParent<PlayerRoot>() : null;
            if (player == null || !player.enabled || receiver == null) return;

            Collider[] candidates = _area.OverlapTargets(out int count,
                out Vector3 center,
                out Quaternion rotation);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = candidates[i];
                if (candidate == null || !candidate.enabled ||
                    !candidate.transform.IsChildOf(target) &&
                    candidate.transform != target ||
                    !_area.Contains(candidate, center, rotation)) continue;

                Vector3 right = Vector3.Cross(Vector3.up, _pathForward);
                float side = Vector3.Dot(player.bounds.center - center, right);
                if (Mathf.Abs(side) <= 0.01f)
                    side = Vector3.Dot(player.ClosestPoint(center) - center, right);
                Vector3 direction = side < 0f ? -right : right;
                if (!receiver.TryGetContactClearanceDistance(
                        _groundBodyColliders, direction, 0f, out _)) return;

                _attack.TryHit(player, direction,
                    moveDurationOverride: _contactMoveDuration,
                    contactBodies: _groundBodyColliders,
                    contactPadding: _contactPadding);
                return;
            }
        }

        private void OnValidate()
        {
            _contactMoveDuration = Mathf.Max(0f, _contactMoveDuration);
            _contactPadding = Mathf.Max(0f, _contactPadding);
        }
    }
}
