using System;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    public enum EBossLocomotionState
    {
        Idle,
        Chasing
    }

    public enum EBossChaseResult
    {
        Reached,
        TargetLost,
        NoProgress,
        MovementUnavailable
    }

    /// <summary>보스의 추적 상태와 도착·진행 정체 판단을 소유한다.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class BossLocomotionStateFlow : MonoBehaviour
    {
        private const float ArrivalTolerance = 0.05f;

        [SerializeField] private bool _useNoProgress = true;
        [Tooltip("이 시간 동안 충분히 가까워지지 않으면 추적을 중단한다. 0이면 적용하지 않는다.")]
        [SerializeField, Min(0f)] private float _noProgressDuration = 3f;
        [Tooltip("목적 거리까지 남은 거리가 이 값 이상 줄어야 진행한 것으로 본다.")]
        [SerializeField, Min(0.01f)] private float _progressDistance = 0.5f;

        private BossLocomotionModule _module;
        private Transform _target;
        private float _stopDistance;
        private float _bestGap;
        private float _lastProgressAt;

        public EBossLocomotionState CurrentState { get; private set; }
        public Transform Target => _target;
        public float ProgressDistance => Mathf.Max(0.01f, _progressDistance);
        public float CurrentDistance => _target != null && _module != null
            ? HorizontalDistance(_module.Position, _target.position) : float.PositiveInfinity;

        public event Action<EBossLocomotionState> StateChanged;
        public event Action<EBossChaseResult, Transform, float, float> ChaseFinished;

        public void Bind(BossLocomotionModule module)
        {
            StopChase();
            _module = module;
        }

        public bool TryBeginChase(Transform target, float stopDistance)
        {
            if (!isActiveAndEnabled || CurrentState != EBossLocomotionState.Idle ||
                _module == null || !_module.CanMove || target == null ||
                !target.gameObject.activeInHierarchy) return false;

            _target = target;
            _stopDistance = Mathf.Max(0f, stopDistance);
            _bestGap = Mathf.Max(0f, CurrentDistance - _stopDistance);
            _lastProgressAt = Time.time;
            ChangeState(EBossLocomotionState.Chasing);
            return true;
        }

        private void FixedUpdate()
        {
            if (CurrentState != EBossLocomotionState.Chasing) return;
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                FinishChase(EBossChaseResult.TargetLost);
                return;
            }
            if (_module == null || !_module.CanMove)
            {
                FinishChase(EBossChaseResult.MovementUnavailable);
                return;
            }

            float distance = CurrentDistance;
            if (!_module.FaceChaseTarget(_target.position))
            {
                _lastProgressAt = Time.time;
                return;
            }

            if (distance <= _stopDistance + ArrivalTolerance)
            {
                FinishChase(EBossChaseResult.Reached);
                return;
            }

            float gap = distance - _stopDistance;
            if (gap <= _bestGap - ProgressDistance)
            {
                _bestGap = gap;
                _lastProgressAt = Time.time;
            }
            if (_useNoProgress && _noProgressDuration > 0f &&
                Time.time - _lastProgressAt >= _noProgressDuration)
            {
                FinishChase(EBossChaseResult.NoProgress);
                return;
            }

            _module.ChaseTo(_target.position, _stopDistance);
        }

        private void FinishChase(EBossChaseResult result)
        {
            Transform target = _target;
            float distance = CurrentDistance;
            float progressDistance = ProgressDistance;
            StopChase();
            ChaseFinished?.Invoke(result, target, distance, progressDistance);
        }

        // 추적 중지 시 공격 점프·돌진에는 관여하지 않는다.
        public void StopChase()
        {
            if (CurrentState == EBossLocomotionState.Idle) return;
            _module?.StopChase();
            _target = null;
            _bestGap = 0f;
            ChangeState(EBossLocomotionState.Idle);
        }

        private void ChangeState(EBossLocomotionState next)
        {
            if (CurrentState == next) return;
            CurrentState = next;
            StateChanged?.Invoke(next);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            Vector3 offset = b - a;
            offset.y = 0f;
            return offset.magnitude;
        }

        private void OnDisable() => StopChase();

        private void OnValidate()
        {
            _noProgressDuration = Mathf.Max(0f, _noProgressDuration);
            _progressDistance = Mathf.Max(0.01f, _progressDistance);
        }
    }
}
