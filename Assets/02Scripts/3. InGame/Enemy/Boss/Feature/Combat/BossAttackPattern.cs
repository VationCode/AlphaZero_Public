using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace alpha.ingame.enemy.boss
{
    public enum EBossAttackMovementType
    {
        Jump,
        Dash
    }

    [Serializable]
    public sealed class BossAttackMovementSettings
    {
        [SerializeField] private string _movementId;
        [SerializeField] private EBossAttackMovementType _type;
        [Tooltip("Dash에서 애니메이션 전방 이동량에 더할 거리입니다.")]
        [SerializeField] private float _additionalDistance;
        [Tooltip("Jump에서 애니메이션 높이에 더할 최고 높이입니다.")]
        [SerializeField, Min(0f)] private float _additionalHeight;

        public string MovementId => _movementId;
        public EBossAttackMovementType Type => _type;
        public float AdditionalDistance => _additionalDistance;
        public float AdditionalHeight => Mathf.Max(0f, _additionalHeight);

        public void Validate() => _additionalHeight = Mathf.Max(0f, _additionalHeight);
    }

    [Serializable]
    public sealed class BossAttackSpawnSettings
    {
        [Tooltip("한 생성 이벤트에서 만들 공격 객체 수입니다. 일반 이펙트에는 적용하지 않습니다.")]
        [SerializeField, Min(1)] private int _spawnCount = 1;
        [Tooltip("대상 위치를 중심으로 소환체 전체가 들어갈 범위의 반경입니다.")]
        [SerializeField, Min(0f)] private float _spawnRadius;
        [SerializeField] private bool _showSpawnRadius;

        public int SpawnCount => Mathf.Max(1, _spawnCount);
        public float SpawnRadius => Mathf.Max(0f, _spawnRadius);
        public bool ShowSpawnRadius => _showSpawnRadius;

        public void Validate()
        {
            _spawnCount = Mathf.Max(1, _spawnCount);
            _spawnRadius = Mathf.Max(0f, _spawnRadius);
        }
    }

    /// <summary>보스 공격 한 패턴의 선택·생성·이동 설정과 대표 공격 객체를 보관한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BossAttackPattern : MonoBehaviour
    {
        [SerializeField] private BossAttackCastConditions _conditions = new();
        [SerializeField] private GameObject _primaryAttackObject;
        [SerializeField] private BossAttackSpawnSettings _spawnSettings = new();
        [FormerlySerializedAs("_movements")]
        [InspectorName("공격 몸체 이동")]
        [SerializeField] private List<BossAttackMovementSettings> _physicalMovements = new();

        public BossAttackCastConditions Conditions => _conditions;
        public string AttackId => _conditions?.AttackId;
        public bool IsEnabled => isActiveAndEnabled && _conditions?.IsEnabled == true;
        public GameObject PrimaryAttackObject => _primaryAttackObject;
        public BossAttackSpawnSettings SpawnSettings => _spawnSettings;
        public IReadOnlyList<BossAttackMovementSettings> PhysicalMovements => _physicalMovements ??
            (IReadOnlyList<BossAttackMovementSettings>)Array.Empty<BossAttackMovementSettings>();
        public BossAttackInstance AttackInstance => _primaryAttackObject != null
            ? _primaryAttackObject.GetComponent<BossAttackInstance>() : null;

        public bool TryGetMovement(string movementId,
            out BossAttackMovementSettings movement)
        {
            movement = null;
            if (string.IsNullOrWhiteSpace(movementId) || _physicalMovements == null) return false;
            foreach (BossAttackMovementSettings candidate in _physicalMovements)
            {
                if (candidate == null || candidate.MovementId != movementId) continue;
                movement = candidate;
                return true;
            }
            return false;
        }

        public bool HasMovementType(EBossAttackMovementType type)
        {
            if (_physicalMovements == null) return false;
            foreach (BossAttackMovementSettings movement in _physicalMovements)
                if (movement != null && movement.Type == type) return true;
            return false;
        }

        private void OnValidate()
        {
            _conditions?.Validate();
            _spawnSettings?.Validate();
            if (_conditions == null || string.IsNullOrWhiteSpace(AttackId))
                Debug.LogError($"{name}: Attack ID가 비어 있습니다.", this);

            if (_physicalMovements == null) return;
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (BossAttackMovementSettings movement in _physicalMovements)
            {
                if (movement == null || string.IsNullOrWhiteSpace(movement.MovementId) ||
                    !ids.Add(movement.MovementId))
                    Debug.LogError($"{name}: 이동 ID가 비어 있거나 중복되었습니다: " +
                        $"{movement?.MovementId}", this);
                movement?.Validate();
            }
        }

#if UNITY_EDITOR
        // Attack7의 실제 타깃 위치가 있을 때 소환 범위를 표시한다.
        private void OnDrawGizmos()
        {
            if (AttackId != "Attack7" || _spawnSettings == null ||
                !_spawnSettings.ShowSpawnRadius || _spawnSettings.SpawnRadius <= 0f ||
                !Application.isPlaying || Camera.current == null ||
                Camera.current.cameraType != CameraType.SceneView) return;

            BossCombatFlow flow = GetComponentInParent<BossRoot>(true)?.CombatFlow;
            if (flow == null || flow.CurrentAttackId != AttackId) return;
            if (!flow.TryGetTargetingPosition(out Vector3 targetPosition))
            {
                if (flow.CurrentTarget == null) return;
                targetPosition = flow.CurrentTarget.position;
            }

            float radius = _spawnSettings.SpawnRadius;
            Vector3 center = targetPosition + Vector3.up * 0.1f;
            Color previous = Handles.color;
            Handles.color = new Color(1f, 0.35f, 0.8f, 1f);
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.Label(center + Vector3.right * radius +
                Vector3.up * 0.35f, $"Attack7 summon {radius:0.#}m");
            Handles.color = previous;
        }
#endif
    }
}
