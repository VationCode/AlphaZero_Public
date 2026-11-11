using alpha.ingame.input;
using UnityEngine;
namespace alpha.ingame.player
{
    public class FallState : LocomotionState
    {
        public FallState(PlayerRoot p_playerCore) : base(p_playerCore) { }

        public override void EnterState()
        {
            if (_AnimationView != null)
                _AnimationView.FallAnim();
        }
        public override void UpdateState()
        {
            AlphaInput input = AlphaInput.Instance;
            Vector2 moveInput = input.MoveInput;

            // 하강 중 남은 2단 점프: JumpState.EnterState가 도약과 이동을 실행한다.
            if (input.IsJump && _LocomotionModule.CanAirJump)
            {
                _StateMachine.ChangeState(ELocomotionState.Jump);
                return;
            }

            _LocomotionModule.AirMove(moveInput);

            if (_LocomotionModule.IsGrounded)
                _StateMachine.ChangeState(ELocomotionState.Land);
        }
        public override void ExitState()
        {

        }
    }
}
