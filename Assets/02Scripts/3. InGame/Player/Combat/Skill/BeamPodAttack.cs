using alpha.ingame.item;
using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>FirePos에서 배정된 적을 확인하고 빔과 충돌 이펙트를 제어한다.</summary>
    [DefaultExecutionOrder(170)]
    [RequireComponent(typeof(SummonLifetime))]
    public sealed class BeamPodAttack : MonoBehaviour
    {
        [SerializeField] private Transform _firePos;
        [SerializeField] private BeamPodBeamVisual _beamVisual;
        [SerializeField] private Transform _hitEffect;
        [SerializeField] private AudioSource _attackAudioSource;
        [SerializeField, Min(0f)] private float _damage = 8f;
        [SerializeField, Min(0.01f)] private float _damageInterval = 0.5f;

        private readonly RaycastHit[] _hits = new RaycastHit[64];
        private BeamPodTargetModule _targetModule;
        private BeamPodTravel _travel;
        private SummonLifetime _lifetime;
        private Transform _player;
        private Transform _group;
        private LayerMask _hitLayers;
        private LayerMask _targetLayers;
        private IAttackDamageResolver _damageResolver;
        private IDamageable _damageable;
        private Collider _damageTarget;
        private float _nextDamageAt;
        private bool _isReady;
        private bool _isFiring;

        private void Awake()
        {
            _lifetime = GetComponent<SummonLifetime>();
            _travel = GetComponent<BeamPodTravel>();
            StopEffects();
        }

        public bool Begin(Transform player, LayerMask hitLayers, LayerMask targetLayers,
            IAttackDamageResolver damageResolver)
        {
            _targetModule = GetComponentInParent<BeamPodTargetModule>();
            if (player == null || damageResolver == null || _targetModule == null || _lifetime == null ||
                _travel == null || _firePos == null || _beamVisual == null ||
                !_beamVisual.IsConfigured || _hitEffect == null)
            {
                Debug.LogError($"{nameof(BeamPodAttack)}: FirePos, BeamVisual, HitEffect를 연결하세요.", this);
                return false;
            }

            _player = player;
            _group = transform.parent;
            _hitLayers = hitLayers;
            _targetLayers = targetLayers;
            _damageResolver = damageResolver;
            _isReady = true;
            StopEffects();
            return true;
        }

        private void LateUpdate()
        {
            if (!_isReady || _lifetime.IsExpired || !_targetModule.IsAttacking ||
                !_travel.IsAtAttackPosition)
            {
                StopEffects();
                return;
            }

            Collider target = _targetModule.GetTarget(_lifetime);
            if (target == null || !target.enabled || !target.gameObject.activeInHierarchy)
            {
                StopEffects();
                return;
            }

            Vector3 origin = _firePos.position;
            Vector3 toTarget = target.bounds.center - origin;
            float distance = toTarget.magnitude;
            if (distance < 0.01f)
            {
                StopEffects();
                return;
            }

            // 보스의 다른 자식 콜라이더보다 먼저 맞더라도 배정된 콜라이더의 충돌점을 사용한다.
            Vector3 direction = toTarget / distance;
            if (!target.Raycast(new Ray(origin, direction), out RaycastHit targetHit, distance + 0.1f))
            {
                StopEffects();
                return;
            }

            // 타겟에 도달하기 전에 다른 적이나 지형이 가로막는지만 확인한다.
            int count = Physics.RaycastNonAlloc(origin, direction, _hits,
                targetHit.distance, _hitLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = _hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(_group)) continue;
                if (hitCollider.isTrigger &&
                    (_targetLayers.value & (1 << hitCollider.gameObject.layer)) == 0) continue;
                if (hitCollider.transform.IsChildOf(_player) &&
                    (_targetLayers.value & (1 << hitCollider.gameObject.layer)) == 0) continue;
                if (IsPartOfAssignedEnemy(hitCollider, target)) continue;
                StopEffects();
                return;
            }

            // 반복되는 빔·Hit 파티클은 생성하지 않고 선택된 콜라이더의 충돌점만 갱신한다.
            _hitEffect.position = targetHit.point;
            _beamVisual.Show(origin, targetHit.point);
            if (!_hitEffect.gameObject.activeSelf) _hitEffect.gameObject.SetActive(true);
            if (_damageTarget != target)
            {
                _damageTarget = target;
                _damageable = FindDamageable(target);
                _nextDamageAt = 0f;
            }
            // 실제 피해를 적용하면 EnemyHealth.Damaged가 데미지 텍스트를 생성한다.
            if (_damageable != null && _damage > 0f && Time.time >= _nextDamageAt)
            {
                _damageResolver.ApplyDamage(_damageable, _damage, _player.gameObject, targetHit.point);
                _nextDamageAt = Time.time + Mathf.Max(0.01f, _damageInterval);
            }
            if (!_isFiring)
            {
                // 명중으로 빔이 처음 켜진 순간에만 재생한다.
                _isFiring = true;
                if (_attackAudioSource != null) _attackAudioSource.Play();
            }
        }

        private static IDamageable FindDamageable(Collider target)
        {
            foreach (MonoBehaviour behaviour in target.GetComponentsInParent<MonoBehaviour>())
                if (behaviour is IDamageable damageable) return damageable;
            return null;
        }

        private bool IsPartOfAssignedEnemy(Collider hit, Collider target)
        {
            if (hit == target) return true;
            Transform root = target.attachedRigidbody != null
                ? target.attachedRigidbody.transform : target.transform;
            while (root.parent != null && root.parent != _player &&
                   (_targetLayers.value & (1 << root.parent.gameObject.layer)) != 0)
                root = root.parent;
            return hit.transform.IsChildOf(root);
        }

        private void StopEffects()
        {
            _damageTarget = null;
            _damageable = null;
            _nextDamageAt = 0f;
            if (_isFiring)
            {
                _isFiring = false;
                if (_attackAudioSource != null) _attackAudioSource.Stop();
            }
            if (_hitEffect != null && _hitEffect.gameObject.activeSelf)
                _hitEffect.gameObject.SetActive(false);
            if (_beamVisual != null) _beamVisual.Hide();
        }

        private void OnDisable()
        {
            _isReady = false;
            StopEffects();
        }
    }
}
