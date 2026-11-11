namespace alpha.ingame.enemy
{
    /// <summary>대기하다가 플레이어를 발견하면 추적한다.</summary>
    public sealed class EnemyIdleState : EnemyState
    {
        public EnemyIdleState(EnemyRoot enemy, EnemyStateFlow flow) : base(enemy, flow) { }

        public override void Enter() => Enemy.Locomotion?.Stop();

        public override void Tick()
        {
            if (Enemy.Combat?.Target != null)
                Flow.ChangeState(EEnemyState.Chase);
            else if (Flow.PatrolArea != null && Flow.PatrolArea.IsReady &&
                     Enemy.Locomotion != null && Enemy.Locomotion.CanMove)
                Flow.ChangeState(EEnemyState.Patrol);
        }
    }
}
