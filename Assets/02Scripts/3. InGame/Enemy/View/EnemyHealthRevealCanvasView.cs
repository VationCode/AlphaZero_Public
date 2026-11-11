using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.enemy
{
    /// <summary>전용 UI Canvas에서 피해 텍스트를 재사용하고 표시 개수를 제한한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class EnemyHealthRevealCanvasView : MonoBehaviour
    {
        [SerializeField] private EnemyHealthRevealView _effectPrefab;
        [SerializeField, Min(0)] private int _initialPoolSize = 16;
        [SerializeField, Min(1)] private int _maxVisible = 64;
        [SerializeField] private int _sortingOrder = 10;

        private readonly Stack<EnemyHealthRevealView> _available = new();
        private readonly List<EnemyHealthRevealView> _active = new();
        private RectTransform _canvasRect;

        public static EnemyHealthRevealCanvasView Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
            if (_effectPrefab == null)
            {
                Debug.LogError($"{nameof(EnemyHealthRevealCanvasView)}: 피해 텍스트 프리팹을 연결하세요.", this);
                enabled = false;
                return;
            }

            GameObject layer = new("EnemyDamageCanvas", typeof(RectTransform), typeof(Canvas));
            _canvasRect = (RectTransform)layer.transform;
            _canvasRect.SetParent(transform, false);
            _canvasRect.anchorMin = Vector2.zero;
            _canvasRect.anchorMax = Vector2.one;
            _canvasRect.offsetMin = Vector2.zero;
            _canvasRect.offsetMax = Vector2.zero;
            Canvas canvas = layer.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = _sortingOrder;

            for (int i = 0; i < Mathf.Min(_initialPoolSize, _maxVisible); i++)
                _available.Push(CreateEffect());
        }

        public void Show(in EnemyDamageResult result)
        {
            ShowDamage(result.DisplayDamage, result.CurrentHealth, result.MaxHealth,
                result.IsCritical, result.HitPoint);
        }

        // 프리팹 Inspector의 재생 미리보기도 실제 Canvas 풀을 사용한다.
        public void Preview(float damage, float currentHealth, float maxHealth,
            bool isCritical, Vector3 hitPoint)
        {
            ShowDamage(damage, currentHealth, maxHealth, isCritical, hitPoint);
        }

        private void ShowDamage(float damage, float currentHealth, float maxHealth,
            bool isCritical, Vector3 hitPoint)
        {
            if (!isActiveAndEnabled || _canvasRect == null) return;
            EnemyHealthRevealView effect;
            if (_available.Count > 0) effect = _available.Pop();
            else if (_active.Count < _maxVisible) effect = CreateEffect();
            else
            {
                // 상한에 도달하면 가장 오래된 숫자를 새 피격 결과에 재사용한다.
                effect = _active[0];
                _active.RemoveAt(0);
            }

            effect.Play(damage, currentHealth, maxHealth, isCritical, hitPoint);
            _active.Add(effect);
        }

        private EnemyHealthRevealView CreateEffect()
        {
            EnemyHealthRevealView effect = Instantiate(_effectPrefab, _canvasRect, false);
            effect.gameObject.SetActive(false);
            return effect;
        }

        private void LateUpdate()
        {
            if (_active.Count == 0) return;
            Camera camera = Camera.main;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                EnemyHealthRevealView effect = _active[i];
                if (effect != null && effect.Tick(_canvasRect, camera)) continue;
                _active.RemoveAt(i);
                if (effect == null) continue;
                effect.gameObject.SetActive(false);
                _available.Push(effect);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnValidate()
        {
            _initialPoolSize = Mathf.Max(0, _initialPoolSize);
            _maxVisible = Mathf.Max(1, _maxVisible);
        }
    }
}
