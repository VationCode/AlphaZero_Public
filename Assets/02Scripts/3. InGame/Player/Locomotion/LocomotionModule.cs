using System;
using UnityEngine;
namespace alpha.ingame.player
{
    public enum EMoveType
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Combat = 3,
        Boost = 4,
        BoostCombat = 5
    }

    [DefaultExecutionOrder(100)]
    public class LocomotionModule : MonoBehaviour
    {
        [Header("Ref")]
        [SerializeField] private CharacterController _characterCtrl;

        [Header("Move")]
        [SerializeField] private float _walkSpeed = 2f;
        [SerializeField] private float _runSpeed = 6f;
        [SerializeField] private float _combatSpeed = 3f;

        [Header("Mobility")]
        [SerializeField, Min(0f)] private float _boostSpeed = 10f;
        [SerializeField, Min(0.01f)] private float _maxBoostDuration = 4f;
        [SerializeField, Min(0.01f)] private float _boostRechargeDuration = 6f;

        [Header("Speed Transition")]
        [Min(0.001f)]
        [SerializeField] private float _speedSmoothTime = 0.12f;

        [Header("Rotation")]
        [SerializeField] private float _rotationSpeed = 800f;

        [Header("Air")]
        [SerializeField, Min(0.01f)] private float _jumpHeight = 2f;
        [SerializeField, Min(0f)] private float _airSpeed = 3f;
        [SerializeField, Min(0f)] private float _airAcceleration = 8f;

        [Header("Land")]
        [SerializeField, Min(0f)] private float _landDuration = 0.25f;

        public Vector3 MoveDirection { get; private set; }
        public float CurrentSpeed { get; private set; }
        public EMoveType MoveType { get; private set; } = EMoveType.Idle;
        public Transform LockOnTarget { get; private set; }

        // 월드 이동 방향, 이동 타입, 0~1 입력 세기
        public event Action<Vector3, EMoveType, float> OnMove;
        public event Action<int> OnJump;
        public event Action<float> OnBoostRatioChanged;
        public event Action<bool> OnBoostStateChanged;
        public event Action OnBoostFullyCharged;

        public float BoostRatio => Mathf.Clamp01(_boostRemaining / Mathf.Max(0.01f, _maxBoostDuration));
        public bool CanUseBoost => _boostRemaining > 0f;
        public bool IsBoosting => _isBoosting;
        public void ResetJumpCount() => _jumpCount = 0;

        private Vector3 _velocityXZ;
        private float _airEntrySpeed;
        private bool _isAirMoving;
        public float VelocityY => _motion != null ? _motion.VelocityY : 0f;
        private int _jumpCount;
        private float _boostRemaining;
        private bool _isBoosting;
        private PlayerRoot _playerCore;
        private PlayerMotionModule _motion;
        public bool IsGrounded => _motion?.IsGrounded == true;
        public bool IsFalling => _motion?.IsFalling == true;
        public bool CanGroundJump => IsGrounded && _jumpCount == 0;
        public bool CanAirJump => !IsGrounded && _jumpCount == 1;
        public float LandDuration => _landDuration;
        private bool ShouldFaceAimDirection =>
            (MoveType == EMoveType.Combat || MoveType == EMoveType.BoostCombat) &&
            _playerCore?.CombatModule?.ShouldFaceAimDirection == true;
        private void Awake()
        {
            _playerCore = GetComponentInParent<PlayerRoot>(true);
            _characterCtrl = GetComponentInParent<CharacterController>(true);
            _motion = GetComponentInParent<PlayerMotionModule>(true);
            _boostRemaining = Mathf.Max(0.01f, _maxBoostDuration);
            OnBoostRatioChanged?.Invoke(BoostRatio);
        }

        private void Update()
        {
            if (_isBoosting || _boostRemaining >= _maxBoostDuration) return;

            // 부스터를 사용하지 않는 동안 설정된 충전 시간에 맞춰 회복한다.
            float recharge = Time.deltaTime * _maxBoostDuration / Mathf.Max(0.01f, _boostRechargeDuration);
            SetBoostRemaining(_boostRemaining + recharge);
        }

        public void BeginBoost()
        {
            if (_isBoosting) return;
            _isBoosting = true;
            OnBoostStateChanged?.Invoke(true);
        }

        public bool ConsumeBoost(float deltaTime)
        {
            if (!_isBoosting || !CanUseBoost) return false;
            SetBoostRemaining(_boostRemaining - Mathf.Max(0f, deltaTime));
            return CanUseBoost;
        }

        public void EndBoost()
        {
            if (!_isBoosting) return;
            _isBoosting = false;
            OnBoostStateChanged?.Invoke(false);
        }

        private void SetBoostRemaining(float value)
        {
            float maximum = Mathf.Max(0.01f, _maxBoostDuration);
            float previous = _boostRemaining;
            float next = Mathf.Clamp(value, 0f, maximum);
            if (_boostRemaining == next) return;
            _boostRemaining = next;
            OnBoostRatioChanged?.Invoke(BoostRatio);
            // 시작 시의 풀충전은 제외하고, 충전으로 최대치에 도달한 순간만 알린다.
            if (!_isBoosting && previous < maximum && next >= maximum)
                OnBoostFullyCharged?.Invoke();
        }

        private void LateUpdate()
        {
            if (_playerCore?.HitModule?.IsHitMotionActive == true) return;
            if (_playerCore?.SkillStateFlow?.IsMovementLocked == true) return;

            // 공중에서는 이동 입력 방향 회전을 유지하고, 락온만 대상 회전을 우선한다.
            if (LockOnTarget != null || (!_isAirMoving && ShouldFaceAimDirection))
                UpdateFacing(MoveDirection, true);
        }
        public void SetMoveType(Vector2 p_input, bool p_isWalk, bool p_isCombat)
        {
            // Combat Tree는 입력이 없어도 중앙의 대기 애니메이션을 사용한다.
            MoveType = p_isCombat ? EMoveType.Combat
                     : p_input.sqrMagnitude <= 0.0001f ? EMoveType.Idle
                     : p_isWalk ? EMoveType.Walk :  EMoveType.Run;
        }

        public void SetBoostMoveType(bool p_isCombat) =>
            MoveType = p_isCombat ? EMoveType.BoostCombat : EMoveType.Boost;

        public void Move(Vector2 p_input)
        {
            // 전투 이동을 우선하고, 걷기 입력에 따라 속도를 선택한다.
            float inputStrength = Mathf.Clamp01(p_input.magnitude);
            MoveDirection = GetWorldMoveDirection(p_input);

            float targetSpeed = MoveType switch
            {
                EMoveType.Idle => 0f,
                EMoveType.Walk => _walkSpeed,
                EMoveType.Run => _runSpeed,
                EMoveType.Combat => _combatSpeed,
                EMoveType.Boost => _boostSpeed,
                EMoveType.BoostCombat => _boostSpeed,
                _ => 0f
            };

            UpdateFacing(MoveDirection, ShouldFaceAimDirection);

            // 이동 속도 값을 부드럽게 전환
            CurrentSpeed = Mathf.Lerp(CurrentSpeed, targetSpeed * inputStrength, Time.deltaTime / _speedSmoothTime);

            _velocityXZ = MoveDirection * targetSpeed * inputStrength;
            _isAirMoving = false;

            // 가까운 보행 가능 지면의 법선을 따라 움직여 내리막에서도 발밑 간격을 유지한다.
            bool hasGround = _motion.TryGetWalkableGround(out RaycastHit groundHit);
            Vector3 velocity = hasGround && _velocityXZ.sqrMagnitude > 0f
                ? Vector3.ProjectOnPlane(_velocityXZ, groundHit.normal).normalized * _velocityXZ.magnitude
                : _velocityXZ;

            if (hasGround)
                _motion.SetGroundVerticalSpeed();
            else
                velocity.y = _motion.CalculateGravity(Time.deltaTime);

            _motion.Move(velocity * Time.deltaTime +
                _motion.GetAttractionDisplacement(Time.deltaTime));
            _motion.RefreshGroundSupport();

            // 월드 방향과 입력 세기를 애니메이션에 전달한다.
            OnMove?.Invoke(MoveDirection, MoveType, inputStrength);
        }

        private Vector3 GetWorldMoveDirection(Vector2 p_input)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null || p_input.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Transform aim = alpha.ingame.camera.CameraAim.StableTransform(mainCamera);
            Vector3 forward = aim.forward;
            Vector3 right = aim.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return (forward * p_input.y + right * p_input.x).normalized;
        }

        public void SetLockOnTarget(Transform p_target)
        {
            LockOnTarget = p_target;
            UpdateFacing(MoveDirection, ShouldFaceAimDirection);
        }

        private void UpdateFacing(Vector3 p_direction, bool p_faceCameraImmediately)
        {
            if (_characterCtrl == null ||
                _playerCore?.HitModule?.IsHitMotionActive == true) return;
            Transform playerTransform = _characterCtrl.transform;
            if (LockOnTarget != null)
            {
                Vector3 targetDirection = LockOnTarget.position - playerTransform.position;
                targetDirection.y = 0f;
                if (targetDirection.sqrMagnitude > 0.0001f)
                    playerTransform.rotation = Quaternion.LookRotation(targetDirection, Vector3.up);
                return;
            }

            if (p_faceCameraImmediately)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera == null) return;
                Transform aim = alpha.ingame.camera.CameraAim.StableTransform(mainCamera);
                playerTransform.rotation = Quaternion.Euler(0f, aim.eulerAngles.y, 0f);
                return;
            }

            // 일반 이동은 실제 이동 방향으로 부드럽게 회전한다.
            p_direction.y = 0f;
            if (p_direction.sqrMagnitude <= 0.0001f) return;
            Quaternion targetRotation = Quaternion.LookRotation(p_direction, Vector3.up);
            playerTransform.rotation = Quaternion.RotateTowards(
                playerTransform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }

        public void Jump()
        {
            // 진입 가능 여부는 상태에서 확인한다.
            _motion.BeginJump(_jumpHeight);
            _jumpCount++;
            OnJump?.Invoke(_jumpCount);
        }

        public void AirMove(Vector2 p_moveInput)
        {
            float deltaTime = Time.deltaTime;

            if (!_isAirMoving)
            {
                // 점프 또는 낙하 직전의 속도를 공중 방향 전환의 기준으로 사용한다.
                _airEntrySpeed = Mathf.Max(_airSpeed, _velocityXZ.magnitude);
                _isAirMoving = true;
            }

            MoveDirection = GetWorldMoveDirection(p_moveInput);
            float inputStrength = Mathf.Clamp01(p_moveInput.magnitude);

            if (inputStrength > 0f)
            {
                // 기존 관성에서 입력 방향의 속도로 서서히 전환해 실제 이동에 반영한다.
                Vector3 targetVelocityXZ = MoveDirection * (_airEntrySpeed * inputStrength);
                _velocityXZ = Vector3.MoveTowards(
                    _velocityXZ, targetVelocityXZ, _airAcceleration * deltaTime);
            }

            CurrentSpeed = _velocityXZ.magnitude;
            UpdateFacing(inputStrength > 0f ? MoveDirection : _velocityXZ, false);

            // 공중에서도 중력과 CharacterController 이동을 매 프레임 처리한다.
            Vector3 velocity = _velocityXZ;
            velocity.y = _motion.CalculateGravity(deltaTime);
            CollisionFlags collision = _motion.Move(velocity * deltaTime +
                _motion.GetAttractionDisplacement(deltaTime));

            // 상승 중에는 이전 프레임의 접지를 재사용하지 않고, 하강 시 발밑에 도달했을 때만 착지한다.
            _motion.RefreshLandingSupport();

            // 천장에 닿으면 상승을 종료한다.
            _motion.StopUpwardVelocityOnCeiling(collision);
        }

        public void HoldGround()
        {
            StopForForcedMotion();
            // OnMove를 발생시키지 않아 착지 애니메이션을 유지한다.
            _motion?.HoldGround();
        }

        // Action이 강제 이동을 시작할 때 이전 입력 이동의 관성을 지운다.
        public void StopForForcedMotion()
        {
            MoveDirection = Vector3.zero;
            _velocityXZ = Vector3.zero;
            _isAirMoving = false;
            CurrentSpeed = 0f;
        }

    }
}
