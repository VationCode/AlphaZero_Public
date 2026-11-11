using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>스킬 1의 무기 강화 시간과 배율을 설정한다.</summary>
    [CreateAssetMenu(fileName = "WeaponBuff", menuName = "AlphaZero/Skill/Weapon Buff")]
    public sealed class WeaponBuffSkillData : SkillData
    {
        [SerializeField, Min(0.1f)] private float _duration = 8f;
        [SerializeField, Min(1f)] private float _fireRateMultiplier = 1.5f;
        [SerializeField, Min(1f)] private float _damageMultiplier = 1.5f;
        [SerializeField, Min(1f)] private float _magazineCapacityMultiplier = 2f;

        public float Duration => Mathf.Max(0.1f, _duration);
        public float FireRateMultiplier => Mathf.Max(1f, _fireRateMultiplier);
        public float DamageMultiplier => Mathf.Max(1f, _damageMultiplier);
        public float MagazineCapacityMultiplier => Mathf.Max(1f, _magazineCapacityMultiplier);

        protected override void OnValidate()
        {
            base.OnValidate();
            _duration = Mathf.Max(0.1f, _duration);
            _fireRateMultiplier = Mathf.Max(1f, _fireRateMultiplier);
            _damageMultiplier = Mathf.Max(1f, _damageMultiplier);
            _magazineCapacityMultiplier = Mathf.Max(1f, _magazineCapacityMultiplier);
        }
    }
}
