using alpha.ingame.item;
using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>조준된 적을 향해 좌우 포구에서 번갈아 투사체를 발사한다.</summary>
    [DefaultExecutionOrder(170)]
    [RequireComponent(typeof(CanonTargeting), typeof(SummonLifetime))]
    public sealed class CanonAttack : MonoBehaviour
    {
        [Header("Fire")]
        [SerializeField] private Transform[] _firePoints;
        [SerializeField, Min(0.01f)] private float _fireInterval = 0.3f;
        [SerializeField, Range(0f, 45f)] private float _aimTolerance = 12f; // 타겟팅 발사 시작 각도
        [SerializeField, Min(0f)] private float _damage = 8f;
        [SerializeField] private GunProjectile _projectilePrefab;
        [SerializeField, Min(0)] private int _initialProjectilePoolSize = 12;
        [SerializeField] private ImpactData _impactData;

        [Header("Effects")]
        [SerializeField] private GameObject _muzzleEffectPrefab;
        [SerializeField, Min(0)] private int _initialMuzzlePoolSize = 4;
        [SerializeField, Min(0.01f)] private float _muzzleEffectMaxLifetime = 1f;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _fireAudioClip;
        [SerializeField] private CanonAnimator _canonAnimator;

        private CanonTargeting _targeting;
        private SummonLifetime _lifetime;
        private GunProjectilePool _projectilePool;
        private ImpactPool _impactPool;
        private MuzzleEffectPool _muzzleEffectPool;
        private IAttackDamageResolver _damageResolver;
        private float _nextShotAt;
        private int _nextFirePoint;

        // 소환한 플레이어의 피해 규칙을 포탑 공격에도 적용한다.
        public void BindDamageResolver(IAttackDamageResolver damageResolver) =>
            _damageResolver = damageResolver;

        private void Awake()
        {
            _targeting = GetComponent<CanonTargeting>();
            _lifetime = GetComponent<SummonLifetime>();
            if (_projectilePrefab == null || _impactData == null ||
                _firePoints == null || _firePoints.Length == 0)
            {
                Debug.LogError($"{nameof(CanonAttack)}: 포구, 투사체, 적중 데이터를 지정하세요.", this);
                enabled = false;
                return;
            }

            foreach (Transform firePoint in _firePoints)
            {
                if (firePoint != null) continue;
                Debug.LogError($"{nameof(CanonAttack)}: 포구 참조가 없습니다.", this);
                enabled = false;
                return;
            }

            _projectilePool = new GunProjectilePool(_projectilePrefab,
                Mathf.Max(0, _initialProjectilePoolSize), name);
            _impactPool = new ImpactPool(_impactData, name);
            if (_muzzleEffectPrefab != null)
                _muzzleEffectPool = new MuzzleEffectPool(_muzzleEffectPrefab,
                    Mathf.Max(0, _initialMuzzlePoolSize), _muzzleEffectMaxLifetime, name);
        }

        private void LateUpdate()
        {
            // 타겟 회전이 끝난 뒤 조준 상태를 검사한다.
            if (_lifetime.IsExpired || !_targeting.isActiveAndEnabled || Time.time < _nextShotAt) return;

            Collider target = _targeting.TargetCollider;
            if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) return;

            Transform firePoint = _firePoints[_nextFirePoint];
            Vector3 targetPoint = target.bounds.center;
            Vector3 toTarget = targetPoint - firePoint.position;
            if (toTarget.sqrMagnitude < 0.0001f ||
                Vector3.Angle(firePoint.forward, toTarget) > _aimTolerance) return;

            // 발사 방향은 각 포구에서 선택한 콜라이더 중심으로 계산하고, 포구를 번갈아 사용한다.
            Vector3 direction = toTarget.normalized;
            _projectilePool.Rent().Launch(_projectilePool, _impactPool,
                transform, firePoint.position, direction, _damage, toTarget.magnitude + 1f,
                target, _damageResolver);
            _canonAnimator?.PlayShoot();
            _muzzleEffectPool?.Spawn(firePoint);
            if (_audioSource != null && _fireAudioClip != null)
                _audioSource.PlayOneShot(_fireAudioClip);

            _nextFirePoint = (_nextFirePoint + 1) % _firePoints.Length;
            _nextShotAt = Time.time + Mathf.Max(0.01f, _fireInterval);
        }

        private void OnDestroy()
        {
            _projectilePool?.Dispose();
            _impactPool?.Dispose();
            _muzzleEffectPool?.Dispose();
        }
    }
}
