using alpha.ingame.input;
using UnityEngine;
namespace alpha.ingame.player
{
    public class JumpState : LocomotionState
    {
        public JumpState(PlayerRoot p_playerCore) : base(p_playerCore){}

        public override void EnterState()
        {
            _LocomotionModule.Jump();
            _LocomotionModule.AirMove(AlphaInput.Instance.MoveInput);
        }
        public override void UpdateState()
        {
            AlphaInput input = AlphaInput.Instance;
            Vector2 moveInput = input.MoveInput;

            // 상승 중 2단 점프는 같은 Jump 상태에서 바로 실행한다.
            if (input.IsJump && _LocomotionModule.CanAirJump)
                _LocomotionModule.Jump();

            _LocomotionModule.AirMove(moveInput); // 이동 후 수직 속도와 접지 상태 확인

            if (_LocomotionModule.IsGrounded)
            {
                _StateMachine.ChangeState(ELocomotionState.Land);
                return;
            }

            // 상승이 끝나고 현재 점프 클립의 낙하 구간에 도달하면 Fall로 전환한다.
            if (_LocomotionModule.VelocityY <= 0f &&
                (_AnimationView == null || _AnimationView.CanEnterFallFromJump()))
                _StateMachine.ChangeState(ELocomotionState.Fall);
        }
        public override void ExitState()
        {

        }
    }
}
