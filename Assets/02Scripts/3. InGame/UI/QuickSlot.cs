using alpha.ingame.player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuickSlot : MonoBehaviour
{
    [SerializeField] private PlayerRoot _playerCore;
    [SerializeField] private Button _mainButton;
    [SerializeField] private Button _subButton;
    
    private void OnEnable()
    {
        if (_playerCore == null) _playerCore = FindFirstObjectByType<PlayerRoot>();
        if (_mainButton != null) _mainButton.onClick.AddListener(SelectMain);
        if (_subButton != null) _subButton.onClick.AddListener(SelectSub);
        Refresh();
    }

    private void OnDisable()
    {
        if (_mainButton != null) _mainButton.onClick.RemoveListener(SelectMain);
        if (_subButton != null) _subButton.onClick.RemoveListener(SelectSub);
    }

    private void SelectMain() => Select((int)EEquipmentSlot.Main);
    private void SelectSub() => Select((int)EEquipmentSlot.Sub);

    public void Select(int p_num)
    {
        // 장착 모듈이 무기 표시를 바꾼 뒤 선택창을 닫는다.
        if (_playerCore == null || _playerCore.EquipmentModule == null) return;
        if (!_playerCore.EquipmentModule.SelectSlot((EEquipmentSlot)p_num)) return;

        UIManager.Instance?.Activate(false);
    }

    private void Refresh()
    {
        EquipmentModule equipment = _playerCore != null ? _playerCore.EquipmentModule : null;
        if (_mainButton != null) _mainButton.interactable = equipment != null && equipment.MainWeapon != null;
        if (_subButton != null) _subButton.interactable = equipment != null && equipment.SubWeapon != null;

        /*if (_mainLabel != null)
            _mainLabel.color = equipment != null && equipment.ActiveSlot == EEquipmentSlot.Main ? _selectedColor : _normalColor;
        if (_subLabel != null)
            _subLabel.color = equipment != null && equipment.ActiveSlot == EEquipmentSlot.Sub ? _selectedColor : _normalColor;*/
    }
}
