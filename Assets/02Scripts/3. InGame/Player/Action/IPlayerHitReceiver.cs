namespace alpha.ingame.player
{
    // 공격 객체가 Player의 내부 Health 구현을 몰라도 피격을 전달할 수 있다.
    public interface IPlayerHitReceiver
    {
        bool TryReceiveHit(in PlayerHitInfo hit);
    }
}
