namespace alpha.ingame.player
{
    /// <summary>스킬 상태가 공통으로 사용하는 상태 흐름 참조.</summary>
    public abstract class SkillState
    {
        protected readonly SkillStateFlow Flow;

        protected SkillState(SkillStateFlow flow) => Flow = flow;

        public abstract void EnterState();
        public abstract void UpdateState();
        public abstract void ExitState();
    }
}
