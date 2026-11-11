using alpha.ingame.input;

namespace alpha.ingame.player
{
    /// <summary>아린의 기동 방식이 정해질 때까지 공통 아레테 정보만 제공한다.</summary>
    public sealed class ArinArete : Arete
    {
        public override EAreteType AreteType => EAreteType.Arin;

        public override void BeginMobility(AlphaInput input, bool isCombatStance) { }
        public override ELocomotionState? UpdateMobility(AlphaInput input, bool isCombatStance) => null;
        public override void EndMobility() { }
    }
}
