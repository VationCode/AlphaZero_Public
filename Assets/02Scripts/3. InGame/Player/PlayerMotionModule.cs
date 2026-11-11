using UnityEngine;

namespace alpha.ingame.player
{
    // 외부 인력은 Player의 대표 진입점으로 전달한다.
    public interface IPlayerAttractionReceiver
    {
        void SetAttraction(Transform source, Vector3 velocity);
        void ClearAttraction(Transform source);
    }

    /// <summary>의도적 이동과 피격 이동이 공유하는 몸체 이동·접지·접촉 해소를 실행한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotionModule : MonoBehaviour
    {
        [SerializeField] private LayerMask _groundLayers = (1 << 0) | (1 << 6);
        [SerializeField, Min(0f)] private float _groundProbeDistance = 0.2f;
        [SerializeField, Min(0f)] private float _landingDistance = 0.06f;
        [SerializeField] private float _gravity = -20f;

        private readonly RaycastHit[] _groundHits = new RaycastHit[8];
        private CharacterController _controller;
        private Transform _attractionSource;
        private Vector3 _attractionVelocity;
        private float _velocityY;
        private bool _groundSupported;

        public float VelocityY => _velocityY;
        public bool IsGrounded => _groundSupported;
        public bool IsFalling => !_groundSupported && _velocityY < 0f;
        public bool IsAttractionActive => _attractionSource != null &&
            _attractionVelocity.sqrMagnitude > 0f;
        private float ProbeStartOffset => 0.05f;

        private void Awake() => _controller = GetComponent<CharacterController>();

        private void OnDisable()
        {
            _attractionSource = null;
            _attractionVelocity = Vector3.zero;
        }

        public void SetAttraction(Transform source, Vector3 velocity)
        {
            if (source == null) return;
            velocity.y = 0f;
            _attractionSource = source;
            _attractionVelocity = velocity;
        }

        public void ClearAttraction(Transform source)
        {
            if (_attractionSource != source) return;
            _attractionSource = null;
            _attractionVelocity = Vector3.zero;
        }

        public Vector3 GetAttractionDisplacement(float deltaTime)
        {
            if (_controller == null || _attractionSource == null ||
                _attractionVelocity.sqrMagnitude <= 0f) return Vector3.zero;
            Vector3 toSource = _attractionSource.position - _controller.transform.position;
            toSource.y = 0f;
            float distance = toSource.magnitude;
            return distance > 0.0001f &&
                Vector3.Dot(toSource, _attractionVelocity) > 0f
                ? _attractionVelocity.normalized *
                    Mathf.Min(_attractionVelocity.magnitude * deltaTime, distance)
                : Vector3.zero;
        }

        public CollisionFlags Move(Vector3 displacement) => _controller != null &&
            _controller.enabled ? _controller.Move(displacement) : CollisionFlags.None;

        public void SetGroundVerticalSpeed() => _velocityY = -2f;
        public void BeginJump(float height)
        {
            _groundSupported = false;
            _velocityY = Mathf.Sqrt(Mathf.Max(0f, height) * -2f * _gravity);
        }

        public float CalculateGravity(float deltaTime)
        {
            if (_controller != null && _controller.isGrounded && _velocityY < 0f)
                _velocityY = -2f;
            _velocityY += _gravity * deltaTime;
            return _velocityY;
        }

        public void StopUpwardVelocityOnCeiling(CollisionFlags collision)
        {
            if ((collision & CollisionFlags.Above) != 0 && _velocityY > 0f)
                _velocityY = 0f;
        }

        public void RefreshGroundSupport() =>
            _groundSupported = TryGetWalkableGround(out _);

        public void RefreshLandingSupport() =>
            _groundSupported = _velocityY <= 0f &&
                TryGetWalkableGround(out RaycastHit groundHit) &&
                groundHit.distance <= ProbeStartOffset + _landingDistance;

        public void HoldGround()
        {
            RefreshGroundSupport();
            _velocityY = -2f;
            Vector3 attraction = GetAttractionDisplacement(Time.deltaTime);
            if (!_groundSupported || attraction.sqrMagnitude > 0f)
            {
                Move(attraction + Vector3.up * (_velocityY * Time.deltaTime));
                RefreshGroundSupport();
            }
        }

        // 피격 방향으로 회전하고, 입력 이동과 같은 접지·중력을 적용한다.
        public void FaceHitDirection(Vector3 direction)
        {
            if (_controller == null) return;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f) return;
            _controller.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        public void MoveByHit(Vector3 displacement, float deltaTime)
        {
            if (_controller == null || !isActiveAndEnabled) return;
            displacement.y = 0f;
            bool grounded = TryGetWalkableGround(out _);
            _velocityY = grounded ? -2f : CalculateGravity(deltaTime);
            displacement.y = _velocityY * deltaTime;
            Move(displacement);
            RefreshGroundSupport();
            if (_groundSupported) _velocityY = -2f;
        }

        // 자신의 충돌체를 기준으로 접촉 몸체의 바깥까지 필요한 거리를 계산한다.
        public bool TryGetContactClearanceDistance(Collider[] bodies, Vector3 direction,
            float padding, out float distance)
        {
            distance = 0f;
            if (_controller == null || !_controller.enabled || bodies == null)
                return false;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f) return false;
            direction.Normalize();
            Physics.SyncTransforms();

            bool touching = false;
            float outerEdge = float.NegativeInfinity;
            foreach (Collider body in bodies)
            {
                if (body == null || !body.enabled || !body.gameObject.activeInHierarchy)
                    continue;
                if (Physics.ComputePenetration(body, body.transform.position,
                    body.transform.rotation, _controller,
                    _controller.transform.position, _controller.transform.rotation,
                    out _, out _)) touching = true;

                float extent;
                Vector3 center;
                if (body is BoxCollider box)
                {
                    Transform pose = box.transform;
                    Vector3 half = box.size * 0.5f;
                    center = pose.TransformPoint(box.center);
                    extent =
                        Mathf.Abs(Vector3.Dot(direction, pose.TransformVector(Vector3.right * half.x))) +
                        Mathf.Abs(Vector3.Dot(direction, pose.TransformVector(Vector3.up * half.y))) +
                        Mathf.Abs(Vector3.Dot(direction, pose.TransformVector(Vector3.forward * half.z)));
                }
                else
                {
                    Bounds bounds = body.bounds;
                    center = bounds.center;
                    extent = Mathf.Abs(direction.x) * bounds.extents.x +
                        Mathf.Abs(direction.z) * bounds.extents.z;
                }
                outerEdge = Mathf.Max(outerEdge, Vector3.Dot(direction, center) + extent);
            }
            if (!touching) return false;

            Bounds playerBounds = _controller.bounds;
            float playerExtent = Mathf.Abs(direction.x) * playerBounds.extents.x +
                Mathf.Abs(direction.z) * playerBounds.extents.z;
            float nearEdge = Vector3.Dot(direction, playerBounds.center) - playerExtent;
            distance = Mathf.Max(0f, outerEdge - nearEdge + Mathf.Max(0f, padding));
            return true;
        }

        public bool TryClearContact(Collider[] bodies, Vector3 direction, float maxStep)
        {
            if (!TryGetContactClearanceDistance(bodies, direction, 0f,
                out float distance)) return false;
            direction.y = 0f;
            if (distance > 0f && maxStep > 0f)
            {
                float stepOffset = _controller.stepOffset;
                _controller.stepOffset = 0f;
                try
                {
                    Move(direction.normalized * Mathf.Min(distance, maxStep));
                    RefreshGroundSupport();
                }
                finally { _controller.stepOffset = stepOffset; }
            }
            return true;
        }

        public bool TryGetWalkableGround(out RaycastHit groundHit)
        {
            groundHit = default;
            if (_controller == null) return false;
            float radius = _controller.radius * 0.8f;
            Vector3 capsuleBottom = _controller.transform.TransformPoint(_controller.center) -
                Vector3.up * (_controller.height * 0.5f);
            float startOffset = ProbeStartOffset;
            Vector3 origin = capsuleBottom + Vector3.up * (radius + startOffset);
            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _groundHits,
                startOffset + _groundProbeDistance, _groundLayers, QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            float minUpDot = Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _groundHits[i];
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(_controller.transform) ||
                    Vector3.Dot(hit.normal, Vector3.up) < minUpDot ||
                    hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                groundHit = hit;
            }
            return nearestDistance < float.PositiveInfinity;
        }

        private void OnValidate()
        {
            _groundProbeDistance = Mathf.Max(0f, _groundProbeDistance);
            _landingDistance = Mathf.Max(0f, _landingDistance);
            if (_gravity >= 0f) _gravity = -20f;
        }
    }
}
