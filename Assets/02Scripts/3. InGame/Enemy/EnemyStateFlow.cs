using UnityEngine;

namespace alpha.ingame.enemy
{
    public enum EEnemyState
    {
        Idle,
        Chase,
        Dead,
        Patrol
    }

    /// <summary>적의 현재 상태를 실행하고 전환한다.</summary>
    public sealed class EnemyStateFlow : MonoBehaviour
    {
        private EnemyRoot _enemy;
        private EnemyState _current;
        private EnemyState _idle;
        private EnemyPatrolState _patrol;
        private EnemyState _chase;
        private EnemyState _dead;

        public EEnemyState CurrentState { get; private set; } = EEnemyState.Idle;
        public EEnemyPatrolPhase PatrolPhase => _patrol?.Phase ?? EEnemyPatrolPhase.Selecting;
        public bool IsReturningFromChase => _patrol?.IsReturningFromChase ?? false;
        public EnemyPatrolArea PatrolArea { get; private set; }

        private void Awake()
        {
            _enemy = GetComponentInParent<EnemyRoot>(true);
            if (_enemy == null)
            {
                Debug.LogError($"{name}: 부모에 EnemyRoot가 필요합니다.", this);
                enabled = false;
                return;
            }

            PatrolArea = _enemy.GetComponentInChildren<EnemyPatrolArea>(true);
            _idle = new EnemyIdleState(_enemy, this);
            _patrol = new EnemyPatrolState(_enemy, this);
            _chase = new EnemyChaseState(_enemy, this);
            _dead = new EnemyDeadState(_enemy, this);
        }

        private void Start() => ChangeState(_enemy.IsDead ? EEnemyState.Dead : EEnemyState.Idle);

        private void Update()
        {
            if (_enemy.IsDead && CurrentState != EEnemyState.Dead)
                ChangeState(EEnemyState.Dead);
            if (!_enemy.IsDead) _enemy.Combat?.RefreshTarget();
            _current?.Tick();
        }

        public void ChangeState(EEnemyState next)
        {
            if (_enemy == null || (_enemy.IsDead && next != EEnemyState.Dead)) return;
            EnemyState state = next switch
            {
                EEnemyState.Idle => _idle,
                EEnemyState.Patrol => _patrol,
                EEnemyState.Chase => _chase,
                EEnemyState.Dead => _dead,
                _ => null
            };
            if (state == null || state == _current) return;

            _current?.Exit();
            _current = state;
            CurrentState = next;
            _current.Enter();
        }

        public void ReturnToPatrolOrigin()
        {
            if (PatrolArea == null || !PatrolArea.IsReady ||
                _enemy.Locomotion == null || !_enemy.Locomotion.CanMove)
            {
                // 복귀 자체가 불가능하면 재감지 차단을 남기지 않는다.
                _enemy.Combat?.ClearReturnTargetSuppression();
                ChangeState(EEnemyState.Idle);
                return;
            }

            ChangeState(EEnemyState.Patrol);
            _patrol.RequestReturnToStart();
        }

        public void EndReturnToPatrolOrigin() =>
            _enemy?.Combat?.ClearReturnTargetSuppression();

        private void OnDisable()
        {
            _enemy?.Locomotion?.Stop();
            _enemy?.Chase?.End();
            _enemy?.Combat?.CancelMeleeAttack();
        }
    }
}
