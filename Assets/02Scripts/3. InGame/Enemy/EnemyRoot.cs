using alpha.ingame.item;
using UnityEngine;

namespace alpha.ingame.enemy
{
    /// <summary>적 정보와 자식 모듈을 연결하고 피격을 전달한다.</summary>
    public sealed class EnemyRoot : MonoBehaviour, IDamageable
    {
        public EnemyInfo Info { get; private set; }
        public EnemyHealth Health { get; private set; }
        public EnemyStateFlow StateFlow { get; private set; }
        public EnemyLocomotionModule Locomotion { get; private set; }
        public EnemyCombatModule Combat { get; private set; }
        public EnemyChaseModule Chase { get; private set; }
        public EnemyAnimationView AnimationView { get; private set; }
        public Vector3 ActivityOrigin { get; private set; }
        public bool IsDead => Health != null && Health.IsDead;

        private Vector3 _patrolCenter;
        private Vector2 _patrolSize;
        private bool _hasPatrolArea;

        private void Awake()
        {
            ActivityOrigin = transform.position;
            Info = GetComponent<EnemyInfo>();
            Health = GetComponentInChildren<EnemyHealth>(true);
            StateFlow = GetComponentInChildren<EnemyStateFlow>(true);
            Locomotion = GetComponentInChildren<EnemyLocomotionModule>(true);
            Combat = GetComponentInChildren<EnemyCombatModule>(true);
            Chase = GetComponentInChildren<EnemyChaseModule>(true);
            AnimationView = GetComponentInChildren<EnemyAnimationView>(true);
            if (Health != null) Health.Died += HandleDied;
            if (Info == null || Health == null)
                Debug.LogError($"{name}: EnemyInfo와 Health/EnemyHealth가 필요합니다.", this);
            if (Chase == null)
                Debug.LogError($"{name}: EnemyChaseModule이 필요합니다.", this);
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Died -= HandleDied;
        }

        public float TakeDamage(in AttackDamage damage)
        {
            // 무기 충돌은 루트에서 수신하고 체력 처리는 Health 자식에 위임한다.
            return Health != null ? Health.ApplyDamage(damage) : 0f;
        }

        /// <summary>외부에서 받은 순찰 영역 값만 보관한다.</summary>
        public void ConfigurePatrolArea(Vector3 center, float sizeX, float sizeZ)
        {
            _patrolCenter = center;
            _patrolSize = new Vector2(Mathf.Max(0.01f, sizeX), Mathf.Max(0.01f, sizeZ));
            _hasPatrolArea = true;
        }

        public bool TryGetPatrolArea(out Vector3 center, out Vector2 size)
        {
            center = _patrolCenter;
            size = _patrolSize;
            return _hasPatrolArea;
        }

        public void SetActivityOrigin(Vector3 position) => ActivityOrigin = position;

        private void HandleDied()
        {
            StateFlow?.ChangeState(EEnemyState.Dead);
            Locomotion?.Stop();
            Combat?.Stop();
        }
    }
}
