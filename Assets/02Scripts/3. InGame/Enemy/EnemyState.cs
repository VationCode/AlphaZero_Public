namespace alpha.ingame.enemy
{
    /// <summary>적 상태의 시작, 매 프레임 동작, 종료를 정의한다.</summary>
    public abstract class EnemyState
    {
        protected readonly EnemyRoot Enemy;
        protected readonly EnemyStateFlow Flow;

        protected EnemyState(EnemyRoot enemy, EnemyStateFlow flow)
        {
            Enemy = enemy;
            Flow = flow;
        }

        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }
}
