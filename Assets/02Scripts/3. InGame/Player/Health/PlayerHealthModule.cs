using System;
using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>Player의 체력 증감과 사망 상태만 소유한다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealthModule : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _maxHealth = 100f;

        public float MaxHealth => Mathf.Max(1f, _maxHealth);
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        private void Awake() => CurrentHealth = MaxHealth;

        public bool TryApplyDamage(float amount)
        {
            if (IsDead || amount <= 0f) return false;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            if (IsDead) Died?.Invoke();
            return true;
        }

        public void RestoreHealth(float amount)
        {
            if (IsDead || amount <= 0f) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        private void OnValidate() => _maxHealth = Mathf.Max(1f, _maxHealth);
    }
}
