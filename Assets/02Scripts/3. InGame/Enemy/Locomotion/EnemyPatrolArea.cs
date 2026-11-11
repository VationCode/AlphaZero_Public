using UnityEngine;
using UnityEngine.AI;

namespace alpha.ingame.enemy
{
    /// <summary>순찰 설정을 보관하고 전방의 도달 가능한 목적지를 고른다.</summary>
    public sealed class EnemyPatrolArea : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.01f), InspectorName("Patrol Speed")]
        private float _patrolSpeed = 3.5f;

        [Header("Patrol Area (Scene Placed Enemy)")]
        [SerializeField, Min(0.01f)] private float _areaSizeX = 20f;
        [SerializeField, Min(0.01f)] private float _areaSizeZ = 20f;
        [Header("Route")]
        [SerializeField, Range(1f, 360f)] private float _nextPointSearchAngle = 90f;
        [SerializeField, Min(0f)] private float _minPointDistance = 3f;
        [SerializeField, Min(0.01f)] private float _maxPointDistance = 8f;
        [SerializeField, Min(1)] private int _maxAttempts = 20;
        [Header("Arrival Wait")]
        [SerializeField, Min(0f)] private float _idleDuration = 1f;
        [SerializeField, Min(0f)] private float _idle2Duration = 2f;

        private Vector3 _center;
        private Vector3 _startPosition;
        private float _runtimeSizeX;
        private float _runtimeSizeZ;
        private EnemyRoot _enemy;
        private Collider _bodyCollider;
        private NavMeshAgent _agent;
        private bool _initialized;

        public float PatrolSpeed => Mathf.Max(0.01f, _patrolSpeed);
        public float IdleDuration => Mathf.Max(0f, _idleDuration);
        public float Idle2Duration => Mathf.Max(0f, _idle2Duration);
        public bool IsReady => _initialized && _bodyCollider != null &&
                               _agent != null && _agent.enabled && _agent.isOnNavMesh;

        private void Start()
        {
            _enemy = GetComponentInParent<EnemyRoot>(true);
            if (_enemy == null)
            {
                Debug.LogWarning($"{name}: 부모에 EnemyRoot가 필요합니다.", this);
                return;
            }

            _bodyCollider = _enemy.GetComponent<Collider>();
            _agent = _enemy.GetComponent<NavMeshAgent>();
            if (_bodyCollider == null || _agent == null)
            {
                Debug.LogWarning($"{_enemy.name}: 순찰에 루트 콜라이더와 NavMeshAgent가 필요합니다.", _enemy);
                return;
            }

            if (_agent.enabled && !_agent.isOnNavMesh &&
                NavMesh.SamplePosition(_enemy.transform.position, out NavMeshHit hit, 2f, _agent.areaMask))
            {
                _agent.enabled = false;
                _enemy.transform.position = hit.position;
                _agent.enabled = true;
            }

            if (!_agent.enabled || !_agent.isOnNavMesh)
            {
                Debug.LogWarning($"{_enemy.name}: NavMeshAgent가 NavMesh 위에 있지 않습니다.", _enemy);
                return;
            }

            _startPosition = _agent.nextPosition;
            _enemy.SetActivityOrigin(_startPosition);
            if (_enemy.TryGetPatrolArea(out Vector3 center, out Vector2 size))
            {
                _center = center;
                _runtimeSizeX = size.x;
                _runtimeSizeZ = size.y;
            }
            else
            {
                // 직접 배치한 적은 최초 위치와 이 컴포넌트의 기본 범위를 사용한다.
                _center = _startPosition;
                _runtimeSizeX = _areaSizeX;
                _runtimeSizeZ = _areaSizeZ;
            }
            _initialized = true;
        }

        public bool TryGetRandomDestination(out Vector3 destination)
        {
            destination = default;
            if (!IsReady) return false;

            Vector3 forward = _enemy.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            // 최소 이동 거리와 전방 각도 안에서 다음 순찰 지점을 고른다.
            float minimumDistance = Mathf.Max(_minPointDistance, 0.01f);
            float maximumDistance = Mathf.Max(minimumDistance, _maxPointDistance);

            for (int attempt = 0; attempt < _maxAttempts; attempt++)
            {
                float angle = Random.Range(-_nextPointSearchAngle * 0.5f, _nextPointSearchAngle * 0.5f);
                float distance = Random.Range(minimumDistance, maximumDistance);
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                Vector3 candidate = _agent.nextPosition + direction * distance;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, _agent.areaMask))
                    continue;

                Vector3 offset = hit.position - _agent.nextPosition;
                offset.y = 0f;
                if (offset.sqrMagnitude < minimumDistance * minimumDistance ||
                    offset.sqrMagnitude > maximumDistance * maximumDistance ||
                    Vector3.Angle(forward, offset) > _nextPointSearchAngle * 0.5f)
                    continue;
                if (!IsBodyInsideArea(hit.position) || !HasCompletePath(hit.position))
                    continue;

                destination = hit.position;
                return true;
            }

            return false;
        }

        public bool TryGetStartDestination(out Vector3 destination)
        {
            destination = _startPosition;
            return IsReady && IsBodyInsideArea(destination) && HasCompletePath(destination);
        }

        private bool HasCompletePath(Vector3 destination)
        {
            NavMeshPath path = new NavMeshPath();
            return _agent.CalculatePath(destination, path) &&
                   path.status == NavMeshPathStatus.PathComplete;
        }

        private bool IsBodyInsideArea(Vector3 destination)
        {
            Bounds bounds = _bodyCollider.bounds;
            bounds.center += destination - _enemy.transform.position;
            return bounds.min.x >= _center.x - _runtimeSizeX * 0.5f &&
                   bounds.max.x <= _center.x + _runtimeSizeX * 0.5f &&
                   bounds.min.z >= _center.z - _runtimeSizeZ * 0.5f &&
                   bounds.max.z <= _center.z + _runtimeSizeZ * 0.5f;
        }

        private void OnDrawGizmosSelected()
        {
            EnemyRoot enemy = GetComponentInParent<EnemyRoot>(true);
            if (enemy == null) return;

            Vector3 center = Application.isPlaying && _initialized ? _center : enemy.transform.position;
            float sizeX = Application.isPlaying && _initialized ? _runtimeSizeX : _areaSizeX;
            float sizeZ = Application.isPlaying && _initialized ? _runtimeSizeZ : _areaSizeZ;
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(center, new Vector3(sizeX, 0.1f, sizeZ));
        }

        private void OnValidate()
        {
            _patrolSpeed = Mathf.Max(0.01f, _patrolSpeed);
            _areaSizeX = Mathf.Max(0.01f, _areaSizeX);
            _areaSizeZ = Mathf.Max(0.01f, _areaSizeZ);
            _nextPointSearchAngle = Mathf.Clamp(_nextPointSearchAngle, 1f, 360f);
            _minPointDistance = Mathf.Max(0f, _minPointDistance);
            _maxPointDistance = Mathf.Max(_minPointDistance, 0.01f, _maxPointDistance);
            _maxAttempts = Mathf.Max(1, _maxAttempts);
        }
    }
}
