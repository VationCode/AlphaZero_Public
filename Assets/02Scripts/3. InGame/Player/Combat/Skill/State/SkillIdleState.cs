namespace alpha.ingame.player
{
    /// <summary>숫자 키 입력을 스킬 슬롯 요청으로 전달한다.</summary>
    public sealed class SkillIdleState : SkillState
    {
        public SkillIdleState(SkillStateFlow flow) : base(flow) { }

        public override void EnterState() { }

        public override void UpdateState()
        {
            if (Flow.PressedSlotThisFrame > 0)
                Flow.RequestSkill(Flow.PressedSlotThisFrame);
        }

        public override void ExitState() { }
    }
}
