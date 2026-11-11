using System;
using System.Collections.Generic;
using alpha.ingame.input;
using alpha.ingame.player;
using UnityEngine;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    public enum EUIType
    {
        QuickSlot,
        CrossHair
    }

    [Serializable]
    private class UITransitionRule
    {
        public EUIType ActiveUI;
        [Tooltip("새 UI를 열 때 이전에 켠 QuickSlot을 계속 표시합니다.")]
        public bool KeepQuickSlot;
        [Tooltip("새 UI를 열 때 이전에 켠 CrossHair를 계속 표시합니다.")]
        public bool KeepCrossHair;
    }

    public static UIManager Instance { get; private set; }

    public GameObject CrossHair;
    public GameObject QuickSlot;

    public bool IsQuickSlotOpen => QuickSlot != null && QuickSlot.activeSelf;

    [SerializeField] private List<UITransitionRule> _uiTransitions = new();

    private readonly List<EUIType> _openedUIs = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (CrossHair != null && CrossHair.activeSelf) _openedUIs.Add(EUIType.CrossHair);
        if (QuickSlot != null) QuickSlot.SetActive(false);
        RefreshUIVisibility();
    }

    private void Update()
    {
        AlphaInput input = AlphaInput.Instance;
        if (input != null && input.IsQuickSlotPressed)
            ToggleUI(EUIType.QuickSlot);
        else if (IsQuickSlotOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Activate(false);
    }

    public void Activate(bool p_isActive) => SetUIActive(EUIType.QuickSlot, p_isActive);

    public void ToggleUI(EUIType type) => SetUIActive(type, !_openedUIs.Contains(type));

    private void Start()
    {
        SetUIActive(EUIType.CrossHair, true);
    }

    public void SetUIActive(EUIType type, bool isActive)
    {
        if (GetUI(type) == null) return;

        // 공격 중에는 키 입력과 외부 호출 모두 퀵슬롯 열기를 거부한다.
        if (type == EUIType.QuickSlot && isActive)
        {
            CombatStateFlow combatFlow = FindFirstObjectByType<CombatStateFlow>();
            // 공격키를 놓은 프레임에도 입력 순서에 따라 창이 열리지 않게 한다.
            bool attackJustEnded = AlphaInput.Instance != null && AlphaInput.Instance.IsPrimaryReleased;
            if (attackJustEnded || combatFlow?.Action?.IsAttacking == true)
                return;
        }

        bool wasOpen = _openedUIs.Remove(type);
        if (isActive)
        {
            // 새로 연 UI를 맨 뒤에 두어 이전 UI에 대한 유지 설정을 적용한다.
            _openedUIs.Add(type);
        }
        else if (!wasOpen) return;

        RefreshUIVisibility();
    }

    private void RefreshUIVisibility()
    {
        bool showQuickSlot = false;
        bool showCrossHair = false;

        foreach (EUIType type in _openedUIs)
        {
            UITransitionRule rule = _uiTransitions.Find(item => item != null && item.ActiveUI == type);
            if (rule == null || !rule.KeepQuickSlot) showQuickSlot = false;
            if (rule == null || !rule.KeepCrossHair) showCrossHair = false;

            if (type == EUIType.QuickSlot) showQuickSlot = true;
            if (type == EUIType.CrossHair) showCrossHair = true;
        }

        SetQuickSlotVisible(showQuickSlot);
        if (CrossHair != null && CrossHair.activeSelf != showCrossHair)
            CrossHair.SetActive(showCrossHair);
    }

    private void SetQuickSlotVisible(bool isVisible)
    {
        if (QuickSlot == null || QuickSlot.activeSelf == isVisible) return;

        // UI 표시와 같은 시점에 전투 및 카메라 입력 상태를 갱신한다.
            if (isVisible) FindFirstObjectByType<CombatModule>()?.CancelSecondary();
        AlphaInput.Instance?.SetQuickSlotOpen(isVisible);
        QuickSlot.SetActive(isVisible);
        MouseManger.Instance?.RefreshLockState();
    }

    private GameObject GetUI(EUIType type)
    {
        return type switch
        {
            EUIType.QuickSlot => QuickSlot,
            EUIType.CrossHair => CrossHair,
            _ => null
        };
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (_openedUIs.Contains(EUIType.QuickSlot)) Activate(false);
        Instance = null;
    }
}
