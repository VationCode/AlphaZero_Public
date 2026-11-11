using UnityEngine;

namespace alpha.ingame.player
{
    // Player가 받는 행동 반응의 강도 순서다. 피해량과 반응 설정은 별개다.
    public enum EPlayerHitType
    {
        Normal,
        Stagger,
        Knockback,
        Knockdown
    }

    // 공격은 피해 정보와 접촉 몸체를 전달하고, Player가 필요한 이격 거리를 계산한다.
    public readonly struct PlayerHitInfo
    {
        public readonly float Damage;
        public readonly EPlayerHitType Type;
        public readonly Transform Source;
        public readonly Vector3 Direction;
        public readonly float AdditionalDistance;
        public readonly float MinimumDistance;
        public readonly float MoveDurationOverride;
        public readonly Collider[] ContactBodies;
        public readonly float ContactPadding;
        public PlayerHitInfo(float damage, EPlayerHitType type, Transform source,
            Vector3 direction, float additionalDistance = 0f,
            float minimumDistance = 0f, float moveDurationOverride = 0f,
            Collider[] contactBodies = null, float contactPadding = 0f)
        {
            Damage = damage;
            Type = type;
            Source = source;
            Direction = direction;
            AdditionalDistance = Mathf.Max(0f, additionalDistance);
            MinimumDistance = Mathf.Max(0f, minimumDistance);
            MoveDurationOverride = Mathf.Max(0f, moveDurationOverride);
            ContactBodies = contactBodies;
            ContactPadding = Mathf.Max(0f, contactPadding);
        }
    }
}
