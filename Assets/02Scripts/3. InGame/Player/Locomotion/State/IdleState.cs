using alpha.ingame.input;
using UnityEngine;

namespace alpha.ingame.player
{
    public class IdleState : LocomotionState
    {
        public IdleState(PlayerRoot p_playerCore) : base(p_playerCore){}

        public override void EnterState()
        {
            _LocomotionModule.SetMoveType(Vector2.zero, false, false);
        }
        public override void UpdateState()
        {
            AlphaInput input = AlphaInput.Instance;
            Vector2 moveInput = input.MoveInput;
            bool isJump = input.IsJump;

            if (isJump)
            {
                if(input.IsJump && _LocomotionModule.CanGroundJump)
                {
                    _StateMachine.ChangeState(ELocomotionState.Jump);
                    return;
                }
            }

            if (_StateMachine.TryEnterMobility())
                return;

            // 정지 중에도 중력을 적용한 뒤 발밑이 사라졌는지 확인한다.
            _LocomotionModule.Move(Vector2.zero);
            if (_LocomotionModule.IsFalling)
            {
                _StateMachine.ChangeState(ELocomotionState.Fall);
                return;
            }

            if (moveInput.sqrMagnitude > 0.0001f || IsCombatStanceActive)
            {
                _StateMachine.ChangeState(ELocomotionState.Move);
                return;
            }
        }
        public override void ExitState(){}
    }
}
