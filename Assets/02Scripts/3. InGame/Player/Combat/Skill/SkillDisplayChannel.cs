using System;
using UnityEngine;

namespace alpha.ingame.skill
{
    /// <summary>UI에 필요한 한 슬롯의 표시 상태.</summary>
    public readonly struct SkillSlotDisplayState
    {
        public readonly int Slot;
        public readonly Sprite Icon;
        public readonly float CooldownDuration;
        public readonly float CooldownEndTime;

        public SkillSlotDisplayState(int slot, Sprite icon, float cooldownDuration, float cooldownEndTime)
        {
            Slot = slot;
            Icon = icon;
            CooldownDuration = cooldownDuration;
            CooldownEndTime = cooldownEndTime;
        }
    }

    /// <summary>스킬 표시 상태를 전달하고 UI 재활성화 시 최신 상태를 다시 보낸다.</summary>
    public sealed class SkillDisplayChannel
    {
        private const int SlotCount = 4;
        private readonly SkillSlotDisplayState[] _slots = new SkillSlotDisplayState[SlotCount];
        private event Action<SkillSlotDisplayState> _slotChanged;

        public SkillDisplayChannel()
        {
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = new SkillSlotDisplayState(i + 1, null, 0f, 0f);
        }

        public void Subscribe(Action<SkillSlotDisplayState> listener)
        {
            if (listener == null) return;
            _slotChanged -= listener;
            _slotChanged += listener;
            foreach (SkillSlotDisplayState state in _slots)
                listener(state);
        }

        public void Unsubscribe(Action<SkillSlotDisplayState> listener) => _slotChanged -= listener;

        public void Publish(SkillSlotDisplayState state)
        {
            if (state.Slot < 1 || state.Slot > SlotCount) return;
            _slots[state.Slot - 1] = state;
            _slotChanged?.Invoke(state);
        }

        public void Clear()
        {
            for (int slot = 1; slot <= SlotCount; slot++)
                Publish(new SkillSlotDisplayState(slot, null, 0f, 0f));
        }
    }
}
