using alpha.ingame.input;

namespace alpha.ingame.player
{
    /// <summary>실행 중인 스킬의 확정 또는 취소 입력을 전달한다.</summary>
    public sealed class SkillExecuteState : SkillState
    {
        public SkillExecuteState(SkillStateFlow flow) : base(flow) { }

        public override void EnterState() { }

        public override void UpdateState()
        {
            AlphaInput input = AlphaInput.Instance;
            // 설치 위치를 고르는 중에는 같은 번호키로도 취소한다.
            if (input.IsSecondaryPressed || Flow.PressedSlotThisFrame == Flow.SelectedSlot)
            {
                Flow.RequestCancel();
                return;
            }

            // 설치형 스킬의 위치 검증 등 실제 확정 조건은 스킬 실행 로직이 판단한다.
            if (input.IsPrimaryPressed)
                Flow.RequestExecuteConfirm();
        }

        public override void ExitState() { }
    }

    /// <summary>스킬 모션 중에는 설치 입력을 무시하고 취소만 받는다.</summary>
    public sealed class SkillCastingState : SkillState
    {
        public SkillCastingState(SkillStateFlow flow) : base(flow) { }

        public override void EnterState() { }

        public override void UpdateState()
        {
            if (AlphaInput.Instance?.IsSecondaryPressed == true)
                Flow.RequestCancel();
        }

        public override void ExitState() { }
    }
}
