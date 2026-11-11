namespace alpha.ingame.enemy
{
    /// <summary>사망 후 이동과 전투를 종료한다.</summary>
    public sealed class EnemyDeadState : EnemyState
    {
        public EnemyDeadState(EnemyRoot enemy, EnemyStateFlow flow) : base(enemy, flow) { }

        public override void Enter()
        {
            Enemy.Locomotion?.Stop();
            Enemy.Combat?.Stop();
        }
    }
}
