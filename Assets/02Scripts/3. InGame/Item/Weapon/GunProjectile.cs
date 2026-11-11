using UnityEngine;

namespace alpha.ingame.item
{
    /// <summary>투사체 이동과 충돌을 처리하고 사용 후 풀에 반환한다.</summary>
    public class GunProjectile : MonoBehaviour
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        [SerializeField, Min(0.01f)] private float _speed = 60f;
        [SerializeField, Min(0.01f)] private float _lifetime = 3f;
        [SerializeField] private SphereCollider _collisionCollider;
        [SerializeField] private LayerMask _triggerHitLayers = 1 << 9;

        private GunProjectilePool _pool;
        private ImpactPool _impactPool;
        private IAttackDamageResolver _damageResolver;
        private Transform _owner;
        private Collider _lockedTarget;
        private Transform _lockedTargetRoot;
        private bool _requiresLockedTarget;
        private Vector3 _direction;
        private float _damage;
        private float _remainingDistance;
        private float _expiresAt;

        public void Launch(GunProjectilePool pool, ImpactPool impactPool,
            Transform owner, Vector3 position, Vector3 direction, float damage, float maxDistance,
            Collider lockedTarget = null, IAttackDamageResolver damageResolver = null)
        {
            _pool = pool;
            _impactPool = impactPool;
            _damageResolver = damageResolver;
            _owner = owner;
            _lockedTarget = lockedTarget;
            _requiresLockedTarget = lockedTarget != null;
            _lockedTargetRoot = lockedTarget != null && lockedTarget.attachedRigidbody != null
                ? lockedTarget.attachedRigidbody.transform : null;
            _direction = direction.normalized;
            _damage = damage;
            _remainingDistance = maxDistance;
            _expiresAt = Time.time + Mathf.Max(0.01f, _lifetime);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(_direction));
            gameObject.SetActive(true);

            // 콜라이더는 실제 물리 충돌 대신 이동 경로 검사에 사용할 모양을 제공한다.
            if (_collisionCollider == null)
                _collisionCollider = GetComponentInChildren<SphereCollider>(true);
            if (_collisionCollider == null)
            {
                Debug.LogError($"{name}: 투사체에 SphereCollider가 필요합니다.", this);
                _pool.Return(this);
            }
        }

        private void Update()
        {
            if (_pool == null) return;
            if (_requiresLockedTarget && (_lockedTarget == null || !_lockedTarget.enabled ||
                !_lockedTarget.gameObject.activeInHierarchy))
            {
                _pool.Return(this);
                return;
            }
            if (Time.time >= _expiresAt || _remainingDistance <= 0f)
            {
                _pool.Return(this);
                return;
            }

            float distance = Mathf.Min(Mathf.Max(0.01f, _speed) * Time.deltaTime, _remainingDistance);
            Vector3 scale = _collisionCollider.transform.lossyScale;
            float radius = _collisionCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 center = _collisionCollider.transform.TransformPoint(_collisionCollider.center);
            int count = Physics.SphereCastNonAlloc(center, radius, _direction, Hits,
                distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            int nearest = -1;
            float nearestDistance = distance;
            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                if (collider == null || (_owner != null && collider.transform.IsChildOf(_owner))) continue;
                // 터렛이 고정한 보스 콜라이더 앞의 다른 부위는 관통하고 선택 부위에 맞춘다.
                if (_lockedTarget != null && _lockedTargetRoot != null &&
                    collider != _lockedTarget && collider.transform.IsChildOf(_lockedTargetRoot)) continue;
                if (collider.isTrigger &&
                    (_triggerHitLayers.value & (1 << collider.gameObject.layer)) == 0) continue;
                if (nearest >= 0 && Hits[i].distance >= nearestDistance) continue;
                nearest = i;
                nearestDistance = Hits[i].distance;
            }

            if (nearest >= 0)
            {
                RaycastHit hit = Hits[nearest];
                // 시작점에서 겹친 SphereCast는 접촉점 대신 (0,0,0)을 돌려줄 수 있다.
                Vector3 contactPoint = hit.distance <= 0f && hit.point == Vector3.zero
                    ? center : hit.point;
                _impactPool?.Spawn(hit, _owner, _damage, -_direction, contactPoint, _damageResolver);
                _pool.Return(this);
                return;
            }

            transform.position += _direction * distance;
            _remainingDistance -= distance;
        }
    }
}
