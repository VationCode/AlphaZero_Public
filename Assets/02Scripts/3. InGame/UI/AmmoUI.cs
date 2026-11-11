using alpha.ingame.player;
using TMPro;
using UnityEngine;

/// <summary>현재 장착한 총의 남은 장탄 수를 표시한다.</summary>
public sealed class AmmoUI : MonoBehaviour
{
    [SerializeField] private PlayerRoot _playerCore;
    [SerializeField] private TMP_Text _ammoText;

    private AmmoDisplayChannel _channel;

    private void OnEnable()
    {
        if (_playerCore == null) _playerCore = FindFirstObjectByType<PlayerRoot>();
        _channel = _playerCore?.AmmoDisplayChannel;
        _channel?.Subscribe(HandleAmmoChanged);
    }

    private void OnDisable()
    {
        _channel?.Unsubscribe(HandleAmmoChanged);
        _channel = null;
    }

    private void HandleAmmoChanged(AmmoDisplayState state)
    {
        if (_ammoText == null) return;
        _ammoText.text = state.HasGun ? state.CurrentAmmo.ToString() : "--";
    }
}
