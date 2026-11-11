using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace alpha.ingame.player
{
    /// <summary>실제 피격에만 짧은 화면 색감 반응을 겹친다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class PlayerHitPostProcessView : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float _fadeDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _hitVignetteIntensity = 0.38f;
        [SerializeField] private Color _hitVignetteColor = new(0.45f, 0.06f, 0.1f, 1f);
        [SerializeField, Range(-100f, 100f)] private float _hitSaturation = -18f;
        [SerializeField, Range(-2f, 2f)] private float _hitExposure = -0.08f;

        private PlayerActionStateFlow _actionFlow;
        private Volume _hitVolume;
        private VolumeProfile _runtimeProfile;

        private void Awake()
        {
            // 기본 Global Volume은 유지하고, 피격 중에만 가중치가 생기는 별도 Volume을 둔다.
            GameObject effectObject = new("PlayerHitPostProcess");
            effectObject.layer = gameObject.layer;
            effectObject.transform.SetParent(transform, false);
            _hitVolume = effectObject.AddComponent<Volume>();
            _hitVolume.isGlobal = true;
            _hitVolume.priority = GetComponent<Volume>().priority + 1f;
            _hitVolume.weight = 0f;

            _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            Vignette vignette = _runtimeProfile.Add<Vignette>();
            vignette.color.Override(_hitVignetteColor);
            vignette.intensity.Override(_hitVignetteIntensity);
            vignette.smoothness.Override(0.45f);
            ColorAdjustments color = _runtimeProfile.Add<ColorAdjustments>();
            color.saturation.Override(_hitSaturation);
            color.postExposure.Override(_hitExposure);
            _hitVolume.sharedProfile = _runtimeProfile;
        }

        private void OnEnable() => BindActionFlow();

        private void Update()
        {
            if (_actionFlow == null) BindActionFlow();
            if (_hitVolume == null || _hitVolume.weight <= 0f) return;
            _hitVolume.weight = Mathf.MoveTowards(_hitVolume.weight, 0f,
                Time.deltaTime / Mathf.Max(0.01f, _fadeDuration));
        }

        private void BindActionFlow()
        {
            if (_actionFlow != null) return;
            _actionFlow = FindFirstObjectByType<PlayerActionStateFlow>();
            if (_actionFlow != null) _actionFlow.HitReceived += HandleHit;
        }

        private void HandleHit(PlayerHitInfo hit)
        {
            if (hit.Damage > 0f && _hitVolume != null) _hitVolume.weight = 1f;
        }

        private void OnDisable()
        {
            if (_actionFlow != null) _actionFlow.HitReceived -= HandleHit;
            _actionFlow = null;
            if (_hitVolume != null) _hitVolume.weight = 0f;
        }

        private void OnDestroy()
        {
            if (_runtimeProfile != null) Destroy(_runtimeProfile);
        }

        private void OnValidate()
        {
            _fadeDuration = Mathf.Max(0.01f, _fadeDuration);
        }
    }
}
