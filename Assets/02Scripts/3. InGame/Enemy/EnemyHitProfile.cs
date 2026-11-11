using System;
using alpha.ingame.player;
using UnityEngine;
using UnityEngine.Serialization;

namespace alpha.ingame.enemy
{
    /// <summary>적 공격의 피해량·피격 종류와 플레이어 기본 밀림 거리에 더할 거리다.</summary>
    [Serializable]
    public struct EnemyHitProfile
    {
        [SerializeField, Min(0f)] private float _damage;
        [SerializeField] private EPlayerHitType _hitType;
        [Tooltip("플레이어의 넉백·넉다운 기본 이동 거리에 더한다. 0은 추가 거리 없음.")]
        [FormerlySerializedAs("_distanceMultiplier")]
        [SerializeField, Min(0f)] private float _additionalDistance;
        public EPlayerHitType HitType => _hitType;
        public EnemyHitProfile(float damage, EPlayerHitType hitType,
            float additionalDistance = 0f)
        {
            _damage = damage;
            _hitType = hitType;
            _additionalDistance = Mathf.Max(0f, additionalDistance);
        }

        public PlayerHitInfo CreateHit(Transform source, Vector3 direction,
            float minimumDistance = 0f, float moveDurationOverride = 0f,
            Collider[] contactBodies = null, float contactPadding = 0f) =>
            new(Mathf.Max(0f, _damage), _hitType, source, direction,
                _additionalDistance, minimumDistance, moveDurationOverride,
                contactBodies, contactPadding);

        public void Validate()
        {
            _damage = Mathf.Max(0f, _damage);
            _additionalDistance = Mathf.Max(0f, _additionalDistance);
        }
    }

    // 공격 중심에서 플레이어 바깥쪽으로 향하는 수평 피격 방향을 계산한다.
    internal static class EnemyHitDirection
    {
        public static Vector3 FromContact(Collider target, Vector3 contactPoint,
            Vector3 fallbackOrigin)
        {
            Vector3 center = target.bounds.center;
            Vector3 direction = Vector3.ProjectOnPlane(center - fallbackOrigin, Vector3.up);
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.ProjectOnPlane(center - contactPoint, Vector3.up);
            return direction;
        }
    }
}
