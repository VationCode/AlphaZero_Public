/// <summary>아린 Mobility 게이지의 표시 범위를 관리한다.</summary>
public sealed class ArinMobilityGauge : AreteMobilityGauge
{
    private void OnEnable() => SetGroupVisible(false);
}
