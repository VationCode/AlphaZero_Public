using System.Collections;
using alpha.ingame.player;
using UnityEngine;
using UnityEngine.UI;

/// <summary>실리온 부스터 잔량을 8칸의 이미지로 표시한다.</summary>
public sealed class SilyonBoosterGauge : AreteMobilityGauge
{
    [SerializeField] private LocomotionModule _locomotion;
    [SerializeField] private Image[] _segments;

    [Tooltip("부스트 이후 활성화 시간")]
    [SerializeField, Min(0f)] private float _visibleAfterBoost = 1f;
    
    [Header("풀충전 이펙트")]
    [SerializeField, Min(0f)] private float _visibleAfterFullCharge = 0.3f;
    [SerializeField, Min(0f)] float flashDuration = 0.15f;
    [SerializeField, Min(0f)] float dimAlpha = 0.2f;
    [Space(10)]

    [Tooltip("활성화 이후 페이드")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.3f;

    private float[] _originalAlphas;
    private Coroutine _hideRoutine;

    protected override void Awake()
    {
        base.Awake();
        // 현재 배치된 각 칸의 알파값을 부스터 재시작 시 복원한다.
        _originalAlphas = new float[_segments?.Length ?? 0];
        for (int i = 0; i < _originalAlphas.Length; i++)
            if (_segments[i] != null) _originalAlphas[i] = _segments[i].color.a;
    }

    private void OnEnable()
    {
        if (_locomotion != null)
        {
            _locomotion.OnBoostRatioChanged += Refresh;
            _locomotion.OnBoostStateChanged += HandleBoostStateChanged;
            _locomotion.OnBoostFullyCharged += HandleBoostFullyCharged;
            Refresh(_locomotion.BoostRatio);
        }

        if (_locomotion != null && _locomotion.IsBoosting) ShowGroup();
        else HideGroup();
    }

    private void OnDisable()
    {
        StopHideRoutine();
        HideGroup();
        if (_locomotion != null)
        {
            _locomotion.OnBoostRatioChanged -= Refresh;
            _locomotion.OnBoostStateChanged -= HandleBoostStateChanged;
            _locomotion.OnBoostFullyCharged -= HandleBoostFullyCharged;
        }
    }

    private void HandleBoostStateChanged(bool isBoosting)
    {
        StopHideRoutine();
        if (isBoosting) ShowGroup();
        else if (IsGroupVisible)
            _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private void HandleBoostFullyCharged()
    {
        if (_locomotion == null || _locomotion.IsBoosting)
            return;

        StopHideRoutine();
        ShowGroup();
        _hideRoutine = StartCoroutine(ShowFullChargeCue());
    }

    private IEnumerator ShowFullChargeCue()
    {
        // 원래 알파값을 넘기지 않고 두 번 짧게 깜빡여 충전 완료를 알린다.
        const int flashCount = 2;

        for (int flash = 0; flash < flashCount; flash++)
        {
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float pulse = Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / flashDuration));
                SetAlpha(Mathf.Lerp(1f, dimAlpha, pulse));
                yield return null;
            }
        }

        SetAlpha(1f);
        if (_visibleAfterFullCharge > 0f)
            yield return new WaitForSeconds(_visibleAfterFullCharge);

        yield return FadeOutAndHide();
    }

    private IEnumerator HideAfterDelay()
    {
        if (_visibleAfterBoost > 0f)
            yield return new WaitForSeconds(_visibleAfterBoost);

        yield return FadeOutAndHide();
    }

    private IEnumerator FadeOutAndHide()
    {
        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / _fadeDuration));
            yield return null;
        }

        HideGroup();
        _hideRoutine = null;
    }

    private void ShowGroup()
    {
        SetAlpha(1f);
        SetGroupVisible(true);
    }

    private void HideGroup()
    {
        SetGroupVisible(false);
    }

    private void SetAlpha(float fraction)
    {
        for (int i = 0; i < _originalAlphas.Length; i++)
        {
            Image image = _segments[i];
            if (image == null) continue;
            Color color = image.color;
            color.a = _originalAlphas[i] * fraction;
            image.color = color;
        }
    }

    private void StopHideRoutine()
    {
        if (_hideRoutine == null) return;
        StopCoroutine(_hideRoutine);
        _hideRoutine = null;
    }

    private void Refresh(float ratio)
    {
        if (_segments == null) return;

        // 1번부터 비우고, 충전할 때는 8번부터 다시 채운다.
        for (int i = 0; i < _segments.Length; i++)
        {
            if (_segments[i] != null)
                _segments[i].fillAmount = Mathf.Clamp01(ratio * _segments.Length - (_segments.Length - 1 - i));
        }
    }
}
