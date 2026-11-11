using UnityEngine;

namespace alpha.ingame.item
{
    // 공격 주체가 확정한 피해와 치명타 판정을 대상에게 전달한다.
    public readonly struct AttackDamage
    {
        public readonly float Amount;
        public readonly bool IsCritical;
        public readonly GameObject Source;
        public readonly Vector3 HitPoint;

        public AttackDamage(float amount, bool isCritical, GameObject source, Vector3 hitPoint)
        {
            Amount = amount;
            IsCritical = isCritical;
            Source = source;
            HitPoint = hitPoint;
        }
    }

    // 투사체 충돌을 받는 대상이 구현할 피해 처리 계약이다.
    public interface IDamageable
    {
        // 실제 감소한 체력을 반환한다. 이미 죽은 대상은 0이다.
        float TakeDamage(in AttackDamage damage);
    }

    // 공격 객체는 플레이어의 치명타 규칙을 이 계약으로만 사용한다.
    public interface IAttackDamageResolver
    {
        float ApplyDamage(IDamageable target, float baseDamage, GameObject source, Vector3 hitPoint);
    }
}
