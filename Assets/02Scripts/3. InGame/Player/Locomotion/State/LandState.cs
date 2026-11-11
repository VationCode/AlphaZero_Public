using UnityEngine;
namespace alpha.ingame.player
{
    public class LandState : LocomotionState
    {
        private float _landEndTime;

        public LandState(PlayerRoot p_playerCore) : base(p_playerCore) { }

        public override void EnterState()
        {
            _LocomotionModule.ResetJumpCount();
            _AnimationView.LandAnim();
            _landEndTime = Time.time + _LocomotionModule.LandDuration;
        }
        public override void UpdateState()
        {
            _LocomotionModule.HoldGround(); // 짧은 착지 구간 동안 접지와 정지를 유지한다.

            if (Time.time < _landEndTime)
                return;

            _StateMachine.ChangeState(ELocomotionState.Idle);
        }
        public override void ExitState()
        {

        }
    }
}
