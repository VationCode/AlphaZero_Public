using System;
using alpha.ingame.player;
using UnityEngine;
using UnityEngine.Serialization;

namespace alpha.ingame.item
{
    public enum EGunFireMode
    {
        Single = 0,
        Automatic = 1,
        Charge = 2,
        Beam = 3
    }

    public enum EGunShotMode
    {
        Projectile = 0,
        Hitscan = 1
    }

    /// <summary>
    /// 입력 방식과 탄환 판정 방식을 선택한다. 단발·연사·차징은 투사체/히트스캔을 사용하고,
    /// 빔은 공격을 누르는 동안 FirePos에서 히트스캔 피해를 반복 적용한다.
    /// </summary>
    public class GunWeapon : EquippableWeapon
    {
        private const string ReloadEndEventKey = "End";

        [Serializable]
        private sealed class ReloadAudioEvent
        {
            [SerializeField] private string _key;
            [SerializeField] private AudioClip _clip;

            public string Key => _key;
            public AudioClip Clip => _clip;
        }

        [Serializable]
        private sealed class ReloadEffectEvent
        {
            [SerializeField] private string _key;
            [SerializeField] private Transform _effect;

            public string Key => _key;
            public Transform Effect => _effect;
        }

        [SerializeField] private Transform _firePoint;
        [SerializeField] private Transform _muzzleEffectPoint;
        [SerializeField, Min(0.01f)] private float _muzzleEffectMaxLifetime = 1f;
        [SerializeField] private EGunFireMode _fireMode = EGunFireMode.Single;
        [SerializeField] private EGunShotMode _shotMode = EGunShotMode.Projectile;
        [SerializeField, Min(0.01f)] private float _fireInterval = 0.2f;
        [SerializeField, Min(0.01f)] private float _minimumFireInterval = 0.1f;
        [Header("Camera Recoil")]
        // 화면 기준 X/Y는 회전 각도(도), Z는 앞뒤 이동 거리(유닛)다.
        [SerializeField, Tooltip("X: 좌우 각도(도), Y: 위아래 각도(도), Z: 앞뒤 거리(유닛). 부호가 방향입니다.")]
        private Vector3 _cameraRecoilDirection;
        // X/Y 회전 각도 1도당 상승·복구에 걸리는 시간이다.
        [SerializeField, Min(0f)] private float _cameraRecoilKickTimePerDegree = 0.006f;
        [SerializeField, Min(0f)] private float _cameraRecoilRecoveryTimePerDegree = 0.024f;
        [Header("Ammo")]
        [SerializeField, Min(1)] private int _magazineCapacity = 30;
        [FormerlySerializedAs("_reloadDuration")]
        [SerializeField, Min(0f)] private float _reloadFallbackDuration = 1.5f;
        [Header("Reload Animation")]
        [SerializeField, Min(0f)] private float _reloadEndFadeDuration = 0.25f;    //애니메이션 끝나는 지점 End이후 weight페이드 시간
        [Header("Reload Effect")]
        [SerializeField] private Transform _reloadEffectPoint;
        [Header("Reload Animation Events")]
        [SerializeField] private ReloadAudioEvent[] _reloadAudioEvents = Array.Empty<ReloadAudioEvent>();
        [SerializeField] private ReloadEffectEvent[] _reloadEffectEvents = Array.Empty<ReloadEffectEvent>();
        [SerializeField] private GunProjectile _projectilePrefab;
        [SerializeField, Min(0)] private int _initialPoolSize = 12;
        [SerializeField] private ImpactData _impactData;
        [SerializeField, Min(0f)] private float _damage = 10f;
        [SerializeField, Min(0.01f)] private float _aimDistance = 200f;
        [SerializeField] private LayerMask _triggerHitLayers = 1 << 9;
        [SerializeField, Min(0.01f)] private float _chargeDuration = 1.5f;
        [SerializeField, Min(0f)] private float _minimumChargeTime = 0.2f;
        [SerializeField, Min(1f)] private float _fullChargeDamageMultiplier = 2f;
        [Header("Weapon Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _fireAudioClip;
        [SerializeField] private AudioClip _beamStartAudioClip;
        [SerializeField] private AudioClip _beamLoopAudioClip;
        [SerializeField] private AudioClip _beamEndAudioClip;

        private GunProjectilePool _projectilePool;
        private ImpactPool _impactPool;
        private PlacedWeaponEffects _muzzleEffects;
        private PlacedWeaponEffects _reloadEffects;
        private float _nextShotTime;
        private float _reloadEndsAt;
        private int _currentAmmo;
        private int _magazineBonus;
        private int _remainingBonusAmmo;
        private bool _isReloading;
        private bool _reloadUsesAnimation;
        private bool _reloadCompletionStarted;
        private float _chargeStartedAt;
        private bool _isCharging;
        private bool _beamActive;
        private bool _beamLoopStarted;
        private float _beamLoopAt;
        private BondageChainEffectLifetime _skillEffect;
        private GameObject _skillEffectGroup;

        public Transform FirePoint => _firePoint;
        public Vector3 CameraRecoilDirection => _cameraRecoilDirection;
        private float CameraRecoilAngle => new Vector2(_cameraRecoilDirection.x, _cameraRecoilDirection.y).magnitude;
        public float CameraRecoilKickDuration =>
            Mathf.Max(0.01f, CameraRecoilAngle * _cameraRecoilKickTimePerDegree);
        public float CameraRecoilRecoveryDuration =>
            Mathf.Max(0.01f, CameraRecoilAngle * _cameraRecoilRecoveryTimePerDegree);
        public Transform MuzzleEffectPoint => _muzzleEffectPoint != null ? _muzzleEffectPoint : _firePoint;
        public int CurrentAmmo => _currentAmmo;
        public int MagazineCapacity => Mathf.Max(1, _magazineCapacity) + _magazineBonus;
        public bool CanReload => IsEquipped && !_isReloading && _currentAmmo < MagazineCapacity;
        public bool IsReloading => _isReloading;
        public event Action<int, int> AmmoChanged;

        public override void Initialize(PlayerRoot owner)
        {
            base.Initialize(owner);
            _magazineBonus = 0;
            _remainingBonusAmmo = 0;
            _currentAmmo = MagazineCapacity;
            _isReloading = false;
            _reloadCompletionStarted = false;
            // 프리팹에 배치한 이펙트만 재생 대상으로 등록한다.
            _muzzleEffects = new PlacedWeaponEffects(MuzzleEffectPoint, _muzzleEffectMaxLifetime);
            _reloadEffects = new PlacedWeaponEffects(_reloadEffectPoint);
            _skillEffect = GetComponentInChildren<BondageChainEffectLifetime>(true);
            _skillEffectGroup = _skillEffect != null && _skillEffect.transform.parent != transform
                ? _skillEffect.transform.parent.gameObject : null;
            SetSkillEffect(false);
            // 무기를 만들 때 선택한 판정 방식에 필요한 풀을 준비한다.
            EnsurePools();
        }

        private void Update()
        {
            _muzzleEffects?.Tick();
            _reloadEffects?.Tick();
            if (!_isReloading) return;

            if (!_reloadUsesAnimation)
            {
                if (Time.time < _reloadEndsAt) return;
                BeginReloadCompletion();
                FinishReload();
                return;
            }

            AnimationView animationView = Owner != null ? Owner.AnimationView : null;
            if (!_reloadCompletionStarted)
            {
                // End 이벤트가 빠진 클립도 재장전 상태에 갇히지 않게 한다.
                if (animationView != null && !animationView.IsReloadFinished()) return;
                Debug.LogWarning($"{nameof(GunWeapon)}: Reload End 이벤트 없이 애니메이션이 끝났습니다.", this);
                BeginReloadCompletion();
            }

            // UpperArm 레이어의 페이드가 끝날 때까지 공격을 잠근다.
            if (animationView == null || !animationView.IsReloadActive)
                FinishReload();
        }

        private void BeginReloadCompletion()
        {
            if (!_isReloading || _reloadCompletionStarted) return;

            _reloadCompletionStarted = true;
            if (_reloadUsesAnimation)
                Owner?.AnimationView?.StopReload(_reloadEndFadeDuration);
            StopReloadEffect();
            _currentAmmo = MagazineCapacity;
            _remainingBonusAmmo = _magazineBonus;
            AmmoChanged?.Invoke(_currentAmmo, MagazineCapacity);
        }

        private void FinishReload()
        {
            _isReloading = false;
            _reloadUsesAnimation = false;
            _reloadCompletionStarted = false;
        }

        public bool TryReload()
        {
            if (!CanReload) return false;

            // 애니메이션이 있으면 End 이벤트를 기다리고, 없으면 설정 시간을 사용한다.
            CancelAttack();
            _isReloading = true;
            _reloadCompletionStarted = false;
            _reloadEndsAt = Time.time + Mathf.Max(0f, _reloadFallbackDuration);
            AnimationView animationView = Owner != null ? Owner.AnimationView : null;
            _reloadUsesAnimation = animationView != null && animationView.PlayReload();
            if (!_reloadUsesAnimation)
            {
                // 애니메이션이 없으면 첫 번째 유효한 사운드와 이펙트를 즉시 재생한다.
                PlayFallbackReloadEvents();
            }
            return true;
        }

        public void CancelReload()
        {
            if (!_isReloading) return;

            Owner?.AnimationView?.StopReload();
            StopReloadEffect();
            FinishReload();
        }

        public void HandleReloadAnimationEvent(string eventKey)
        {
            if (!IsEquipped || !_isReloading || !_reloadUsesAnimation ||
                _reloadCompletionStarted ||
                string.IsNullOrEmpty(eventKey)) return;

            if (string.Equals(eventKey, ReloadEndEventKey, StringComparison.Ordinal))
            {
                // End에서 탄약을 채우고, 재장전 상태는 페이드 종료까지 유지한다.
                BeginReloadCompletion();
                return;
            }

            // 같은 키에 여러 항목이 있어도 모두 재생한다.
            if (_reloadAudioEvents != null)
                foreach (ReloadAudioEvent cue in _reloadAudioEvents)
                    if (cue != null && string.Equals(eventKey, cue.Key, StringComparison.Ordinal))
                        PlayReloadAudio(cue.Clip);

            if (_reloadEffectEvents != null)
                foreach (ReloadEffectEvent cue in _reloadEffectEvents)
                    if (cue != null && string.Equals(eventKey, cue.Key, StringComparison.Ordinal))
                        _reloadEffects?.Play(cue.Effect);
        }

        // 전투 상태가 공격을 시작할 때 호출한다. 발사 방식은 무기가 결정한다.
        public void BeginAttack()
        {
            if (!IsEquipped || Owner == null || _isReloading) return;

            switch (_fireMode)
            {
                case EGunFireMode.Single:
                case EGunFireMode.Automatic:
                    TryFire(0f);
                    break;
                case EGunFireMode.Charge:
                    _isCharging = true;
                    _chargeStartedAt = Time.time;
                    break;
                case EGunFireMode.Beam:
                    BeginBeam();
                    break;
            }
        }

        // 연사는 탄환을 발사하고 빔은 일정 간격으로 적중 판정을 갱신한다.
        public void UpdateAttack()
        {
            if (!IsEquipped || Owner == null || _isReloading) return;
            if (_fireMode == EGunFireMode.Automatic) TryFire(0f);
            if (_fireMode != EGunFireMode.Beam) return;

            if (!_beamActive) BeginBeam();
            else
            {
                if (!_beamLoopStarted && Time.time >= _beamLoopAt) StartBeamLoop();
                if (Time.time >= _nextShotTime) TickBeam();
            }
        }

        public void EndAttack()
        {
            if (_isCharging && IsEquipped && Owner != null)
            {
                float heldTime = Time.time - _chargeStartedAt;
                if (heldTime >= _minimumChargeTime)
                    TryFire(Mathf.Clamp01(heldTime / Mathf.Max(0.01f, _chargeDuration)));
            }
            _isCharging = false;
            StopBeam(true);
        }

        public void CancelAttack()
        {
            _isCharging = false;
            StopBeam(false);
        }

        private void OnDisable()
        {
            CancelAttack();
            CancelReload();
            _muzzleEffects?.StopAll();
        }

        public void SetSkillEffect(bool active, float duration = 0f)
        {
            if (_skillEffectGroup == null || _skillEffect == null) return;
            if (!active)
            {
                _skillEffect.gameObject.SetActive(false);
                _skillEffectGroup.SetActive(false);
                return;
            }

            _skillEffectGroup.SetActive(true);
            _skillEffect.gameObject.SetActive(true);
            _skillEffect.Activate(duration);
        }

        public void ApplyMagazineBuff(float multiplier)
        {
            ClearMagazineBuff();
            int baseCapacity = Mathf.Max(1, _magazineCapacity);
            int increasedCapacity = Mathf.RoundToInt(baseCapacity * Mathf.Max(1f, multiplier));
            _magazineBonus = Mathf.Max(0, increasedCapacity - baseCapacity);
            _remainingBonusAmmo = _magazineBonus;
            _currentAmmo += _magazineBonus;
            AmmoChanged?.Invoke(_currentAmmo, MagazineCapacity);
        }

        public void ClearMagazineBuff()
        {
            if (_magazineBonus == 0) return;
            // 보너스 탄약부터 소비했으므로 남은 보너스만 제거한다.
            _currentAmmo = Mathf.Max(0, _currentAmmo - _remainingBonusAmmo);
            _magazineBonus = 0;
            _remainingBonusAmmo = 0;
            AmmoChanged?.Invoke(_currentAmmo, MagazineCapacity);
        }

        private float EffectiveFireInterval()
        {
            float baseInterval = Mathf.Max(0.01f, _fireInterval);
            float multiplier = Owner?.SkillModule?.FireRateMultiplier ?? 1f;
            // 총마다 지정한 최소 간격보다 빠르게 발사하지 않는다.
            return Mathf.Max(Mathf.Min(baseInterval, Mathf.Max(0.01f, _minimumFireInterval)),
                baseInterval / Mathf.Max(1f, multiplier));
        }

        private float EffectiveDamage() =>
            _damage * (Owner?.SkillModule?.DamageMultiplier ?? 1f);

        private void PlayFallbackReloadEvents()
        {
            if (_reloadAudioEvents != null)
                foreach (ReloadAudioEvent cue in _reloadAudioEvents)
                    if (cue?.Clip != null)
                    {
                        PlayReloadAudio(cue.Clip);
                        break;
                    }

            if (_reloadEffectEvents != null)
                foreach (ReloadEffectEvent cue in _reloadEffectEvents)
                    if (cue?.Effect != null)
                    {
                        _reloadEffects?.Play(cue.Effect);
                        break;
                    }
        }

        private void PlayReloadAudio(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }

        private void StopReloadEffect()
        {
            _reloadEffects?.StopAll();
        }

        private void OnDestroy()
        {
            _projectilePool?.Dispose();
            _impactPool?.Dispose();
        }

        private bool TryFire(float charge)
        {
            if (_isReloading || _currentAmmo <= 0 || _firePoint == null || Time.time < _nextShotTime) return false;
            if (_shotMode == EGunShotMode.Projectile && _projectilePrefab == null) return false;
            _nextShotTime = Time.time + EffectiveFireInterval();
            EnsurePools();

            Vector3 direction = GetAimDirection();
            float baseDamage = EffectiveDamage();
            float damage = Mathf.Lerp(baseDamage, baseDamage * _fullChargeDamageMultiplier, charge);
            if (_shotMode == EGunShotMode.Hitscan)
            {
                // 총구에서 다시 검사해 카메라와 총구 사이의 장애물을 반영한다.
                if (TryGetNearestHit(new Ray(_firePoint.position, direction), _aimDistance, out RaycastHit hit))
                    _impactPool?.Spawn(hit, Owner.transform, damage, -direction,
                        damageResolver: Owner.CombatModule);
            }
            else
            {
                _projectilePool.Rent().Launch(_projectilePool, _impactPool,
                    Owner.transform, _firePoint.position, direction,
                    damage, Mathf.Max(0.01f, _aimDistance),
                    damageResolver: Owner.CombatModule);
            }

            ConsumeRound();

            // 입력 시점이 아닌 실제 발사 시점에 무기별 총구 효과를 재생한다.
            _muzzleEffects?.PlayNext();
            if (_audioSource != null && _fireAudioClip != null)
                _audioSource.PlayOneShot(_fireAudioClip);
            // 적중 여부와 관계없이 실제 발사가 일어나면 전투 자세 시간을 갱신한다.
            Owner.CombatModule?.RegisterSuccessfulAttack(this, notifyShotFired: true);
            return true;
        }

        private void BeginBeam()
        {
            if (_beamActive || _isReloading || _currentAmmo <= 0 ||
                _firePoint == null || Time.time < _nextShotTime) return;
            _beamActive = true;
            _beamLoopStarted = false;
            _beamLoopAt = Time.time;

            if (_audioSource != null && _beamStartAudioClip != null)
            {
                _audioSource.Stop();
                _audioSource.loop = false;
                _audioSource.clip = _beamStartAudioClip;
                _audioSource.Play();
                _beamLoopAt += _beamStartAudioClip.length;
            }
            else StartBeamLoop();

            TickBeam();
        }

        private void StartBeamLoop()
        {
            _beamLoopStarted = true;
            if (_audioSource == null || _beamLoopAudioClip == null) return;
            _audioSource.Stop();
            _audioSource.clip = _beamLoopAudioClip;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        private void TickBeam()
        {
            if (_currentAmmo <= 0)
            {
                StopBeam(true);
                return;
            }

            _nextShotTime = Time.time + EffectiveFireInterval();
            ConsumeRound();
            Vector3 direction = GetAimDirection();
            if (TryGetNearestHit(new Ray(_firePoint.position, direction), _aimDistance, out RaycastHit hit))
            {
                // 빔 틱은 직접 피해를 준다. ImpactData의 지속 피해와 중첩하지 않는다.
                foreach (MonoBehaviour behaviour in hit.collider.GetComponentsInParent<MonoBehaviour>())
                {
                    if (behaviour is not IDamageable damageable) continue;
                    if (Owner.CombatModule != null)
                        Owner.CombatModule.ApplyDamage(damageable, EffectiveDamage(),
                            Owner.gameObject, hit.point);
                    else
                        damageable.TakeDamage(new AttackDamage(EffectiveDamage(), false,
                            Owner.gameObject, hit.point));
                    break;
                }
            }
            Owner.CombatModule?.RegisterSuccessfulAttack(this);
            if (_currentAmmo == 0) StopBeam(true);
        }

        private void ConsumeRound()
        {
            if (_remainingBonusAmmo > 0) _remainingBonusAmmo--;
            _currentAmmo--;
            AmmoChanged?.Invoke(_currentAmmo, MagazineCapacity);
        }

        private void StopBeam(bool playEndSound)
        {
            if (!_beamActive) return;
            _beamActive = false;
            _beamLoopStarted = false;
            if (_audioSource == null) return;
            _audioSource.Stop();
            _audioSource.clip = null;
            _audioSource.loop = false;
            if (playEndSound && _beamEndAudioClip != null && _audioSource.isActiveAndEnabled)
                _audioSource.PlayOneShot(_beamEndAudioClip);
        }

        private void EnsurePools()
        {
            if (_fireMode != EGunFireMode.Beam && _shotMode == EGunShotMode.Projectile && _projectilePrefab != null)
                _projectilePool ??= new GunProjectilePool(_projectilePrefab, Mathf.Max(0, _initialPoolSize), name);
            if (_impactPool == null && _impactData != null)
                _impactPool = new ImpactPool(_impactData, name);
        }

        private Vector3 GetAimDirection()
        {
            Camera camera = Camera.main;
            if (camera == null) return _firePoint.forward;

            Ray ray = alpha.ingame.camera.CameraAim.CenterRay(camera);
            Vector3 target = TryGetNearestHit(ray, _aimDistance, out RaycastHit hit)
                ? hit.point : ray.GetPoint(_aimDistance);

            Vector3 direction = target - _firePoint.position;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : _firePoint.forward;
        }

        private bool TryGetNearestHit(Ray ray, float maxDistance, out RaycastHit nearestHit)
        {
            nearestHit = default;
            float nearestDistance = maxDistance;
            bool found = false;
            foreach (RaycastHit hit in Physics.RaycastAll(ray, maxDistance,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(Owner.transform) ||
                    (hit.collider.isTrigger &&
                     (_triggerHitLayers.value & (1 << hit.collider.gameObject.layer)) == 0) ||
                    hit.distance >= nearestDistance) continue;
                nearestHit = hit;
                nearestDistance = hit.distance;
                found = true;
            }
            return found;
        }
    }
}
