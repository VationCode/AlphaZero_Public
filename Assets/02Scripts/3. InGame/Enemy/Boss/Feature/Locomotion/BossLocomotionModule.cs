using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    public enum EBossMovePace
    {
        Walk,
        Run
    }

    /// <summary>추적 이동을 구성하고 실제 몸체 이동은 Movement에 맡긴다.</summary>
    [DisallowMultipleComponent]
    public sealed class BossLocomotionModule : MonoBehaviour
    {
        [SerializeField] private EBossMovePace _chasePace = EBossMovePace.Run;
        [SerializeField, Min(0f)] private float _walkSpeed = 2f;
        [SerializeField, Min(0f)] private float _runSpeed = 5f;
        [SerializeField, Min(0f)] private float _turnSpeed = 100f;
        [SerializeField, Range(0f, 180f)] private float _facingTolerance = 5f;
        [SerializeField, Min(0f)] private float _contactClearanceSpeed = 12f;

        private BossMovementModule _movement;

        public bool CanMove => isActiveAndEnabled && _movement != null && _movement.CanMove;
        public Vector3 Position => _movement != null ? _movement.Position : transform.root.position;
        public float ContactClearanceSpeed => _contactClearanceSpeed;

        public void Bind(BossMovementModule movement)
        {
            StopChase();
            _movement = movement;
        }

        // 회전 실행과 정면 판정은 추적·공격에서 같은 설정을 사용한다.
        public bool IsFacingTarget(Vector3 targetPosition) =>
            CanMove && _movement.IsFacing(targetPosition, _facingTolerance);

        public bool FaceTarget(Vector3 targetPosition) =>
            CanMove && _movement.Face(targetPosition, _turnSpeed, _facingTolerance);

        // 추적 중에는 목표를 바라본 다음 전진한다.
        public bool FaceChaseTarget(Vector3 targetPosition)
        {
            if (!CanMove) return false;
            if (FaceTarget(targetPosition)) return true;
            _movement.StopGroundMovement();
            return false;
        }

        public void ChaseTo(Vector3 targetPosition, float stopDistance)
        {
            if (CanMove)
            {
                float speed = _chasePace == EBossMovePace.Run ? _runSpeed : _walkSpeed;
                _movement.MoveTo(targetPosition, speed, stopDistance);
            }
        }

        public void StopChase() => _movement?.StopGroundMovement();

        private void OnDisable() => StopChase();

        private void OnValidate()
        {
            _walkSpeed = Mathf.Max(0f, _walkSpeed);
            _runSpeed = Mathf.Max(0f, _runSpeed);
            _turnSpeed = Mathf.Max(0f, _turnSpeed);
            _facingTolerance = Mathf.Clamp(_facingTolerance, 0f, 180f);
            _contactClearanceSpeed = Mathf.Max(0f, _contactClearanceSpeed);
        }
    }
}
