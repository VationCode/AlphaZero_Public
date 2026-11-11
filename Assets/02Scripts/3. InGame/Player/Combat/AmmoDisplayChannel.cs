using System;

namespace alpha.ingame.player
{
    /// <summary>현재 장착한 총의 탄약 표시 값.</summary>
    public readonly struct AmmoDisplayState
    {
        public readonly bool HasGun;
        public readonly int CurrentAmmo;
        public readonly int MagazineCapacity;

        public AmmoDisplayState(int currentAmmo, int magazineCapacity)
        {
            HasGun = true;
            CurrentAmmo = currentAmmo;
            MagazineCapacity = magazineCapacity;
        }
    }

    /// <summary>장착 무기의 탄약 상태를 UI에 전달하고 재구독 시 최신 값을 보낸다.</summary>
    public sealed class AmmoDisplayChannel
    {
        private AmmoDisplayState _state;
        private event Action<AmmoDisplayState> _changed;

        public void Subscribe(Action<AmmoDisplayState> listener)
        {
            if (listener == null) return;
            _changed -= listener;
            _changed += listener;
            listener(_state);
        }

        public void Unsubscribe(Action<AmmoDisplayState> listener) => _changed -= listener;

        public void Publish(AmmoDisplayState state)
        {
            _state = state;
            _changed?.Invoke(state);
        }

        public void Clear() => Publish(default);
    }
}
