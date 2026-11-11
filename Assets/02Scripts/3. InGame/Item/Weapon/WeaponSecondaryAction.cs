using UnityEngine;

namespace alpha.ingame.item
{
    /// <summary>장착 무기의 Secondary 입력 구간을 처리하는 교체 가능한 행동이다.</summary>
    public abstract class WeaponSecondaryAction : MonoBehaviour
    {
        public abstract void Begin();
        public virtual void Tick() { }
        public abstract void End();
        public virtual void Cancel() => End();

        protected virtual void OnDisable() => Cancel();
    }
}
