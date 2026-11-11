using alpha.ingame.player;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace alpha.ingame.enemy
{
    /// <summary>전투 대상을 감지하고 근접 공격의 클립 선택과 실행 시간을 관리한다.</summary>
    public sealed class EnemyCombatModule : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private PlayerRoot _player;
        [SerializeField, Min(0f)] private float _detectionRange = 15f;
        [SerializeField, Range(1f, 360f)] private float _detectionAngle = 120f;
        [SerializeField, InspectorName("Color")]
        private Color _detectionColor = new Color(1f, 0.8f, 0f, 1f);
        [SerializeField, InspectorName("Show")]
        private bool _showDetectionRange = true;

        [Header("Attack")]
        [SerializeField, Min(0f)] private float _attackRange = 2f;
        [SerializeField] private EnemyHitProfile _meleeHit =
            new(1f, EPlayerHitType.Normal);
        [SerializeField, Range(0f, 1f)] private float _meleeHitTime = 0.5f;
        [SerializeField, InspectorName("Color")]
        private Color _attackRangeColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField, InspectorName("Show")]
        private bool _showAttackRange = true;

        private EnemyRoot _enemy;
        private Transform _suppressedTarget;
        private float _meleeAttackEndsAt;
        private float _meleeHitAt;
        private Transform _meleeTarget;
        private bool _meleeHitSent;
        public Transform Target { get; private set; }
        public bool IsMeleeAttacking { get; private set; }
        public float DetectionRange => Mathf.Max(0f, _detectionRange);
        public float DetectionAngle => Mathf.Clamp(_detectionAngle, 1f, 360f);
        public float AttackRange => Mathf.Max(0f, _attackRange);

        private void Awake() => _enemy = GetComponentInParent<EnemyRoot>(true);

        public void RefreshTarget()
        {
            if (_enemy == null || _enemy.IsDead)
            {
                Target = null;
                return;
            }

            if (_player == null) _player = FindFirstObjectByType<PlayerRoot>();
            if (_player == null || !_player.isActiveAndEnabled)
            {
                Target = null;
                return;
            }

            if (_suppressedTarget == _player.transform)
            {
                Target = null;
                return;
            }

            Target = CanSee(_player.transform) ? _player.transform : null;
        }

        public bool CanSee(Transform candidate)
        {
            if (_enemy == null || _enemy.IsDead || candidate == null ||
                !candidate.gameObject.activeInHierarchy)
                return false;
            if (_player != null && candidate == _player.transform && !_player.isActiveAndEnabled)
                return false;

            Vector3 offset = candidate.position - _enemy.transform.position;
            float range = DetectionRange;
            if (range <= 0f || offset.sqrMagnitude > range * range)
                return false;

            // 거리는 3D로, 시야각은 적의 수평 정면을 기준으로 판정한다.
            offset.y = 0f;
            Vector3 forward = _enemy.transform.forward;
            forward.y = 0f;
            return Vector3.Angle(forward, offset) <= DetectionAngle * 0.5f;
        }

        public void SuppressTargetDuringReturn(Transform target)
        {
            _suppressedTarget = target;
            Target = null;
        }

        public void ClearReturnTargetSuppression()
        {
            _suppressedTarget = null;
        }

        public bool IsTargetInRange(Transform target)
        {
            if (_enemy == null || _enemy.IsDead || target == null) return false;
            Vector3 offset = target.position - _enemy.transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= AttackRange * AttackRange;
        }

        public bool TryChooseMeleeAnimation(int animationCount, out int index)
        {
            index = -1;
            if (_enemy == null || _enemy.IsDead || _enemy.Info == null ||
                _enemy.Info.AttackStyle != EEnemyAttackStyle.Melee ||
                IsMeleeAttacking || animationCount <= 0) return false;

            // 공격을 시작할 때 한 번만 클립을 뽑는다.
            index = Random.Range(0, animationCount);
            return true;
        }

        public void BeginMeleeAttack(float animationDuration, Transform target)
        {
            IsMeleeAttacking = true;
            float duration = Mathf.Max(0.01f, animationDuration);
            _meleeAttackEndsAt = Time.time + duration;
            _meleeHitAt = Time.time + duration * _meleeHitTime;
            _meleeTarget = target;
            _meleeHitSent = false;
        }

        public void TickMeleeAttack()
        {
            if (IsMeleeAttacking && !_meleeHitSent && Time.time >= _meleeHitAt)
            {
                _meleeHitSent = true; // 판정 거부나 무적 중에도 한 공격은 한 번만 전달한다.
                TryDeliverMeleeHit();
            }
            if (IsMeleeAttacking && Time.time >= _meleeAttackEndsAt)
                CancelMeleeAttack();
        }

        private void TryDeliverMeleeHit()
        {
            if (_enemy == null || _enemy.IsDead ||
                _meleeTarget == null || !IsTargetInRange(_meleeTarget)) return;

            IPlayerHitReceiver receiver =
                _meleeTarget.GetComponentInParent(typeof(IPlayerHitReceiver)) as IPlayerHitReceiver;
            if (receiver == null) return;
            Collider targetCollider = _meleeTarget.GetComponentInChildren<CharacterController>();
            Vector3 contactPoint = targetCollider != null
                ? targetCollider.ClosestPoint(_enemy.transform.position)
                : _meleeTarget.position;
            Vector3 direction = targetCollider != null
                ? EnemyHitDirection.FromContact(targetCollider, contactPoint,
                    _enemy.transform.position)
                : _meleeTarget.position - _enemy.transform.position;
            receiver.TryReceiveHit(_meleeHit.CreateHit(_enemy.transform, direction));
        }

        public void CancelMeleeAttack()
        {
            IsMeleeAttacking = false;
            _meleeTarget = null;
            _meleeHitSent = false;
        }

        public void Stop()
        {
            CancelMeleeAttack();
            Target = null;
            _suppressedTarget = null;
        }

        private void OnDrawGizmos()
        {
            if (!_showDetectionRange && !_showAttackRange) return;

            // 편집 모드에는 Awake가 실행되지 않으므로 루트 EnemyRoot를 직접 찾는다.
            EnemyRoot enemy = _enemy != null ? _enemy : GetComponentInParent<EnemyRoot>(true);
            if (enemy == null) return;

            if (_showDetectionRange)
            {
                Gizmos.color = _detectionColor;
                DrawDetectionGizmo(enemy.transform);
            }

            if (_showAttackRange) DrawAttackRangeGizmo(enemy.transform.position);
        }

        private void OnValidate() => _meleeHit.Validate();

        private void DrawDetectionGizmo(Transform enemy)
        {
            float radius = DetectionRange;
            if (radius <= 0f) return;

            // 바닥에 가려지지 않도록 본체 콜라이더 중앙 높이에 표시한다.
            Collider body = enemy.GetComponent<Collider>();
            Vector3 center = body != null ? body.bounds.center : enemy.position + Vector3.up;
            center.y += 0.05f;
            Vector3 forward = enemy.forward;
            forward.y = 0f;
            forward.Normalize();

            float halfAngle = DetectionAngle * 0.5f;
            const int segments = 48;
            Vector3 left = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward;
#if UNITY_EDITOR
            Color previousColor = Handles.color;
            Color fillColor = _detectionColor;
            fillColor.a = 0.15f;
            Handles.color = fillColor;
            Handles.DrawSolidArc(center, Vector3.up, left, DetectionAngle, radius);
            Handles.color = previousColor;
#endif
            Vector3 previous = center + left * radius;
            Gizmos.DrawLine(center, previous);

            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                Vector3 next = center + Quaternion.AngleAxis(angle, Vector3.up) * forward * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }

            Gizmos.DrawLine(center, previous);
        }

        private void DrawAttackRangeGizmo(Vector3 origin)
        {
            float radius = AttackRange;
            if (radius <= 0f) return;

            // 공격 판정은 X/Z 거리만 사용하므로 수평 원으로 표시한다.
            const int segments = 48;
            Vector3 center = origin + Vector3.up * 0.05f;
            Vector3 previous = center + Vector3.right * radius;
            Gizmos.color = _attackRangeColor;

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
