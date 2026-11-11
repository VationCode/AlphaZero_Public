using alpha.ingame.input;

namespace alpha.ingame.player
{
    /// <summary>기동 상태 전환을 담당하고 아레테별 동작은 모델 컴포넌트에 맡긴다.</summary>
    public class MobilityState : LocomotionState
    {
        private Arete _arete;

        public MobilityState(PlayerRoot playerCore) : base(playerCore) { }

        public override void EnterState()
        {
            _arete = _PlayerRoot.CurrentArete;
            _arete?.BeginMobility(AlphaInput.Instance, IsCombatStanceActive);
        }

        public override void UpdateState()
        {
            if (_arete == null) return;

            ELocomotionState? nextState = _arete.UpdateMobility(AlphaInput.Instance, IsCombatStanceActive);
            if (nextState.HasValue)
                _StateMachine.ChangeState(nextState.Value);
        }

        public override void ExitState()
        {
            _arete?.EndMobility();
            _arete = null;

            AlphaInput input = AlphaInput.Instance;
            if (input != null)
                _LocomotionModule.SetMoveType(input.MoveInput, input.IsWalk, IsCombatStanceActive);
        }
    }
}
