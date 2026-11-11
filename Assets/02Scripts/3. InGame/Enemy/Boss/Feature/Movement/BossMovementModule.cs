using alpha.ingame.player;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>걷기·달리기, 점프·돌진의 몸체 이동과 접촉 처리를 실행한다.</summary>
    public sealed class BossMovementModule
    {
        private const float ContactDirectionEpsilon = 0.0001f;

        private readonly Rigidbody _body;
        private readonly BoxCollider[] _groundBodyColliders;
        private readonly bool[] _ignoredPlayerPairs;
        private readonly float _contactClearanceSpeed;
        private CharacterController _contactPlayer;
        private bool _contactRestorePending;
        private PlayerRoot _blockingPlayer;
        private CharacterController _blockingController;
        private Vector3 _jumpStart;
        private Vector3 _jumpDestination;
        private float _jumpProgress;
        private float _appliedJumpProgress;
        private float _jumpAnimatedHeight;
        private float _jumpAdditionalHeight;
        private float _appliedAnimatedHeight;
        private float _appliedAdditionalHeight;
        private bool _restoreGravity;
        private Vector3 _dashStart;
        private Vector3 _dashForward;
        private float _dashDistance;
        private RigidbodyConstraints _constraintsBeforeRotationLock;
        private bool _rotationLocked;

        public bool CanMove => _body != null && !_body.isKinematic;
        public Vector3 Position => _body != null ? _body.position : Vector3.zero;
        public Vector3 Forward => _body != null ? _body.rotation * Vector3.forward : Vector3.forward;
        public float CurrentSpeed => _body != null ?
            Vector3.ProjectOnPlane(_body.linearVelocity, Vector3.up).magnitude : 0f;
        public bool IsJumping { get; private set; }
        public bool IsDashing { get; private set; }
        // 보간된 실제 Transform 높이를 반환해 View가 같은 화면 프레임에서 보정한다.
        public float CurrentJumpHeight => IsJumping && _body != null
            ? Mathf.Max(0f, _body.transform.position.y -
                Mathf.Lerp(_jumpStart.y, _jumpDestination.y, _appliedJumpProgress))
            : 0f;
        // View는 애니메이션이 만든 높이만 상쇄하고 패턴의 추가 높이는 유지한다.
        public float CurrentAnimatedJumpHeight
        {
            get
            {
                float appliedHeight = _appliedAnimatedHeight + _appliedAdditionalHeight;
                return appliedHeight > 0.0001f
                    ? CurrentJumpHeight * _appliedAnimatedHeight / appliedHeight
                    : 0f;
            }
        }

        public BossMovementModule(Rigidbody body, BoxCollider[] groundBodyColliders,
            float contactClearanceSpeed)
        {
            _body = body;
            _groundBodyColliders = groundBodyColliders ?? System.Array.Empty<BoxCollider>();
            _ignoredPlayerPairs = new bool[_groundBodyColliders.Length];
            _contactClearanceSpeed = Mathf.Max(0f, contactClearanceSpeed);
        }

        // 물리 프레임에서 반복 호출한다. 도착하면 수평 이동을 멈춘다.
        public bool MoveTo(Vector3 destination, float configuredSpeed, float stopDistance)
        {
            if (_body == null || _body.isKinematic || IsJumping || IsDashing) return false;

            Vector3 offset = destination - _body.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            float stoppingDistance = Mathf.Max(0f, stopDistance);
            if (distance <= stoppingDistance)
            {
                StopGroundMovement();
                return true;
            }

            float speed = Mathf.Min(Mathf.Max(0f, configuredSpeed),
                (distance - stoppingDistance) / Time.fixedDeltaTime);
            Vector3 velocity = _body.linearVelocity;
            Vector3 direction = offset / distance;
            velocity.x = direction.x * speed;
            velocity.z = direction.z * speed;
            _body.linearVelocity = velocity; // 중력에 따른 Y 속도는 유지한다.
            return false;
        }

        public bool IsFacing(Vector3 targetPosition, float facingTolerance)
        {
            if (_body == null || _body.isKinematic) return false;

            Vector3 direction = targetPosition - _body.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return true;

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            return Quaternion.Angle(_body.rotation, targetRotation) <= Mathf.Clamp(facingTolerance, 0f, 180f);
        }

        // 물리 프레임에서 회전한다. 실제 회전이 적용된 다음 틱에 정면을 확인한다.
        public bool Face(Vector3 targetPosition, float turnSpeed, float facingTolerance)
        {
            if (_rotationLocked || IsDashing) return false;
            if (IsFacing(targetPosition, facingTolerance)) return true;
            if (_body == null || _body.isKinematic) return false;

            Vector3 direction = targetPosition - _body.position;
            direction.y = 0f;
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);

            Quaternion next = Quaternion.RotateTowards(_body.rotation, targetRotation,
                Mathf.Max(0f, turnSpeed) * Time.fixedDeltaTime);
            _body.MoveRotation(next);
            return false;
        }

        // 이동 중지 상태가 요청한 회전 제한을 물리 충돌에도 유지한다.
        public void LockRotation()
        {
            if (_body == null || _rotationLocked) return;

            _constraintsBeforeRotationLock = _body.constraints;
            if (!_body.isKinematic) _body.angularVelocity = Vector3.zero;
            _body.constraints |= RigidbodyConstraints.FreezeRotationY;
            _rotationLocked = true;
        }

        public void UnlockRotation()
        {
            if (!_rotationLocked) return;
            if (_body != null) _body.constraints = _constraintsBeforeRotationLock;
            _rotationLocked = false;
        }

        // 이동 시작 이벤트에서 착지점을 고정하고 클립의 이벤트 진행률로 이동한다.
        public bool BeginJumpTo(Vector3 destination, Transform target)
        {
            if (_body == null || _body.isKinematic || IsJumping || IsDashing) return false;

            if (!TryBeginContactMotion(target)) return false;

            StopGroundMovement();
            _jumpStart = _body.position;
            _jumpDestination = destination;
            _jumpProgress = 0f;
            _appliedJumpProgress = 0f;
            _jumpAnimatedHeight = 0f;
            _jumpAdditionalHeight = 0f;
            _appliedAnimatedHeight = 0f;
            _appliedAdditionalHeight = 0f;
            _restoreGravity = _body.useGravity;
            _body.linearVelocity = Vector3.zero;
            _body.useGravity = false;
            IsJumping = true;
            return true;
        }

        public void SetJumpPose(float progress, float animatedHeight, float additionalHeight)
        {
            if (!IsJumping) return;
            _jumpProgress = Mathf.Max(_jumpProgress, Mathf.Clamp01(progress));
            _jumpAnimatedHeight = Mathf.Max(0f, animatedHeight);
            _jumpAdditionalHeight = Mathf.Max(0f, additionalHeight);
        }

        // 시작 시점의 위치와 방향을 고정하고 전달받는 거리만큼 돌진한다.
        public bool BeginDash(Transform target)
        {
            if (_body == null || _body.isKinematic || IsJumping || IsDashing ||
                _rotationLocked) return false;
            if (!TryBeginContactMotion(target)) return false;

            StopGroundMovement();
            _dashStart = _body.position;
            _dashForward = Vector3.ProjectOnPlane(Forward, Vector3.up).normalized;
            _dashDistance = 0f;
            IsDashing = true;
            return true;
        }

        // 애니메이션 전방 이동량과 패턴 추가 거리를 합친 현재 이동 거리다.
        public void SetDashDistance(float distance)
        {
            if (IsDashing)
                _dashDistance = Mathf.Max(_dashDistance, Mathf.Max(0f, distance));
        }

        public void EndDash()
        {
            if (!IsDashing || _body == null) return;

            Vector3 destination = _dashStart + _dashForward * _dashDistance;
            destination.y = _body.position.y;
            _body.position = destination; // 충돌 복구 전에 최종 위치를 확정한다.
            IsDashing = false;
            _contactRestorePending = _contactPlayer != null;
            StopGroundMovement();
        }

        public void FixedTick()
        {
            if (_body == null) return;

            if (IsDashing)
            {
                IgnorePlayerCollision();
                Vector3 destination = _dashStart + _dashForward * _dashDistance;
                destination.y = _body.position.y; // Y축 중력은 물리에 맡긴다.
                _body.MovePosition(destination);
            }

            if (IsJumping)
            {
                IgnorePlayerCollision();
                // 애니메이션에서 읽은 높이를 Rigidbody에 적용해 부모와 충돌체도 함께 점프한다.
                _appliedJumpProgress = _jumpProgress;
                float arc = 4f * _appliedJumpProgress * (1f - _appliedJumpProgress);
                _appliedAnimatedHeight = _jumpAnimatedHeight;
                _appliedAdditionalHeight = _jumpAdditionalHeight * arc;
                _body.MovePosition(Vector3.Lerp(_jumpStart, _jumpDestination, _jumpProgress)
                    + Vector3.up * (_appliedAnimatedHeight + _appliedAdditionalHeight));
            }
            else if (_contactRestorePending)
            {
                TryRestorePlayerCollision();
            }
        }

        // GroundBody는 바닥을 지탱하고, 플레이어와의 겹침은 수평 이동으로만 해소한다.
        public void LateTick()
        {
            if (_body == null || IsJumping || IsDashing || _contactRestorePending ||
                _groundBodyColliders.Length == 0) return;

            if (_blockingPlayer == null)
            {
                _blockingPlayer = Object.FindFirstObjectByType<PlayerRoot>();
                _blockingController = null;
            }
            if (_blockingPlayer == null) return;
            if (_blockingController == null)
                _blockingController = _blockingPlayer.GetComponent<CharacterController>();
            if (_blockingController == null || !_blockingController.enabled) return;

            Physics.SyncTransforms();
            Vector3 outward = Vector3.ProjectOnPlane(
                _blockingController.bounds.center - _body.position, Vector3.up);
            if (outward.sqrMagnitude <= ContactDirectionEpsilon)
                outward = Vector3.ProjectOnPlane(Forward, Vector3.up);
            if (outward.sqrMagnitude <= ContactDirectionEpsilon) return;

            _blockingPlayer.TryClearContact(_groundBodyColliders, outward,
                _contactClearanceSpeed * Time.deltaTime);
        }

        // 착지 이벤트에서 남은 물리 프레임 오차를 정리한다.
        public void CompleteJump()
        {
            if (!IsJumping || _body == null) return;

            _body.position = _jumpDestination;
            IsJumping = false;
            _jumpAnimatedHeight = 0f;
            _jumpAdditionalHeight = 0f;
            _appliedAnimatedHeight = 0f;
            _appliedAdditionalHeight = 0f;
            _appliedJumpProgress = 0f;
            _contactRestorePending = _contactPlayer != null;
            _body.useGravity = _restoreGravity;
            _body.linearVelocity = Vector3.zero;
        }

        public void Stop()
        {
            bool wasContactMotion = IsJumping || IsDashing;
            IsDashing = false;
            _dashDistance = 0f;
            _jumpAnimatedHeight = 0f;
            _jumpAdditionalHeight = 0f;
            _appliedAnimatedHeight = 0f;
            _appliedAdditionalHeight = 0f;
            _appliedJumpProgress = 0f;
            if (IsJumping)
            {
                IsJumping = false;
                if (_body != null) _body.useGravity = _restoreGravity;
            }
            if (wasContactMotion) _contactRestorePending = _contactPlayer != null;

            StopGroundMovement();
        }

        public void StopGroundMovement()
        {
            if (_body == null || _body.isKinematic) return;
            Vector3 velocity = _body.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            _body.linearVelocity = velocity;
        }

        // 점프와 전진 공격은 같은 대상과의 충돌 무시·복구 절차를 사용한다.
        private bool TryBeginContactMotion(Transform target)
        {
            CharacterController player = target != null
                ? target.GetComponentInParent<CharacterController>() ??
                  target.GetComponentInChildren<CharacterController>()
                : null;
            if (_contactRestorePending && _contactPlayer != player) return false;

            _contactPlayer = player;
            _contactRestorePending = false;
            IgnorePlayerCollision();
            return true;
        }

        // 지면을 지탱하는 GroundBody와 현재 대상 사이의 물리 충돌만 무시한다.
        private void IgnorePlayerCollision()
        {
            if (_contactPlayer == null || !_contactPlayer.enabled) return;
            for (int i = 0; i < _groundBodyColliders.Length; i++)
            {
                BoxCollider bodyCollider = _groundBodyColliders[i];
                if (bodyCollider == null || !bodyCollider.enabled ||
                    !bodyCollider.gameObject.activeInHierarchy ||
                    Physics.GetIgnoreCollision(bodyCollider, _contactPlayer)) continue;
                Physics.IgnoreCollision(bodyCollider, _contactPlayer, true);
                _ignoredPlayerPairs[i] = true;
            }
        }

        private void TryRestorePlayerCollision()
        {
            if (_contactPlayer == null || !_contactPlayer.enabled ||
                !_contactPlayer.gameObject.activeInHierarchy)
            {
                RestorePlayerCollision();
                return;
            }

            // 컴포넌트 재활성화로 충돌 무시가 초기화되더라도 겹친 동안 유지한다.
            IgnorePlayerCollision();
            // 피격 이동이 몸체 밖으로 보내는 동안 별도 이격 이동을 더하지 않는다.
            PlayerRoot player = _contactPlayer.GetComponentInParent<PlayerRoot>();
            if (player?.HitModule?.IsHitMotionActive == true) return;
            Vector3 direction = Vector3.ProjectOnPlane(
                _contactPlayer.bounds.center - _body.position, Vector3.up);
            if (direction.sqrMagnitude <= ContactDirectionEpsilon)
                direction = Vector3.ProjectOnPlane(Forward, Vector3.up);
            if (player != null && player.TryClearContact(_groundBodyColliders,
                    direction, _contactClearanceSpeed * Time.fixedDeltaTime)) return;
            RestorePlayerCollision();
        }

        private void RestorePlayerCollision()
        {
            if (_contactPlayer != null)
                for (int i = 0; i < _groundBodyColliders.Length; i++)
                {
                    if (!_ignoredPlayerPairs[i]) continue;
                    BoxCollider bodyCollider = _groundBodyColliders[i];
                    if (bodyCollider != null && bodyCollider.enabled &&
                        _contactPlayer.enabled &&
                        Physics.GetIgnoreCollision(bodyCollider, _contactPlayer))
                        Physics.IgnoreCollision(bodyCollider, _contactPlayer, false);
                    _ignoredPlayerPairs[i] = false;
                }
            _contactPlayer = null;
            _contactRestorePending = false;
        }

        public void OnDisabled()
        {
            Stop();
            RestorePlayerCollision();
            UnlockRotation();
        }

        public void Dispose()
        {
            RestorePlayerCollision();
            UnlockRotation();
        }

    }
}
