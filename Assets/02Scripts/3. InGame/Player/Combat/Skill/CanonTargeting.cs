using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>처음 선택한 적 콜라이더를 유지하며 포탑 상부와 포구를 회전시킨다.</summary>
    [DefaultExecutionOrder(160)]
    public sealed class CanonTargeting : MonoBehaviour
    {
        private readonly Collider[] _candidates = new Collider[32];
        private readonly RaycastHit[] _sightHits = new RaycastHit[32];

        [Header("Target Search")]
        [SerializeField] private LayerMask _targetLayers = 1 << 9;
        [SerializeField] private LayerMask _sightLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.1f)] private float _range = 12f;
        [SerializeField, Min(0.05f)] private float _scanInterval = 0.2f;

        [Header("Rotation Axes")]
        [SerializeField] private Transform _yawAxis;
        [SerializeField] private Transform[] _pitchAxes;
        [SerializeField, Min(0f)] private float _yawSpeed = 180f;
        [SerializeField, Min(0f)] private float _pitchSpeed = 90f;
        [SerializeField, Range(-89f, 89f)] private float _minPitch = -45f;
        [SerializeField, Range(-89f, 89f)] private float _maxPitch = 30f;

        private Quaternion _baseYawRotation;
        private Quaternion[] _basePitchRotations;
        private Collider _target;
        private bool _hasLineOfSight;
        private float _yawAngle;
        private float _pitchAngle;
        private float _nextScanAt;

        public Collider TargetCollider => _hasLineOfSight ? _target : null;
        public float Range => _range;

        private void Awake()
        {
            if (_yawAxis == null || _yawAxis.parent == null || _pitchAxes == null || _pitchAxes.Length == 0)
            {
                Debug.LogError($"{nameof(CanonTargeting)}: 회전 축 참조를 확인하세요.", this);
                enabled = false;
                return;
            }

            _baseYawRotation = _yawAxis.localRotation;
            _basePitchRotations = new Quaternion[_pitchAxes.Length];
            for (int i = 0; i < _pitchAxes.Length; i++)
            {
                if (_pitchAxes[i] == null)
                {
                    Debug.LogError($"{nameof(CanonTargeting)}: Pitch 축 {i} 참조가 없습니다.", this);
                    enabled = false;
                    return;
                }
                _basePitchRotations[i] = _pitchAxes[i].localRotation;
            }
        }

        private void OnEnable()
        {
            _target = null;
            _hasLineOfSight = false;
            _nextScanAt = 0f;
        }

        private void LateUpdate()
        {
            // 파괴 외형으로 전환되면 일반 포탑의 회전과 탐색을 멈춘다.
            if (_yawAxis == null || !_yawAxis.gameObject.activeInHierarchy)
            {
                _target = null;
                _hasLineOfSight = false;
                return;
            }

            if (_target != null && (!_target.enabled || !_target.gameObject.activeInHierarchy ||
                (_target.bounds.center - transform.position).sqrMagnitude > _range * _range))
            {
                _target = null;
                _hasLineOfSight = false;
            }

            // 처음 고른 콜라이더가 유효한 동안에는 다시 탐색하지 않는다.
            if (Time.time >= _nextScanAt)
            {
                if (_target == null)
                {
                    SearchTarget();
                    _hasLineOfSight = _target != null;
                }
                else _hasLineOfSight = HasLineOfSight(_target, true);
                _nextScanAt = Time.time + _scanInterval;
            }

            if (_target == null)
            {
                RotateToRest();
                return;
            }

            RotateTo(_target.bounds.center);
        }

        private void SearchTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, _range, _candidates,
                _targetLayers, QueryTriggerInteraction.Collide);
            Collider nearest = null;
            float nearestDistance = _range * _range;

            for (int i = 0; i < count; i++)
            {
                Collider candidate = _candidates[i];
                if (candidate == null || candidate.transform.IsChildOf(transform)) continue;

                float distance = (candidate.bounds.center - transform.position).sqrMagnitude;
                if (distance >= nearestDistance || !HasLineOfSight(candidate, false)) continue;
                nearest = candidate;
                nearestDistance = distance;
            }

            _target = nearest;
        }

        private bool HasLineOfSight(Collider candidate, bool allowSameEnemyColliders)
        {
            Vector3 origin = _yawAxis.position;
            Vector3 direction = candidate.bounds.center - origin;
            float distance = direction.magnitude;
            if (distance < 0.01f) return true;

            int count = Physics.RaycastNonAlloc(origin, direction / distance, _sightHits,
                distance, _sightLayers, QueryTriggerInteraction.Collide);
            Collider nearest = null;
            float nearestDistance = distance;
            Transform targetRoot = allowSameEnemyColliders && candidate.attachedRigidbody != null
                ? candidate.attachedRigidbody.transform : null;
            for (int i = 0; i < count; i++)
            {
                Collider hit = _sightHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) ||
                    (hit.isTrigger && (_targetLayers.value & (1 << hit.gameObject.layer)) == 0) ||
                    _sightHits[i].distance >= nearestDistance) continue;
                // 고정 타겟의 다른 피격 부위는 시야에서 제외하고 외부 장애물은 확인한다.
                if (targetRoot != null && hit != candidate && hit.transform.IsChildOf(targetRoot)) continue;
                nearest = hit;
                nearestDistance = _sightHits[i].distance;
            }

            return nearest == null || nearest == candidate;
        }

        private void RotateTo(Vector3 targetPoint)
        {
            Vector3 localDirection = _yawAxis.parent.InverseTransformDirection(targetPoint - _yawAxis.position);
            localDirection.y = 0f;
            if (localDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 baseForward = _baseYawRotation * Vector3.forward;
                baseForward.y = 0f;
                float desiredYaw = Vector3.SignedAngle(baseForward, localDirection, Vector3.up);
                _yawAngle = Mathf.MoveTowardsAngle(_yawAngle, desiredYaw, _yawSpeed * Time.deltaTime);
            }
            _yawAxis.localRotation = _baseYawRotation * Quaternion.Euler(0f, _yawAngle, 0f);

            Vector3 muzzleCenter = Vector3.zero;
            foreach (Transform pitchAxis in _pitchAxes) muzzleCenter += pitchAxis.position;
            muzzleCenter /= _pitchAxes.Length;

            Vector3 pitchDirection = _yawAxis.InverseTransformDirection(targetPoint - muzzleCenter);
            float horizontalDistance = new Vector2(pitchDirection.x, pitchDirection.z).magnitude;
            float desiredPitch = -Mathf.Atan2(pitchDirection.y, horizontalDistance) * Mathf.Rad2Deg;
            _pitchAngle = Mathf.MoveTowards(_pitchAngle,
                Mathf.Clamp(desiredPitch, _minPitch, _maxPitch), _pitchSpeed * Time.deltaTime);
            ApplyPitch();
        }

        private void RotateToRest()
        {
            _yawAngle = Mathf.MoveTowardsAngle(_yawAngle, 0f, _yawSpeed * Time.deltaTime);
            _pitchAngle = Mathf.MoveTowards(_pitchAngle, 0f, _pitchSpeed * Time.deltaTime);
            _yawAxis.localRotation = _baseYawRotation * Quaternion.Euler(0f, _yawAngle, 0f);
            ApplyPitch();
        }

        private void ApplyPitch()
        {
            for (int i = 0; i < _pitchAxes.Length; i++)
                _pitchAxes[i].localRotation = _basePitchRotations[i] * Quaternion.Euler(_pitchAngle, 0f, 0f);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _range);
        }
    }
}
