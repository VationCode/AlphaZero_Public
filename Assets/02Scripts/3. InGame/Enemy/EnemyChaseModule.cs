using UnityEngine;

namespace alpha.ingame.enemy
{
    public enum EEnemyChaseCancelReason
    {
        None,
        TargetLost,
        Distance,
        SightLost,
        ActivityArea,
        Duration,
        NoProgress,
        PathFailed,
        CombatArea
    }

    /// <summary>추적 이동과 대상 기록을 관리하고 취소 조건을 평가한다.</summary>
    public sealed class EnemyChaseModule : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.01f), Tooltip("추적 중 이동 속도입니다. 초당 이동하는 월드 단위로 설정합니다.")]
        private float _chaseSpeed = 6f;
        [SerializeField, Min(0.01f), InspectorName("Stop Distance"),
         Tooltip("적 루트와 추적 목적지의 수평 거리가 이 값 이하가 되면 정지합니다.")]
        private float _chaseStopDistance = 2f;
        [SerializeField, Tooltip("작업씬에 정지 거리 원을 표시합니다. Play 중에는 추적 목적지가 원의 중심입니다.")]
        private bool _showChaseStopDistance = true;
        [SerializeField, Tooltip("정지 거리 원의 표시 색상입니다.")]
        private Color _chaseStopColor = new Color(0.7f, 0.3f, 1f, 0.9f);

        [Header("General Cancel")]
        [SerializeField, Tooltip("플레이어와의 거리로 추적을 취소할지 결정합니다.")]
        private bool _useReleaseDistance = true;
        [SerializeField, Min(0f), Tooltip("적과 플레이어 루트의 수평 거리가 이 값보다 크면 취소합니다. 0이면 적용하지 않습니다.")]
        private float _releaseDistance = 20f;
        [SerializeField, Tooltip("플레이어를 감지하지 못한 시간이 설정값에 도달하면 추적을 취소합니다.")]
        private bool _useLostSight = true;
        [SerializeField, Min(0f), Tooltip("마지막 감지 또는 근접 공격 완료 후 이 시간(초) 동안 보이지 않으면 취소합니다. 0이면 즉시 취소합니다.")]
        private float _lostSightDuration = 2f;
        [SerializeField, Tooltip("NavMesh 경로 또는 이동 요청이 실패하면 추적을 취소할지 결정합니다.")]
        private bool _usePathFailure = true;
        [SerializeField, Min(0f), Tooltip("경로 실패가 연속으로 지속될 수 있는 시간(초)입니다. 0이면 즉시 취소합니다.")]
        private float _pathFailureDuration = 0.5f;

        [Header("Optional Cancel")]
        [SerializeField, Tooltip("적이 최초 위치에서 너무 멀어지면 추적을 취소합니다.")]
        private bool _useActivityArea = true;
        [SerializeField, Min(0f), Tooltip("최초 위치에서 적 루트까지 허용할 수평 거리입니다. 0이면 적용하지 않습니다.")]
        private float _activityRadius = 25f;
        [SerializeField, Tooltip("근접 공격 사이에 추적할 수 있는 시간을 제한합니다. 공격이 끝나면 시간이 다시 시작됩니다.")]
        private bool _useMaxChaseDuration;
        [SerializeField, Min(0f), Tooltip("추적 시작 또는 근접 공격 완료 후 허용할 시간(초)입니다. Play 중 체크를 다시 켜면 시간이 초기화됩니다. 0이면 적용하지 않습니다.")]
        private float _maxChaseDuration = 10f;
        [SerializeField, Tooltip("플레이어와의 거리가 충분히 줄지 않으면 추적을 취소합니다.")]
        private bool _useNoProgress;
        [SerializeField, Min(0f), Tooltip("Progress Distance만큼 접근하지 못한 상태의 허용 시간(초)입니다. 0이면 적용하지 않습니다.")]
        private float _noProgressDuration = 3f;
        [SerializeField, Min(0.01f), Tooltip("적과 플레이어의 수평 거리가 이 값 이상 줄면 접근 성공으로 보고 시간을 다시 셉니다.")]
        private float _progressDistance = 0.5f;
        [SerializeField, Tooltip("플레이어가 지정된 전투 구역을 벗어나면 추적을 취소합니다.")]
        private bool _useCombatArea;
        [SerializeField, Tooltip("전투 구역을 나타내는 BoxCollider입니다. 플레이어 루트의 XZ 위치를 판정하며 높이는 무시합니다.")]
        private BoxCollider _combatArea;

        [Header("Test")]
        [SerializeField, Tooltip("추적이 취소될 때 사유를 Console에 한 번 출력합니다.")]
        private bool _logCancelReason;

        private EnemyRoot _enemy;
        private EnemyLocomotionModule _locomotion;
        private Vector3 _destination;
        private bool _hasDestination;
        private bool _moving;
        private Vector3 _previousPosition;
        private bool _hasMovementSample;
        private Vector3 _activityOrigin;
        private float _startedAt;
        private float _lastSeenAt;
        private float _lastProgressAt;
        private float _bestDistance;
        private float _pathFailureStartedAt = -1f;

        public Transform Target { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }
        public bool CanMove => _locomotion != null && _locomotion.CanMove;
        public float ChaseSpeed => Mathf.Max(0.01f, _chaseSpeed);
        public float StopDistance => Mathf.Max(0.01f, _chaseStopDistance);
        public bool PathFailed => CanMove && _moving && _hasDestination &&
                                  _locomotion.HasPathFailed(StopDistance, 0f);

        private void Awake()
        {
            _enemy = GetComponentInParent<EnemyRoot>(true);
            _locomotion = GetComponent<EnemyLocomotionModule>();
            if (_locomotion == null)
                Debug.LogError($"{name}: 같은 오브젝트에 EnemyLocomotionModule이 필요합니다.", this);
        }

        public void Begin(Transform target, Vector3 activityOrigin, Vector3 enemyPosition)
        {
            _moving = false;
            _hasDestination = false;
            _hasMovementSample = false;
            Target = target;
            _activityOrigin = activityOrigin;
            LastSeenPosition = target != null ? target.position : enemyPosition;
            _destination = LastSeenPosition;
            ResetCancellationProgress(enemyPosition);
        }

        public void ResetCancellationProgress(Vector3 enemyPosition)
        {
            // 공격 완료 후에도 대상과 활동 시작점은 유지하고 취소 판정 기록만 새로 시작한다.
            float now = Time.time;
            _startedAt = now;
            _lastSeenAt = now;
            _lastProgressAt = now;
            _pathFailureStartedAt = -1f;
            _bestDistance = Target != null
                ? HorizontalDistance(enemyPosition, Target.position) : 0f;
        }

        public EEnemyChaseCancelReason Evaluate(Vector3 enemyPosition, bool visible,
            bool atTarget, bool pathFailed)
        {
            // 대상 소실은 테스트 체크박스와 무관하게 즉시 취소한다.
            if (Target == null || !Target.gameObject.activeInHierarchy)
                return EEnemyChaseCancelReason.TargetLost;

            float now = Time.time;
            float distance = HorizontalDistance(enemyPosition, Target.position);
            if (_useReleaseDistance && _releaseDistance > 0f && distance > _releaseDistance)
                return EEnemyChaseCancelReason.Distance;
            if (_useActivityArea && _activityRadius > 0f &&
                HorizontalDistance(enemyPosition, _activityOrigin) > _activityRadius)
                return EEnemyChaseCancelReason.ActivityArea;
            if (_useCombatArea && _combatArea != null && !IsInsideCombatArea(Target.position))
                return EEnemyChaseCancelReason.CombatArea;
            // Play 중 체크를 다시 켜면 꺼져 있던 동안의 시간을 누적하지 않는다.
            if (!_useMaxChaseDuration) _startedAt = now;
            if (_useMaxChaseDuration && _maxChaseDuration > 0f &&
                now - _startedAt >= _maxChaseDuration)
                return EEnemyChaseCancelReason.Duration;

            if (visible)
            {
                _lastSeenAt = now;
                LastSeenPosition = Target.position;
            }
            else if (!_useLostSight)
                _lastSeenAt = now;
            else if (now - _lastSeenAt >= _lostSightDuration)
                return EEnemyChaseCancelReason.SightLost;

            if (!_useNoProgress)
            {
                _bestDistance = distance;
                _lastProgressAt = now;
            }
            else
            {
                if (atTarget || distance <= _bestDistance - _progressDistance)
                {
                    _bestDistance = distance;
                    _lastProgressAt = now;
                }
                if (_noProgressDuration > 0f && !atTarget &&
                    now - _lastProgressAt >= _noProgressDuration)
                    return EEnemyChaseCancelReason.NoProgress;
            }

            if (!_usePathFailure || atTarget || !pathFailed)
                _pathFailureStartedAt = -1f;
            else if (_pathFailureStartedAt < 0f)
                _pathFailureStartedAt = now;
            if (_usePathFailure && _pathFailureStartedAt >= 0f &&
                now - _pathFailureStartedAt >= _pathFailureDuration)
                return EEnemyChaseCancelReason.PathFailed;

            return EEnemyChaseCancelReason.None;
        }

        public void End()
        {
            StopMovement();
            Target = null;
            _pathFailureStartedAt = -1f;
        }

        public void ReportCancellation(EEnemyChaseCancelReason reason)
        {
            if (_logCancelReason)
                Debug.Log($"{name}: 추적 취소 - {reason}", this);
        }

        public bool HasReachedTarget(Vector3 targetPosition)
        {
            if (_enemy == null || !CanMove) return false;
            return HorizontalDistance(_enemy.transform.position, targetPosition) <= StopDistance;
        }

        public bool MoveTowards(Vector3 destination)
        {
            if (_enemy == null || _enemy.IsDead || !CanMove) return false;
            bool wasMoving = _moving;
            if (!_locomotion.MoveTo(destination, ChaseSpeed))
            {
                StopMovement();
                return false;
            }

            _destination = destination;
            _hasDestination = true;
            _moving = true;
            if (!wasMoving)
            {
                _previousPosition = _enemy.transform.position;
                _hasMovementSample = true;
            }
            return true;
        }

        public void StopAtBoundary(Vector3 targetPosition)
        {
            _destination = targetPosition;
            if (_moving) TryClampToBoundary(targetPosition);
            StopMovement();
        }

        private void StopMovement()
        {
            _moving = false;
            _hasDestination = false;
            _hasMovementSample = false;
            _locomotion?.Stop();
        }

        private void LateUpdate()
        {
            if (!_moving || !CanMove || !_hasMovementSample) return;
            if (_hasDestination && TryClampToBoundary(_destination))
            {
                StopMovement();
                return;
            }
            _previousPosition = _enemy.transform.position;
        }

        private bool TryClampToBoundary(Vector3 targetPosition)
        {
            if (!CanMove || !_hasMovementSample) return false;

            Vector3 current = _enemy.transform.position;
            Vector3 from = _previousPosition - targetPosition;
            Vector3 to = current - targetPosition;
            from.y = 0f;
            to.y = 0f;
            float radiusSquared = StopDistance * StopDistance;
            if (from.sqrMagnitude <= radiusSquared || to.sqrMagnitude >= radiusSquared)
                return false;

            // 한 프레임에 정지 원을 넘어가면 이동 경로와 원의 교점으로 되돌린다.
            Vector3 step = to - from;
            float a = Vector3.Dot(step, step);
            if (a < 0.000001f) return false;
            float b = 2f * Vector3.Dot(from, step);
            float discriminant = b * b - 4f * a * (from.sqrMagnitude - radiusSquared);
            if (discriminant < 0f) return false;

            float fraction = Mathf.Clamp01((-b - Mathf.Sqrt(discriminant)) / (2f * a));
            return _locomotion.WarpTo(Vector3.Lerp(_previousPosition, current, fraction));
        }

        private bool IsInsideCombatArea(Vector3 position)
        {
            Vector3 local = _combatArea.transform.InverseTransformPoint(position) - _combatArea.center;
            Vector3 halfSize = _combatArea.size * 0.5f;
            return Mathf.Abs(local.x) <= halfSize.x && Mathf.Abs(local.z) <= halfSize.z;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            Vector3 offset = a - b;
            offset.y = 0f;
            return offset.magnitude;
        }

        private void OnValidate()
        {
            _chaseSpeed = Mathf.Max(0.01f, _chaseSpeed);
            _chaseStopDistance = Mathf.Max(0.01f, _chaseStopDistance);
            _releaseDistance = Mathf.Max(0f, _releaseDistance);
            _lostSightDuration = Mathf.Max(0f, _lostSightDuration);
            _pathFailureDuration = Mathf.Max(0f, _pathFailureDuration);
            _activityRadius = Mathf.Max(0f, _activityRadius);
            _maxChaseDuration = Mathf.Max(0f, _maxChaseDuration);
            _noProgressDuration = Mathf.Max(0f, _noProgressDuration);
            _progressDistance = Mathf.Max(0.01f, _progressDistance);
        }

        private void OnDrawGizmos()
        {
            if (!_showChaseStopDistance) return;
            EnemyRoot enemy = _enemy != null ? _enemy : GetComponentInParent<EnemyRoot>(true);
            if (enemy == null) return;

            Collider body = enemy.GetComponent<Collider>();
            float height = body != null ? body.bounds.center.y - enemy.transform.position.y : 1f;
            Vector3 center = enemy.transform.position + Vector3.up * height;
            if (Application.isPlaying && enemy.StateFlow != null &&
                enemy.StateFlow.CurrentState == EEnemyState.Chase)
            {
                center = _destination + Vector3.up * height;
                Gizmos.color = _chaseStopColor;
                Vector3 enemyCenter = enemy.transform.position + Vector3.up * height;
                Gizmos.DrawLine(enemyCenter, center);
                Gizmos.DrawSphere(enemyCenter, 0.08f);
            }
            center.y += 0.05f;
            Gizmos.color = _chaseStopColor;

            const int segments = 48;
            Vector3 previous = center + Vector3.right * StopDistance;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f,
                    Mathf.Sin(angle)) * StopDistance;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
