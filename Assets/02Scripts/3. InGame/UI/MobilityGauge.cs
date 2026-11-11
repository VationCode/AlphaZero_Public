using alpha;
using alpha.ingame.player;
using UnityEngine;

/// <summary>선택된 아레테의 Mobility 게이지만 활성화한다.</summary>
public sealed class MobilityGauge : MonoBehaviour
{
    [SerializeField] private PlayerRoot _playerCore;
    [SerializeField] private SilyonBoosterGauge _silyonGauge;
    [SerializeField] private HernaMobilityGauge _hernaGauge;
    [SerializeField] private ArinMobilityGauge _arinGauge;

    private void OnEnable()
    {
        if (_playerCore != null) _playerCore.OnPlayerSetup += SelectGauge;
        if (_playerCore != null && _playerCore.CurrentArete != null)
            SelectGauge(_playerCore.CurrentAreteType);
        else
            SetAllInactive();
    }

    private void OnDisable()
    {
        if (_playerCore != null) _playerCore.OnPlayerSetup -= SelectGauge;
    }

    private void SelectGauge(EAreteType type)
    {
        if (_silyonGauge != null) _silyonGauge.gameObject.SetActive(type == EAreteType.Silyon);
        if (_hernaGauge != null) _hernaGauge.gameObject.SetActive(type == EAreteType.Heruna);
        if (_arinGauge != null) _arinGauge.gameObject.SetActive(type == EAreteType.Arin);
    }

    private void SetAllInactive()
    {
        if (_silyonGauge != null) _silyonGauge.gameObject.SetActive(false);
        if (_hernaGauge != null) _hernaGauge.gameObject.SetActive(false);
        if (_arinGauge != null) _arinGauge.gameObject.SetActive(false);
    }
}
