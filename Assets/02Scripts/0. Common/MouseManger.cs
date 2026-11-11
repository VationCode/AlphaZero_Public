using alpha.scene;
using alpha.ingame.input;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>씬과 UI 상태에 맞춰 마우스 커서 잠금을 관리한다.</summary>
public class MouseManger : MonoBehaviour
{
    public static MouseManger Instance { get; private set; }
    public bool IsLocked { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        Instance = this;
        RefreshLockState();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SetLocked(false);
        Instance = null;
    }

    private void Update()
    {
        RefreshLockState();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshLockState();
    }

    public void RefreshLockState()
    {
        // 퀵슬롯과 임시 커서 표시 키가 잠금보다 우선한다.
        bool isInGame = SceneManager.GetActiveScene().buildIndex == (int)ESceneType.InGame;
        bool isQuickSlotOpen = UIManager.Instance != null && UIManager.Instance.IsQuickSlotOpen;
        bool isMouseLockHeld = AlphaInput.Instance != null && AlphaInput.Instance.IsMouseLock;
        SetLocked(isInGame && !isQuickSlotOpen && !isMouseLockHeld);
    }

    private void SetLocked(bool isLocked)
    {
        IsLocked = isLocked;
        CursorLockMode targetLock = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        if (Cursor.lockState != targetLock) Cursor.lockState = targetLock;
        if (Cursor.visible == isLocked) Cursor.visible = !isLocked;
    }
}
