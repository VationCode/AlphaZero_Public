using System;
using TMPro;
using UnityEngine;

namespace alpha.ingame.enemy
{
    /// <summary>피격점을 Canvas 좌표에 표시하고 거리별 크기와 글리치를 표현한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class EnemyHealthRevealView : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField, Min(0.1f)] private float _fontSize = 36f;
        [SerializeField] private Color _highColor = new(0.45f, 1f, 0.95f, 1f);
        [SerializeField] private Color _middleColor = new(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color _lowColor = new(1f, 0.35f, 0.4f, 1f);
        [SerializeField] private Color _criticalColor = new(1f, 0.4f, 0.1f, 1f);
        [SerializeField, Min(1f)] private float _criticalSizeMultiplier = 1.5f;

        [Header("Distance Size")]
        [SerializeField, Min(0.1f)] private float _referenceDistance = 10f;
        [SerializeField, Min(0.01f)] private float _minDistanceScale = 0.6f;
        [SerializeField, Min(0.01f)] private float _maxDistanceScale = 1.4f;

        [Header("Motion")]
        [SerializeField, Min(0.01f)] private float _lifetime = 1f;
        [SerializeField, Min(0f)] private float _riseDistance = 50f;
        [SerializeField, Min(0f)] private float _surfaceOffset = 0.06f;
        [SerializeField, Range(0f, 1f)] private float _glitchStart = 0.35f;
        [SerializeField, Min(0f)] private float _glitchOffset = 2f;
        [SerializeField, Range(0f, 1f)] private float _dropoutChance = 0.18f;

        private TextMeshProUGUI _text;
        private RectTransform _rect;
        // 버퍼를 유지해 같은 텍스트 오브젝트를 재사용할 때 정점 배열을 다시 할당하지 않는다.
        private Vector3[][] _baseVertices;
        private Color32[][] _baseColors;
        private Vector3 _hitPoint;
        private float _startedAt;
        private float _nextGlitchAt;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _text = GetComponent<TextMeshProUGUI>();
            if (_text == null) _text = gameObject.AddComponent<TextMeshProUGUI>();
            _text.font = _font != null ? _font : TMP_Settings.defaultFontAsset;
            _text.fontSize = _fontSize;
            _text.alignment = TextAlignmentOptions.Center;
            _text.enableWordWrapping = false;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.raycastTarget = false;
            _text.text = string.Empty;
            _text.enabled = false;
        }

        public void Play(float damage, float currentHealth, float maxHealth,
            bool isCritical, Vector3 hitPoint)
        {
            gameObject.SetActive(true);
            float ratio = Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));
            _text.text = Mathf.Max(0f, damage).ToString("0.##");
            _text.fontSize = _fontSize * (isCritical ? _criticalSizeMultiplier : 1f);
            _text.color = isCritical ? _criticalColor : ratio > 0.7f ? _highColor :
                ratio >= 0.3f ? _middleColor : _lowColor;
            _text.enabled = true;
            _text.ForceMeshUpdate();
            CacheBaseMesh();
            _hitPoint = hitPoint;
            _startedAt = Time.time;
            _nextGlitchAt = 0f;
        }

        // 수명과 화면 좌표는 풀을 소유한 Canvas가 한 번에 갱신한다.
        public bool Tick(RectTransform canvasRect, Camera camera)
        {
            float progress = (Time.time - _startedAt) / Mathf.Max(0.01f, _lifetime);
            if (progress >= 1f) return false;
            if (camera == null)
            {
                _text.enabled = false;
                return true;
            }

            Vector3 towardCamera = (camera.transform.position - _hitPoint).normalized;
            Vector3 screen = camera.WorldToScreenPoint(_hitPoint + towardCamera * _surfaceOffset);
            if (screen.z <= 0f)
            {
                _text.enabled = false;
                return true;
            }
            if (!_text.enabled)
            {
                _text.enabled = true;
                _text.ForceMeshUpdate();
                CacheBaseMesh();
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screen, null, out Vector2 position))
                _rect.anchoredPosition = position + Vector2.up * (_riseDistance * progress);

            // Canvas의 화면 기준 크기에 피격점의 거리감을 다시 부여한다.
            float distance = Vector3.Distance(camera.transform.position, _hitPoint);
            float scale = Mathf.Clamp(_referenceDistance / Mathf.Max(0.1f, distance),
                _minDistanceScale, _maxDistanceScale);
            _rect.localScale = new Vector3(scale, scale, 1f);
            // 글리치 이전에는 정점 갱신을 생략하고 이후에도 20Hz로 제한한다.
            if (progress >= _glitchStart && Time.time >= _nextGlitchAt)
            {
                AnimateGlyphs(progress);
                _nextGlitchAt = Time.time + 0.05f;
            }
            return true;
        }

        private void CacheBaseMesh()
        {
            TMP_TextInfo info = _text.textInfo;
            int count = info.meshInfo.Length;
            if (_baseVertices == null || _baseVertices.Length < count)
            {
                Array.Resize(ref _baseVertices, count);
                Array.Resize(ref _baseColors, count);
            }
            for (int i = 0; i < count; i++)
            {
                Vector3[] vertices = info.meshInfo[i].vertices;
                Color32[] colors = info.meshInfo[i].colors32;
                if (_baseVertices[i] == null || _baseVertices[i].Length < vertices.Length)
                    _baseVertices[i] = new Vector3[vertices.Length];
                if (_baseColors[i] == null || _baseColors[i].Length < colors.Length)
                    _baseColors[i] = new Color32[colors.Length];
                Array.Copy(vertices, _baseVertices[i], vertices.Length);
                Array.Copy(colors, _baseColors[i], colors.Length);
            }
        }

        private void AnimateGlyphs(float progress)
        {
            bool glitching = progress >= _glitchStart;
            float fade = Mathf.Clamp01((1f - progress) /
                Mathf.Max(0.001f, 1f - _glitchStart));
            TMP_TextInfo info = _text.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                int material = character.materialReferenceIndex;
                int first = character.vertexIndex;
                Vector3[] vertices = info.meshInfo[material].vertices;
                Color32[] colors = info.meshInfo[material].colors32;
                Vector3[] originalVertices = _baseVertices[material];
                Color32[] originalColors = _baseColors[material];
                Vector3 offset = glitching
                    ? new Vector3(UnityEngine.Random.Range(-_glitchOffset, _glitchOffset),
                        UnityEngine.Random.Range(-_glitchOffset * 0.25f, _glitchOffset * 0.25f), 0f)
                    : Vector3.zero;
                float opacity = glitching && UnityEngine.Random.value < _dropoutChance ? 0f : fade;
                for (int corner = 0; corner < 4; corner++)
                {
                    int index = first + corner;
                    vertices[index] = originalVertices[index] + offset;
                    Color32 color = originalColors[index];
                    color.a = (byte)(color.a * opacity);
                    colors[index] = color;
                }
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices |
                TMP_VertexDataUpdateFlags.Colors32);
        }

        private void OnValidate()
        {
            _fontSize = Mathf.Max(0.1f, _fontSize);
            _criticalSizeMultiplier = Mathf.Max(1f, _criticalSizeMultiplier);
            _referenceDistance = Mathf.Max(0.1f, _referenceDistance);
            _minDistanceScale = Mathf.Max(0.01f, _minDistanceScale);
            _maxDistanceScale = Mathf.Max(_minDistanceScale, _maxDistanceScale);
            _lifetime = Mathf.Max(0.01f, _lifetime);
        }
    }
}
