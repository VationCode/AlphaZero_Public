using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>선정된 적의 화면 좌표에 기존 타겟 이미지들을 배치한다.</summary>
    public sealed class TargetMarkerView : MonoBehaviour
    {
        [SerializeField] private RectTransform[] _markers;

        private RectTransform _root;
        private Canvas _canvas;
        private IReadOnlyList<Collider> _targets;
        private Camera _camera;

        private void Awake()
        {
            _root = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            Hide();
        }

        public void Show(IReadOnlyList<Collider> targets, Camera camera)
        {
            _targets = targets;
            _camera = camera;
            Refresh();
        }

        public void Hide()
        {
            _targets = null;
            if (_markers == null) return;
            foreach (RectTransform marker in _markers)
                if (marker != null) marker.gameObject.SetActive(false);
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (_markers == null) return;
            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera != null ? _canvas.worldCamera : _camera : null;
            for (int i = 0; i < _markers.Length; i++)
            {
                RectTransform marker = _markers[i];
                if (marker == null) continue;
                Collider target = _targets != null && i < _targets.Count ? _targets[i] : null;
                if (target == null || !target.enabled || !target.gameObject.activeInHierarchy ||
                    _camera == null || _root == null)
                {
                    marker.gameObject.SetActive(false);
                    continue;
                }

                Vector3 screen = _camera.WorldToScreenPoint(target.bounds.center);
                if (screen.z <= 0f || !_camera.pixelRect.Contains(new Vector2(screen.x, screen.y)) ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _root, screen, uiCamera, out Vector2 localPoint))
                {
                    marker.gameObject.SetActive(false);
                    continue;
                }

                marker.anchoredPosition = localPoint;
                if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (_markers == null) return;
            foreach (RectTransform marker in _markers)
                if (marker != null) marker.gameObject.SetActive(false);
        }
    }
}
