using alpha.ingame.input;
using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>모델 프리팹에 붙어 아레테별 기동 동작을 제공한다.</summary>
    public abstract class Arete : MonoBehaviour
    {
        protected PlayerRoot PlayerRoot { get; private set; }
        protected LocomotionModule Locomotion => PlayerRoot.LocomotionModule;

        public abstract EAreteType AreteType { get; }
        public virtual bool CanUseMobility => false;

        public virtual void Initialize(PlayerRoot playerCore)
        {
            PlayerRoot = playerCore;
        }

        public abstract void BeginMobility(AlphaInput input, bool isCombatStance);
        public abstract ELocomotionState? UpdateMobility(AlphaInput input, bool isCombatStance);
        public abstract void EndMobility();
    }
}
