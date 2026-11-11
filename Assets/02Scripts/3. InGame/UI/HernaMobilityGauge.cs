/// <summary>헤르나 Mobility 게이지의 표시 범위를 관리한다.</summary>
public sealed class HernaMobilityGauge : AreteMobilityGauge
{
    private void OnEnable() => SetGroupVisible(false);
}
