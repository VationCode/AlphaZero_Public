using System.Collections;
using alpha.ingame.player;
using alpha.ingame.skill;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>스킬 슬롯의 쿨다운, 선택 반응 및 지속 상태를 표시한다.</summary>
public sealed class SkillUI : MonoBehaviour
{
    [SerializeField] private PlayerRoot _playerCore;
    [SerializeField] private RectTransform _slotsRoot;
    [Header("Selection")]
    [SerializeField, Min(1f)] private float _selectionScale = 1.1f;
    [SerializeField, Min(0.01f)] private float _selectionPulseDuration = 0.28f;
    [SerializeField] private AudioSource _selectionAudioSource;
    [SerializeField] private AudioClip _selectionClip;
    [SerializeField] private AudioClip _cancelClip;

    [Header("Beam Pod Status")]
    [SerializeField] private Color _beamPodTargetingColor = new(0.2f, 1f, 0.35f, 1f);
    [SerializeField] private Color _beamPodAttackingColor = new(1f, 0.25f, 0.25f, 1f);

    private readonly RectTransform[] _slots = new RectTransform[4];
    private readonly Vector3[] _normalSlotScales = new Vector3[4];
    private readonly Image[] _icons = new Image[4];
    private readonly Image[] _backs = new Image[4];
    private readonly Color[] _normalBackColors = new Color[4];
    private readonly Image[] _fills = new Image[4];
    private readonly Image[] _progresses = new Image[4];
    private readonly Color[] _normalFillColors = new Color[4];
    private readonly TMP_Text[] _times = new TMP_Text[4];
    private readonly Coroutine[] _fillRoutines = new Coroutine[4];
    private readonly SkillSlotDisplayState[] _slotStates = new SkillSlotDisplayState[4];
    private int _pulseSlot;
    private float _pulseStartedAt;

    private void Awake()
    {
        if (_playerCore == null) _playerCore = FindFirstObjectByType<PlayerRoot>();
        if (_slotsRoot == null) return;

        for (int i = 0; i < 4; i++)
        {
            RectTransform slot = _slotsRoot.Find($"Skill{i + 1}") as RectTransform;
            _slots[i] = slot;
            if (slot != null) _normalSlotScales[i] = slot.localScale;
            _icons[i] = slot?.Find("Icon")?.GetComponent<Image>();
            _backs[i] = slot?.Find("Back")?.GetComponent<Image>();
            if (_backs[i] != null) _normalBackColors[i] = _backs[i].color;
            _fills[i] = slot?.Find("Fill")?.GetComponent<Image>();
            _progresses[i] = slot?.Find("Progress")?.GetComponent<Image>();
            if (_fills[i] != null) _normalFillColors[i] = _fills[i].color;
            _times[i] = slot?.Find("Time (TMP)")?.GetComponent<TMP_Text>();

            if (_icons[i] == null || _fills[i] == null || _progresses[i] == null || _times[i] == null)
                Debug.LogWarning($"{nameof(SkillUI)}: Skill{i + 1}의 Icon, Fill, Progress 또는 Time (TMP)를 찾을 수 없습니다.", this);
        }
    }

    private void OnEnable()
    {
        // 구독 즉시 채널이 네 슬롯의 최신 상태를 재전달한다.
        _playerCore?.SkillDisplayChannel.Subscribe(HandleSlotChanged);
        if (_playerCore?.SkillModule != null)
        {
            _playerCore.SkillModule.SkillSelected += HandleSkillSelected;
            _playerCore.SkillModule.SkillSelectionSoundRequested += HandleSkillSelectionSoundRequested;
            _playerCore.SkillModule.SkillCanceled += HandleSkillCanceled;
        }
    }

    private void OnDisable()
    {
        _playerCore?.SkillDisplayChannel.Unsubscribe(HandleSlotChanged);
        if (_playerCore?.SkillModule != null)
        {
            _playerCore.SkillModule.SkillSelected -= HandleSkillSelected;
            _playerCore.SkillModule.SkillSelectionSoundRequested -= HandleSkillSelectionSoundRequested;
            _playerCore.SkillModule.SkillCanceled -= HandleSkillCanceled;
        }
        RestorePulseScale();
        RefreshStatusColors(0, 0, false);
        foreach (Image progress in _progresses)
            if (progress != null) progress.fillAmount = 0f;
        for (int i = 0; i < _fillRoutines.Length; i++)
        {
            if (_fillRoutines[i] == null) continue;
            StopCoroutine(_fillRoutines[i]);
            _fillRoutines[i] = null;
        }
    }

    private void Update()
    {
        SkillStateFlow flow = _playerCore?.SkillStateFlow;
        int placementSlot = flow != null && flow.CurrentState == ESkillState.Execute
            ? flow.SelectedSlot : 0;
        SkillModule skillModule = _playerCore?.SkillModule;
        RefreshStatusColors(placementSlot, skillModule?.ActiveBeamPodSlot ?? 0,
            skillModule?.IsBeamPodAttacking ?? false);
        for (int i = 0; i < _progresses.Length; i++)
            if (_progresses[i] != null)
                _progresses[i].fillAmount = skillModule?.GetActiveProgress(i + 1) ?? 0f;
        UpdateSelectionPulse();
    }

    private void HandleSkillSelected(int slot)
    {
        PulseSlot(slot);
    }

    private void HandleSkillSelectionSoundRequested(int slot)
    {
        if (slot < 1 || slot > _slots.Length) return;
        if (_selectionAudioSource != null && _selectionClip != null)
            _selectionAudioSource.PlayOneShot(_selectionClip);
    }

    private void HandleSkillCanceled(int slot)
    {
        if (!PulseSlot(slot)) return;
        if (_selectionAudioSource != null && _cancelClip != null)
            _selectionAudioSource.PlayOneShot(_cancelClip);
    }

    private bool PulseSlot(int slot)
    {
        if (slot < 1 || slot > _slots.Length) return false;
        RestorePulseScale();
        _pulseSlot = slot;
        _pulseStartedAt = Time.unscaledTime;
        return true;
    }

    private void UpdateSelectionPulse()
    {
        if (_pulseSlot == 0) return;
        int index = _pulseSlot - 1;
        RectTransform slot = _slots[index];
        float progress = (Time.unscaledTime - _pulseStartedAt) /
            Mathf.Max(0.01f, _selectionPulseDuration);
        if (progress >= 1f)
        {
            RestorePulseScale();
            return;
        }

        // 선택 순간 한 번 커졌다가 끝에서 정확히 원래 배율로 돌아간다.
        float scale = 1f + (Mathf.Max(1f, _selectionScale) - 1f) *
            Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress));
        if (slot != null) slot.localScale = _normalSlotScales[index] * scale;
    }

    private void RestorePulseScale()
    {
        if (_pulseSlot == 0) return;
        int index = _pulseSlot - 1;
        if (_slots[index] != null) _slots[index].localScale = _normalSlotScales[index];
        _pulseSlot = 0;
    }

    private void RefreshStatusColors(int placementSlot, int activeBeamPodSlot,
        bool isBeamPodAttacking)
    {
        for (int index = 0; index < _backs.Length; index++)
        {
            Color back = _normalBackColors[index];
            Color fill = _normalFillColors[index];
            if (activeBeamPodSlot != 0 && index + 1 == activeBeamPodSlot)
            {
                // 포드가 존재할 때 타겟팅·복귀는 초록, 공격 중은 빨강으로 표시한다.
                Color status = isBeamPodAttacking
                    ? _beamPodAttackingColor : _beamPodTargetingColor;
                back = status;
                status.a = 0.75f;
                fill = status;
            }
            else if (index + 1 == placementSlot)
            {
                // 설치 확정 전에는 활성화색을 유지하고, 확정·취소하면 기본색으로 복원한다.
                back = _beamPodTargetingColor;
                fill = _beamPodTargetingColor;
                fill.a = 0.75f;
            }
            if (_backs[index] != null && _backs[index].color != back)
                _backs[index].color = back;
            if (_fills[index] != null && _fills[index].color != fill)
                _fills[index].color = fill;
        }
    }

    private void HandleSlotChanged(SkillSlotDisplayState state)
    {
        if (state.Slot < 1 || state.Slot > 4) return;
        int index = state.Slot - 1;
        _slotStates[index] = state;
        RefreshSlot(index);
    }

    private void RefreshSlot(int index)
    {
        SkillSlotDisplayState state = _slotStates[index];
        Image icon = _icons[index];
        if (icon != null)
        {
            // 데이터가 없는 슬롯은 배경과 번호만 남긴다.
            icon.sprite = state.Icon;
            icon.enabled = state.Icon != null;
        }

        if (_fillRoutines[index] != null)
        {
            StopCoroutine(_fillRoutines[index]);
            _fillRoutines[index] = null;
        }

        // 재활성화 시에도 남은 시간으로 Fill을 복원한다.
        float remaining = GetRemainingCooldown(state);
        UpdateCooldownVisual(index, remaining);
        if (remaining > 0f)
            _fillRoutines[index] = StartCoroutine(AnimateFill(index));
    }

    private IEnumerator AnimateFill(int index)
    {
        while (true)
        {
            yield return null;
            float remaining = GetRemainingCooldown(_slotStates[index]);
            UpdateCooldownVisual(index, remaining);
            if (remaining <= 0f) break;
        }
        _fillRoutines[index] = null;
    }

    private void UpdateCooldownVisual(int index, float remaining)
    {
        SkillSlotDisplayState state = _slotStates[index];
        Image fill = _fills[index];
        if (fill != null)
            fill.fillAmount = state.CooldownDuration > 0f
                ? Mathf.Clamp01(remaining / state.CooldownDuration) : 0f;

        TMP_Text time = _times[index];
        if (time == null) return;
        bool coolingDown = remaining > 0f;
        if (coolingDown)
        {
            string seconds = Mathf.CeilToInt(remaining).ToString();
            if (time.text != seconds) time.text = seconds;
        }
        if (time.gameObject.activeSelf != coolingDown)
            time.gameObject.SetActive(coolingDown);
    }

    private static float GetRemainingCooldown(SkillSlotDisplayState state)
    {
        if (state.CooldownDuration <= 0f) return 0f;
        return Mathf.Max(0f, state.CooldownEndTime - Time.time);
    }
}
