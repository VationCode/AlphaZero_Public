namespace alpha.ingame.player
{
    public abstract class LocomotionState
    {
        protected PlayerRoot _PlayerRoot;
        protected LocomotionStateFlow _StateMachine;
        protected LocomotionModule _LocomotionModule;
        protected AnimationView _AnimationView;
        protected bool IsCombatStanceActive => _PlayerRoot.CombatModule?.IsCombatStanceActive ?? false;

        public LocomotionState(PlayerRoot p_playerCore)
        {
            _PlayerRoot = p_playerCore;
            _StateMachine = _PlayerRoot.LocomotionStateFlow;
            _LocomotionModule = _PlayerRoot.LocomotionModule;
            _AnimationView = _PlayerRoot.AnimationView;
        }

        public abstract void EnterState();
        public abstract void UpdateState();
        public abstract void ExitState();
    }
}
