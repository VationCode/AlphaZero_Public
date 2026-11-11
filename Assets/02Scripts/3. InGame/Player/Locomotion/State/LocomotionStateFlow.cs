using System.Collections.Generic;
using alpha.ingame.input;
using UnityEngine;

namespace alpha.ingame.player
{
    public enum ELocomotionState
    {
        Idle,
        Move,
        Jump,
        Fall,
        Land,
        Mobility
    }

    public class LocomotionStateFlow : MonoBehaviour
    {
        private PlayerRoot _playerCore;
        private Dictionary<ELocomotionState, LocomotionState> _stateDict;

        private LocomotionState _currentState;

        public ELocomotionState CurrentState { get; private set; } = ELocomotionState.Idle;

        private void Awake()
        {
            _playerCore = GetComponentInParent<PlayerRoot>(true);
        }

        private void Start()
        {
            _stateDict = new Dictionary<ELocomotionState, LocomotionState>
            {
                { ELocomotionState.Idle, new IdleState(_playerCore) },
                { ELocomotionState.Move, new MoveState(_playerCore) },
                { ELocomotionState.Jump, new JumpState(_playerCore) },
                { ELocomotionState.Fall, new FallState(_playerCore) },
                { ELocomotionState.Land, new LandState(_playerCore) },
                { ELocomotionState.Mobility, new MobilityState(_playerCore) }
            };
            ChangeState(ELocomotionState.Idle);
        }

        private void Update()
        {
            // Action에서 강제 이동한 프레임에는 입력 이동을 실행하지 않는다.
            if (_playerCore?.ActionStateFlow?.DidMoveByHitThisFrame == true) return;

            if (_playerCore?.ActionStateFlow?.IsActionBlocked == true)
            {
                _playerCore.LocomotionModule?.HoldGround();
                return;
            }

            if (_playerCore?.SkillStateFlow?.IsMovementLocked == true)
            {
                // 이동 잠금 중에는 수평 이동을 멈추고 접지용 중력만 유지한다.
                _playerCore.LocomotionModule?.HoldGround();
                return;
            }

            _currentState?.UpdateState();
        }

        public void ChangeState(ELocomotionState newState)
        {
            if (_currentState == _stateDict[newState]) return;

            // 공중 및 착지 상태에 들어가기 전에 공격과 Aim/Scope를 즉시 종료한다.
            if (newState == ELocomotionState.Jump ||
                newState == ELocomotionState.Fall ||
                newState == ELocomotionState.Land)
            {
                _playerCore.CombatModule?.CancelSecondary();
                _playerCore.CombatStateFlow?.CancelForAirState();
            }

            _currentState?.ExitState();
            _currentState = _stateDict[newState];
            CurrentState = newState;
            _currentState?.EnterState();
        }

        public bool TryEnterMobility()
        {
            AlphaInput input = AlphaInput.Instance;
            if (input == null || !input.IsMobilityPressed ||
                _playerCore.CurrentArete == null || !_playerCore.LocomotionModule.IsGrounded)
                return false;

            // 기동이 정의되지 않은 아레테는 입력을 소비하지 않는다.
            if (!_playerCore.CurrentArete.CanUseMobility)
                return false;

            // 실리온 기동에만 부스터 잔량 조건을 적용한다.
            if (_playerCore.CurrentArete is SilyonArete && !_playerCore.LocomotionModule.CanUseBoost)
                return false;

            if (_playerCore.CombatModule?.IsAimOrScopeActive == true)
                return false;

            ChangeState(ELocomotionState.Mobility);
            return true;
        }
    }
}
