using alpha.ingame.input;

namespace alpha.ingame.player
{
    /// <summary>헤르나 아레테를 식별한다. 기동 동작은 추후 구현한다.</summary>
    public sealed class HernaArete : Arete
    {
        public override EAreteType AreteType => EAreteType.Heruna;

        public override void BeginMobility(AlphaInput input, bool isCombatStance) { }
        public override ELocomotionState? UpdateMobility(AlphaInput input, bool isCombatStance) => null;
        public override void EndMobility() { }
    }
}
