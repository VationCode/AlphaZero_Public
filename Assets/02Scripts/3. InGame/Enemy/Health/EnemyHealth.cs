using System;
using alpha.ingame.item;
using UnityEngine;

namespace alpha.ingame.enemy
{
    // 적에게 실제로 적용된 피해, 표시할 최종 공격 피해와 피격 상태다.
    public readonly struct EnemyDamageResult
    {
        public readonly float AppliedDamage;
        // 남은 체력보다 큰 결정타도 계산된 피해량 그대로 표시한다.
        public readonly float DisplayDamage;
        public readonly bool IsCritical;
        public readonly float CurrentHealth;
        public readonly float MaxHealth;
        public readonly Vector3 HitPoint;
        public readonly GameObject Source;

        public EnemyDamageResult(float appliedDamage, float displayDamage, bool isCritical,
            float currentHealth, float maxHealth, Vector3 hitPoint, GameObject source)
        {
            AppliedDamage = appliedDamage;
            DisplayDamage = displayDamage;
            IsCritical = isCritical;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            HitPoint = hitPoint;
            Source = source;
        }
    }

    /// <summary>적의 체력, 피격, 사망을 단독으로 관리한다.</summary>
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _maxHealth = 100f;

        public float MaxHealth => Mathf.Max(1f, _maxHealth);
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;

        public event Action<EnemyDamageResult> Damaged;
        public event Action Died;

        private void Awake() => CurrentHealth = MaxHealth;

        public float ApplyDamage(in AttackDamage damage)
        {
            if (IsDead || damage.Amount <= 0f) return 0f;

            float applied = Mathf.Min(CurrentHealth, damage.Amount);
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage.Amount);
            Damaged?.Invoke(new EnemyDamageResult(applied, damage.Amount, damage.IsCritical,
                CurrentHealth, MaxHealth, damage.HitPoint, damage.Source));
            if (IsDead) Died?.Invoke();
            return applied;
        }
    }
}
