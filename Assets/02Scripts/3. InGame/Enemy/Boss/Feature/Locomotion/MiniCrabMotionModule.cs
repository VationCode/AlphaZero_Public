using System;
using alpha.system.combat;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>소환 위치를 중심으로 MiniCrab을 로컬 좌우 방향으로 왕복 이동시킨다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MiniCrabMotionModule : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _halfRange = 1.5f;
        [SerializeField, Min(0f)] private float _speed = 2f;

        private Rigidbody _body;
        private SphereCollider _contactCollider;
        private Vector3 _origin;
        private Vector3 _right;
        private Vector3 _pushTarget;
        private IShieldBarrier _pushBarrier;
        private RigidbodyConstraints _originalConstraints;
        private float _originalDamping;
        private float _pushDeadline;
        private float _travel;
        private float _previousOffset;
        private bool _started;
        private bool _spawnInitialized;
        private bool _stopped;
        private bool _pushing;

        public int Direction { get; private set; }
        public bool IsBeingPushed => _pushing;
        public Vector3 Position => _body != null ? _body.position : transform.position;
        public Vector3 ContactCenter => _contactCollider != null
            ? _contactCollider.transform.TransformPoint(_contactCollider.center) : Position;
        public Vector3 PreviousContactCenter { get; private set; }
        public float ContactRadius => _contactCollider == null ? 0f :
            _contactCollider.radius * Mathf.Max(
                Mathf.Abs(_contactCollider.transform.lossyScale.x),
                Mathf.Abs(_contactCollider.transform.lossyScale.y),
                Mathf.Abs(_contactCollider.transform.lossyScale.z));
        public event Action<int> DirectionChanged;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _contactCollider = GetComponentInChildren<SphereCollider>(true);
        }

        private void OnEnable()
        {
            _spawnInitialized = false;
            _travel = 0f;
            _previousOffset = 0f;
            _stopped = false;
            _pushing = false;
            SetDirection(_halfRange > 0f && _speed > 0f ? 1 : 0);
            ShieldBarrierRegistry.Activated += TryPushFromShield;
            if (_started) InitializeSpawn();
        }

        private void OnDisable()
        {
            ShieldBarrierRegistry.Activated -= TryPushFromShield;
            if (_pushing) FinishPush(false);
        }

        private void Start()
        {
            _started = true;
            InitializeSpawn(); // Instantiate의 위치·회전 적용이 끝난 뒤 개체별 방향을 정한다.
        }

        private void InitializeSpawn()
        {
            Quaternion rotation = Quaternion.AngleAxis(
                UnityEngine.Random.Range(0f, 360f), Vector3.up) * transform.rotation;
            _body.rotation = rotation;
            transform.rotation = rotation;
            _origin = _body.position;
            _right = rotation * Vector3.right;
            _spawnInitialized = true;
            PreviousContactCenter = ContactCenter;
            if (ShieldBarrierRegistry.TryGetContaining(ContactCenter,
                    ContactRadius, out IShieldBarrier barrier))
                TryPushFromShield(barrier);
        }

        private void FixedUpdate()
        {
            PreviousContactCenter = ContactCenter;
            if (_pushing)
            {
                Vector3 remaining = _pushTarget - _body.position;
                remaining.y = 0f;
                if (!_pushBarrier.IsActive || remaining.sqrMagnitude <= 0.01f ||
                    Time.time >= _pushDeadline)
                    FinishPush(true);
                return;
            }
            if (!_spawnInitialized || _stopped || _halfRange <= 0f || _speed <= 0f) return;

            _travel = Mathf.Repeat(_travel + _speed * Time.fixedDeltaTime, 4f * _halfRange);
            float offset = Mathf.PingPong(_travel + _halfRange, 2f * _halfRange) - _halfRange;
            if (offset > _previousOffset + 0.0001f) SetDirection(1);
            else if (offset < _previousOffset - 0.0001f) SetDirection(-1);

            _body.MovePosition(_origin + _right * offset);
            _previousOffset = offset;
        }

        // 쉴드가 내부의 MiniCrab을 밀 때만 잠시 물리 Rigidbody로 전환한다.
        private void TryPushFromShield(IShieldBarrier barrier)
        {
            if (!_spawnInitialized || _stopped || _pushing || !barrier.IsActive) return;
            Vector3 outward = ContactCenter - barrier.Center;
            outward.y = 0f;
            float contactDistance = barrier.Radius + ContactRadius;
            if (outward.sqrMagnitude >= contactDistance * contactDistance) return;
            // 이후 왕복 이동의 최대 안쪽 진폭까지 고려해 목표를 잡는다.
            float targetDistance = barrier.Radius + ContactRadius + _halfRange + 0.2f;
            outward = outward.sqrMagnitude > 0.0001f
                ? outward.normalized : transform.forward;
            outward.y = 0f;
            outward.Normalize();
            _pushTarget = barrier.Center + outward * targetDistance;
            _pushTarget.y = _body.position.y;
            _pushBarrier = barrier;
            _pushDeadline = Time.time + 1.5f;
            _originalConstraints = _body.constraints;
            _originalDamping = _body.linearDamping;
            _pushing = true;
            SetDirection(0);
            _body.isKinematic = false;
            _body.constraints |= RigidbodyConstraints.FreezePositionY;
            _body.linearDamping = 1f;
            _body.linearVelocity = Vector3.zero;
            _body.AddForce(outward * barrier.PushImpulse, ForceMode.Impulse);
        }

        private void FinishPush(bool resumePatrol)
        {
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.isKinematic = true;
            _body.constraints = _originalConstraints;
            _body.linearDamping = _originalDamping;
            _pushing = false;
            _pushBarrier = null;
            if (!resumePatrol || _stopped) return;
            _body.position = _pushTarget;
            _origin = _pushTarget;
            _travel = 0f;
            _previousOffset = 0f;
            SetDirection(_halfRange > 0f && _speed > 0f ? 1 : 0);
        }

        public void Stop()
        {
            _stopped = true;
            if (_pushing) FinishPush(false);
            SetDirection(0);
        }

        private void SetDirection(int direction)
        {
            if (Direction == direction) return;
            Direction = direction;
            DirectionChanged?.Invoke(direction);
        }

        private void OnValidate()
        {
            _halfRange = Mathf.Max(0f, _halfRange);
            _speed = Mathf.Max(0f, _speed);
        }
    }
}
