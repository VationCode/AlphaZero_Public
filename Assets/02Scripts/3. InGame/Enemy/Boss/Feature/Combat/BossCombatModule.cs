using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace alpha.ingame.enemy.boss
{
    /// <summary>전투 대상을 찾고 공격 이동을 공통 Movement 기능에 연결한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BossCombatModule : MonoBehaviour
    {
        private const float GroundProbeHeight = 100f;

        [Header("Target Search")]
        [SerializeField] private LayerMask _targetLayers = 1 << 7;
        [SerializeField] private LayerMask _landingGroundLayers = 1 << 6;

        private readonly Collider[] _targetCandidates = new Collider[32];
        private Transform _boss;
        private BossMovementModule _movement;
        private Transform _targetingTarget;
        private Vector3 _targetingPosition;
        private bool _targetingLocked;

        public Transform Owner => _boss;
        public Transform TargetingTarget => _targetingTarget;
        public bool HasTargetingTarget => _targetingTarget != null &&
            _targetingTarget.gameObject.activeInHierarchy;
        public bool CanMove => _movement != null && _movement.CanMove;
        public bool IsAttackMovementActive => _movement != null &&
            (_movement.IsJumping || _movement.IsDashing);
        public float CurrentAnimatedJumpHeight =>
            _movement?.CurrentAnimatedJumpHeight ?? 0f;

        // 공격 시작 시 대상만 보관하고, 위치는 애니메이션 이벤트 시점에 고정한다.
        public void BeginTargeting(Transform target)
        {
            _targetingTarget = target;
            _targetingPosition = default;
            _targetingLocked = false;
        }

        public bool CaptureTargetingPosition()
        {
            if (_targetingLocked || !HasTargetingTarget) return false;
            _targetingPosition = _targetingTarget.position;
            _targetingLocked = true;
            return true;
        }

        public bool TryGetTargetingPosition(out Vector3 position)
        {
            position = _targetingPosition;
            return _targetingLocked;
        }

        public void ClearTargetingPosition()
        {
            _targetingPosition = default;
            _targetingLocked = false;
        }

        public void EndTargeting()
        {
            ClearTargetingPosition();
            _targetingTarget = null;
        }

        public bool BeginJump(Vector3 destination, Transform target) =>
            _movement != null && _movement.BeginJumpTo(destination, target);

        public void SetJumpPose(float progress, float animatedHeight,
            float additionalHeight) =>
            _movement?.SetJumpPose(progress, animatedHeight, additionalHeight);

        public void CompleteJump() => _movement?.CompleteJump();

        public bool BeginDash(Transform target) =>
            _movement != null && _movement.BeginDash(target);

        public void SetDashDistance(float distance) =>
            _movement?.SetDashDistance(distance);

        public void EndDash() => _movement?.EndDash();

        public void StopAttackMovement() => _movement?.Stop();

        public void LockRecoveryRotation() => _movement?.LockRotation();

        public void UnlockRecoveryRotation() => _movement?.UnlockRotation();

        // 전달받은 위치의 지면 높이만 조회하고, 착지점 선택은 Flow가 맡는다.
        public bool TryProjectToGround(Vector3 position, out Vector3 groundPosition)
        {
            groundPosition = default;
            Vector3 origin = position + Vector3.up * GroundProbeHeight;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                    GroundProbeHeight * 2f, _landingGroundLayers,
                    QueryTriggerInteraction.Ignore)) return false;
            groundPosition = new Vector3(position.x, hit.point.y, position.z);
            return true;
        }

        // 전달받은 공격 프리팹을 보스 방향으로 생성하고 판정을 시작한다.
        public BossAttackInstance SpawnAttack(BossAttackInstance prefab, Vector3 position)
        {
            if (_boss == null || prefab == null)
            {
                Debug.LogError($"{name}: 공격 프리팹 또는 보스 참조가 없습니다.", this);
                return null;
            }

            BossAttackInstance instance = Instantiate(prefab, position, _boss.rotation);
            instance.Begin();
            return instance;
        }

        // 타깃을 중심으로 서로 닿지 않는 무작위 소환 위치를 고른다.
        public bool TryGetAttack7SpawnPositions(BossAttackSpawnSettings spawnSettings,
            float minSpacing,
            List<Vector3> positions)
        {
            if (spawnSettings == null || positions == null ||
                !_targetingLocked || !HasTargetingTarget)
                return false;
            CharacterController player = _targetingTarget.GetComponent<CharacterController>();
            if (player == null || !player.enabled ||
                !TryProjectToGround(_targetingPosition, out Vector3 groundTarget))
                return false;
            GetAttack7SpawnPositions(groundTarget, spawnSettings.SpawnCount,
                spawnSettings.SpawnRadius, minSpacing, player, positions);
            return positions.Count > 0;
        }

        private void GetAttack7SpawnPositions(Vector3 center, int spawnCount,
            float spawnRadius, float minSpacing,
            CharacterController player,
            List<Vector3> positions)
        {
            positions.Clear();
            float radius = Mathf.Max(0f, spawnRadius - minSpacing * 0.5f);
            if (radius <= 0f)
            {
                Debug.LogWarning($"{name}: Attack7 소환 반경이 프리팹의 충돌 범위보다 작습니다.", this);
                return;
            }

            float minSpacingSqr = minSpacing * minSpacing;
            Vector3 playerCenter = player.transform.TransformPoint(player.center);
            Vector3 playerScale = player.transform.lossyScale;
            float horizontalScale = Mathf.Max(Mathf.Abs(playerScale.x), Mathf.Abs(playerScale.z));
            float playerClearance = minSpacing * 0.5f +
                (player.radius + player.skinWidth) * horizontalScale + 0.1f;
            float playerClearanceSqr = playerClearance * playerClearance;
            int maxAttempts = Mathf.Max(64, spawnCount * 64);
            for (int attempt = 0; attempt < maxAttempts &&
                 positions.Count < spawnCount; attempt++)
            {
                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                float distance = radius * Mathf.Sqrt(UnityEngine.Random.value);
                Vector3 candidate = center + new Vector3(
                    Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                Vector3 toPlayer = candidate - playerCenter;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude < playerClearanceSqr) continue;

                bool overlaps = false;
                foreach (Vector3 placed in positions)
                {
                    Vector3 offset = candidate - placed;
                    offset.y = 0f;
                    if (offset.sqrMagnitude >= minSpacingSqr) continue;
                    overlaps = true;
                    break;
                }
                if (!overlaps) positions.Add(candidate);
            }

            if (positions.Count < spawnCount)
                Debug.LogWarning($"{name}: Attack7 소환 범위에 {spawnCount}마리를 " +
                    $"겹치지 않게 배치할 수 없어 {positions.Count}마리만 생성합니다.", this);
        }

        public void Bind(Transform boss, BossMovementModule movement)
        {
            _boss = boss;
            _movement = movement;
        }

        // 전투 대상 후보의 레이어와 활성 상태만 확인한다. 대상 유지와 선택은 Combat 상태가 판단한다.
        public bool IsCombatTargetValid(Transform target) =>
            target != null && target.gameObject.activeInHierarchy &&
            (_targetLayers.value & (1 << target.gameObject.layer)) != 0;

        // Player 레이어의 충돌체에서 신체 중심으로 사용할 CharacterController 루트를 수집한다.
        public void FindCombatTargets(float maxDistance, List<Transform> targets)
        {
            if (targets == null) return;
            targets.Clear();
            if (!isActiveAndEnabled || _boss == null || maxDistance <= 0f ||
                _targetLayers.value == 0) return;

            int count = Physics.OverlapSphereNonAlloc(_boss.position, maxDistance,
                _targetCandidates, _targetLayers, QueryTriggerInteraction.Ignore);
            float maxSqrDistance = maxDistance * maxDistance;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _targetCandidates[i];
                if (candidate == null || !candidate.enabled) continue;

                CharacterController controller = candidate.GetComponentInParent<CharacterController>();
                if (controller == null || !controller.enabled ||
                    !IsCombatTargetValid(controller.transform)) continue;

                Vector3 offset = controller.transform.position - _boss.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > maxSqrDistance ||
                    targets.Contains(controller.transform)) continue;
                targets.Add(controller.transform);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Camera.current == null || Camera.current.cameraType != CameraType.SceneView) return;

            Transform owner = _boss != null ? _boss : GetComponentInParent<BossRoot>(true)?.transform;
            if (owner == null) return;
            Vector3 center = owner.position + Vector3.up * 0.1f;
            Vector3 forward = Vector3.ProjectOnPlane(owner.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            BossAttackPattern[] patterns = GetComponentsInChildren<BossAttackPattern>(true);
            for (int i = 0; i < patterns.Length; i++)
            {
                BossAttackCastConditions conditions = patterns[i].Conditions;
                if (conditions != null &&
                    (conditions.ShowSelectionDistance || conditions.ShowCastDistance))
                    DrawCastConditionsGizmo(center, forward, conditions, i);
                foreach (BossAttackMovementSettings movement in patterns[i].PhysicalMovements)
                    if (movement != null && movement.Type == EBossAttackMovementType.Dash &&
                        movement.AdditionalDistance > 0f)
                        DrawDashAdditionalDistanceGizmo(center, forward,
                            patterns[i].AttackId, movement);
            }

        }

        private static void DrawDashAdditionalDistanceGizmo(Vector3 start,
            Vector3 forward, string attackId, BossAttackMovementSettings movement)
        {
            Vector3 end = start + forward * movement.AdditionalDistance;
            Color previous = Handles.color;
            Handles.color = new Color(1f, 0.65f, 0f, 1f);
            Handles.DrawLine(start, end);
            Handles.DrawWireDisc(end, Vector3.up, 0.25f);
            Handles.Label(end + Vector3.up * 0.35f,
                $"{attackId}/{movement.MovementId} extra +{movement.AdditionalDistance:0.##}m");
            Handles.color = previous;
        }

        private static void DrawCastConditionsGizmo(Vector3 center, Vector3 forward,
            BossAttackCastConditions conditions, int index)
        {
            float min = conditions.MinSelectionDistance;
            float max = conditions.MaxSelectionDistance;
            float cast = conditions.CastDistance;
            Color previous = Handles.color;

            if (conditions.ShowSelectionDistance && max > 0f)
            {
                Handles.color = conditions.SelectionDistanceColor;
                Handles.DrawWireDisc(center, Vector3.up, max);
                if (min > 0f)
                    Handles.DrawWireDisc(center, Vector3.up, min);
                Handles.Label(center + forward * max +
                    Vector3.up * (0.5f + index * 0.25f),
                    $"{conditions.AttackId} select {min:0.#}-{max:0.#}m");
            }

            if (conditions.ShowCastDistance && cast > 0f)
            {
                Handles.color = conditions.CastDistanceColor;
                Handles.DrawWireDisc(center + Vector3.up * 0.08f,
                    Vector3.up, cast);
                Handles.Label(center + forward * cast +
                    Vector3.up * (0.85f + index * 0.25f),
                    $"{conditions.AttackId} cast <= {cast:0.#}m");
            }

            Handles.color = previous;
        }

#endif
    }
}
