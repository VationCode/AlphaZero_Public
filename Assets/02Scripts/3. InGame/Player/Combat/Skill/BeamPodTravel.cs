using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>개별 포드가 타겟 콜라이더 주변의 접근 위치로 이동하거나 대형으로 복귀한다.</summary>
    [DefaultExecutionOrder(155)]
    [RequireComponent(typeof(SummonLifetime), typeof(RandomPointMove))]
    public sealed class BeamPodTravel : MonoBehaviour
    {
        [Header("Travel")]
        [Tooltip("타겟에 처음 접근하거나 대형으로 복귀할 때의 속도")]
        [SerializeField, Min(0.1f)] private float _approachSpeed = 15f;
        [Tooltip("공격 위치에 한 번 도착한 뒤 움직이는 타겟을 따라갈 때의 속도")]
        [SerializeField, Min(0.1f)] private float _attackFollowSpeed = 40f;
        [SerializeField, Min(0.1f)] private float _turnSpeed = 360f;
        [SerializeField, Min(0.1f)] private float _maxPlayerDistance = 30f;

        [Header("Target Approach")]
        [Tooltip("타겟 콜라이더 표면에서 포드가 유지할 기본 거리")]
        [SerializeField, Min(0f)] private float _targetApproachDistance = 2f;
        [Tooltip("타겟 콜라이더의 가로 크기를 접근 거리에 반영하는 비율")]
        [SerializeField, Min(0f)] private float _colliderSizeDistanceFactor = 0.35f;
        [SerializeField, Min(1f)] private float _targetRandomMultiplier = 1.25f;
        [SerializeField, Min(0.01f)] private float _attackPositionTolerance = 0.2f;

        private RandomPointMove _randomMove;
        private SummonLifetime _lifetime;
        private Transform _player;
        private Collider _target;
        private Vector3 _formationLocalPosition;
        private Quaternion _formationLocalRotation;
        private Vector3 _baseWorldPosition;
        private bool _isActive;
        private bool _isApproaching;
        private bool _isReturning;
        private bool _isAtAttackPosition;
        private bool _hasReachedAttackPosition;

        // 공격을 끝내고 추적을 재개할지 그룹이 각 포드의 실제 위치로 판단한다.
        public bool IsBeyondPlayerLimit => _isActive && _player != null &&
            (transform.position - _player.position).sqrMagnitude >
            _maxPlayerDistance * _maxPlayerDistance;
        public bool IsAtAttackPosition => _isApproaching && _isAtAttackPosition;

        private void Awake()
        {
            _randomMove = GetComponent<RandomPointMove>();
            _lifetime = GetComponent<SummonLifetime>();
        }

        public void Begin(Transform player, Vector3 formationLocalPosition)
        {
            _player = player;
            _formationLocalPosition = formationLocalPosition;
            _formationLocalRotation = transform.localRotation;
            _baseWorldPosition = transform.parent.TransformPoint(formationLocalPosition);
            _target = null;
            _isActive = player != null;
            _isApproaching = false;
            _isReturning = false;
            _isAtAttackPosition = false;
            _hasReachedAttackPosition = false;
            _randomMove.SetRadiusMultiplier(1f);
            _randomMove.SetOrigin(formationLocalPosition);
        }

        public void MoveToTarget(Collider target)
        {
            if (!_isActive || target == null) return;
            if (_target != target)
            {
                _isAtAttackPosition = false;
                _hasReachedAttackPosition = false;
            }
            _target = target;
            _isApproaching = true;
            _isReturning = false;
            _randomMove.SetRadiusMultiplier(_targetRandomMultiplier);
        }

        public void ReturnToFormation()
        {
            _target = null;
            _isReturning |= _isApproaching;
            _isApproaching = false;
            _isAtAttackPosition = false;
            _hasReachedAttackPosition = false;
            _randomMove.SetRadiusMultiplier(1f);
        }

        private void LateUpdate()
        {
            if (!_isActive || _lifetime.IsExpired || _player == null) return;
            Transform group = transform.parent;
            if (group == null) return;

            // 그룹 추적이 멈춘 공격 중에도 포드의 기준 위치는 월드 좌표로 유지한다.
            Vector3 current = _baseWorldPosition;
            Vector3 destination = group.TransformPoint(_formationLocalPosition);
            Vector3 scale = group.lossyScale;
            float randomRadius = _randomMove.MaxOffsetRadius *
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            if (_isApproaching && _target != null && _target.enabled &&
                _target.gameObject.activeInHierarchy)
            {
                // 콜라이더 표면을 기준으로 하되 큰 적일수록 조금 더 떨어진 위치를 목표로 한다.
                Vector3 targetPoint = _target.ClosestPoint(current);
                float horizontalSize = Mathf.Max(_target.bounds.extents.x, _target.bounds.extents.z);
                float approachDistance = Mathf.Max(randomRadius + 0.05f,
                    _targetApproachDistance + horizontalSize * _colliderSizeDistanceFactor);
                Vector3 fromTarget = current - targetPoint;
                float surfaceDistance = fromTarget.magnitude;
                if (Mathf.Abs(surfaceDistance - approachDistance) > 0.05f)
                {
                    // 이미 가까운 경우에도 목표 거리까지 벌려 공격 위치를 잡는다.
                    Vector3 away = surfaceDistance > 0.0001f
                        ? fromTarget / surfaceDistance : current - _target.bounds.center;
                    if (away.sqrMagnitude < 0.0001f)
                        away = _player.position - _target.bounds.center;
                    if (away.sqrMagnitude < 0.0001f) away = group.forward;
                    destination = targetPoint + away.normalized * approachDistance;
                }
                else destination = current;
            }
            else if (_isApproaching) ReturnToFormation();

            // 첫 도착 이후에는 타겟이 잠시 멀어져도 빠른 추적 속도를 유지한다.
            float travelSpeed = _isApproaching && _hasReachedAttackPosition
                ? _attackFollowSpeed : _approachSpeed;
            _baseWorldPosition = _isApproaching || _isReturning
                ? Vector3.MoveTowards(current, destination, travelSpeed * Time.deltaTime)
                : destination;
            // 기준 위치가 목표 접근 위치에 도착해야 빔을 켤 수 있다.
            float tolerance = _isAtAttackPosition
                ? _attackPositionTolerance * 1.5f : _attackPositionTolerance;
            _isAtAttackPosition = _isApproaching &&
                (_baseWorldPosition - destination).sqrMagnitude <= tolerance * tolerance;
            if (_isAtAttackPosition) _hasReachedAttackPosition = true;
            if (_isReturning && (_baseWorldPosition - destination).sqrMagnitude < 0.0004f)
                _isReturning = false;
            _randomMove.SetOrigin(group.InverseTransformPoint(_baseWorldPosition));

            // 공격 중에는 각 포드가 자신의 타겟을 바라보고, 복귀하면 대형의 원래 자세를 따른다.
            if (_isApproaching && _target != null)
            {
                Vector3 direction = _target.bounds.center - transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
                        _turnSpeed * Time.deltaTime);
                }
            }
            else
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation,
                    _formationLocalRotation, _turnSpeed * Time.deltaTime);
        }

        private void OnDisable()
        {
            _isActive = false;
            _target = null;
            _isApproaching = false;
            _isReturning = false;
            _isAtAttackPosition = false;
            _hasReachedAttackPosition = false;
        }
    }
}
