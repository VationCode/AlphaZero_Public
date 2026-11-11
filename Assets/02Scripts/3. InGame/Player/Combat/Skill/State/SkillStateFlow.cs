using System;
using System.Collections.Generic;
using alpha.ingame.input;
using UnityEngine;

namespace alpha.ingame.player
{
    public enum ESkillState
    {
        Idle,
        Execute,
        Casting
    }

    /// <summary>스킬 슬롯 입력과 실행 상태를 관리한다.</summary>
    [DefaultExecutionOrder(-200)]
    public class SkillStateFlow : MonoBehaviour
    {
        private Dictionary<ESkillState, SkillState> _states;
        private SkillState _currentState;
        private int _primaryConsumedFrame = -1;
        private bool _movementUnlockScheduled;
        private float _movementUnlockAt;
        private PlayerRoot _playerCore;

        public ESkillState CurrentState { get; private set; } = ESkillState.Idle;
        public bool IsExecuting => _currentState != null && CurrentState != ESkillState.Idle;
        public bool IsCasting => _currentState != null && CurrentState == ESkillState.Casting;
        public bool IsMovementLocked =>
            (IsCasting && !_movementUnlockScheduled) ||
            (_movementUnlockScheduled && Time.time < _movementUnlockAt);
        public bool IsPrimaryConsumedThisFrame => _primaryConsumedFrame == Time.frameCount;
        public int SelectedSlot { get; private set; }
        public int PressedSlotThisFrame { get; private set; }

        public event Action<int> SkillRequested;
        public event Action<int> ExecuteConfirmRequested;
        public event Action<int> ExecuteCanceled;
        public event Action<int> CancelRequested;
        public event Action ExecutionEnded;

        private void Awake()
        {
            _playerCore = GetComponentInParent<PlayerRoot>(true);
            _states = new Dictionary<ESkillState, SkillState>
            {
                { ESkillState.Idle, new SkillIdleState(this) },
                { ESkillState.Execute, new SkillExecuteState(this) },
                { ESkillState.Casting, new SkillCastingState(this) }
            };
        }

        private void OnEnable() => ChangeState(ESkillState.Idle);

        private void Update()
        {
            if (_playerCore?.ActionStateFlow?.IsActionBlocked == true)
            {
                if (IsExecuting) CancelExecute();
                return;
            }

            AlphaInput input = AlphaInput.Instance;
            PressedSlotThisFrame = input?.SkillPressedSlotThisFrame ?? 0;

            if (input == null || input.IsQuickSlotOpen)
            {
                if (IsExecuting) CancelExecute();
                return;
            }

            _currentState?.UpdateState();
        }

        private void OnDisable()
        {
            if (IsExecuting) CancelExecute();
            _currentState?.ExitState();
            _currentState = null;
            CurrentState = ESkillState.Idle;
            SelectedSlot = 0;
            PressedSlotThisFrame = 0;
            _primaryConsumedFrame = -1;
            _movementUnlockScheduled = false;
            _movementUnlockAt = 0f;
        }

        // 슬롯의 스킬이 실행 가능한지 판단한 뒤 호출한다.
        public bool BeginExecute(int slot)
        {
            if (slot < 1 || slot > 4 || _currentState == null || CurrentState != ESkillState.Idle)
                return false;

            SelectedSlot = slot;
            _movementUnlockScheduled = false;
            ChangeState(ESkillState.Execute);
            return true;
        }

        public bool BeginCast()
        {
            if (CurrentState != ESkillState.Execute) return false;
            _movementUnlockScheduled = false;
            ChangeState(ESkillState.Casting);
            return true;
        }

        // 자신에게 발동하는 스킬은 위치 선택 상태 없이 시전 상태로 들어간다.
        public bool BeginSelfCast(int slot)
        {
            if (slot < 1 || slot > 4 || _currentState == null || CurrentState != ESkillState.Idle)
                return false;

            SelectedSlot = slot;
            _movementUnlockScheduled = false;
            ChangeState(ESkillState.Casting);
            return true;
        }

        // 실행 이벤트 이후 지정한 시간이 지나면 애니메이션 진행과 별개로 이동을 허용한다.
        public void ScheduleMovementUnlock(float delay)
        {
            if (!IsCasting) return;
            _movementUnlockAt = Time.time + Mathf.Max(0f, delay);
            _movementUnlockScheduled = true;
        }

        public void CompleteExecute()
        {
            if (!IsExecuting) return;
            ChangeState(ESkillState.Idle);
            SelectedSlot = 0;
            ExecutionEnded?.Invoke();
        }

        public void CancelExecute()
        {
            if (!IsExecuting) return;
            _movementUnlockScheduled = false;
            int slot = SelectedSlot;
            ChangeState(ESkillState.Idle);
            SelectedSlot = 0;
            ExecuteCanceled?.Invoke(slot);
            ExecutionEnded?.Invoke();
        }

        // 입력으로 취소한 경우만 알린다. 내부 실패나 비활성화에는 취소음을 내지 않는다.
        public void RequestCancel()
        {
            if (!IsExecuting) return;
            int slot = SelectedSlot;
            CancelExecute();
            CancelRequested?.Invoke(slot);
        }

        public void RequestSkill(int slot) => SkillRequested?.Invoke(slot);

        public void RequestExecuteConfirm()
        {
            if (CurrentState != ESkillState.Execute) return;

            // 설치 확정 클릭을 무기 공격에 재사용하지 않는다.
            _primaryConsumedFrame = Time.frameCount;
            ExecuteConfirmRequested?.Invoke(SelectedSlot);
        }

        private void ChangeState(ESkillState nextState)
        {
            if (_states == null || !_states.TryGetValue(nextState, out SkillState next) || _currentState == next)
                return;

            _currentState?.ExitState();
            _currentState = next;
            CurrentState = nextState;
            _currentState.EnterState();
        }
    }
}
