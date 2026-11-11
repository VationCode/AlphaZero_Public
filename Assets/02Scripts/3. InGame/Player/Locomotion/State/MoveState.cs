using alpha.ingame.input;
using UnityEngine;
namespace alpha.ingame.player
{
    public class MoveState : LocomotionState
    {
        public MoveState(PlayerRoot p_playerCore) : base(p_playerCore){}

        public override void EnterState()
        {
        }
        public override void UpdateState()
        {
            AlphaInput input = AlphaInput.Instance;
            Vector2 moveInput = input.MoveInput;
            bool isWalk = input.IsWalk;
            bool isCombat = IsCombatStanceActive;
            bool isJump = input.IsJump;

            _LocomotionModule.SetMoveType(moveInput, isWalk, isCombat);

            if (isJump)
            {
                if (input.IsJump && _LocomotionModule.CanGroundJump)
                {
                    _StateMachine.ChangeState(ELocomotionState.Jump);
                    return;
                }
            }

            if (_StateMachine.TryEnterMobility())
                return;

            _LocomotionModule.Move(moveInput);

            // 지상 이동 중 턱을 벗어나면 입력 상태보다 낙하를 우선한다.
            if (_LocomotionModule.IsFalling)
            {
                _StateMachine.ChangeState(ELocomotionState.Fall);
                return;
            }

            // 전투 자세일 때만 입력 없이 CombatMove Tree의 대기를 유지한다.
            if (moveInput.sqrMagnitude <= 0.0001f && !isCombat)
            {
                _StateMachine.ChangeState(ELocomotionState.Idle);
                return;
            }
        }
        public override void ExitState(){}
    }
}
