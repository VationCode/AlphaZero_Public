using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>기준 위치 주변의 무작위 지점을 순서대로 부드럽게 이동한다.</summary>
    [DefaultExecutionOrder(160)]
    public sealed class RandomPointMove : MonoBehaviour
    {
        [SerializeField, Min(2)] private int _pointCount = 3;
        [SerializeField, Min(0f)] private float _radius = 0.3f;
        [SerializeField, Min(0.01f)] private float _arrivalTime = 1.2f;
        [SerializeField, Range(0f, 1f)] private float _movementSmooth = 0.8f;

        private Vector3[] _points;
        private Vector3 _origin;
        private Vector3 _from;
        private Vector3 _to;
        private float _elapsed;
        private float _radiusMultiplier = 1f;
        private int _nextPoint;
        private bool _isMoving;

        public float MaxOffsetRadius => _radius * _radiusMultiplier;

        public void Begin(Vector3 originLocalPosition)
        {
            _origin = originLocalPosition;
            _radiusMultiplier = 1f;
            _points = new Vector3[Mathf.Max(2, _pointCount)];
            for (int i = 0; i < _points.Length; i++)
                _points[i] = Random.insideUnitSphere * Mathf.Max(0f, _radius);

            transform.localPosition = _origin;
            _from = Vector3.zero;
            _nextPoint = 0;
            _isMoving = true;
            NextPoint();
        }

        // 이동 기준만 바꾼다. 랜덤 포인트 이동 자체는 독립적으로 계속된다.
        public void SetOrigin(Vector3 originLocalPosition) => _origin = originLocalPosition;

        public void SetRadiusMultiplier(float multiplier)
        {
            float next = Mathf.Max(1f, multiplier);
            if (Mathf.Approximately(_radiusMultiplier, next)) return;
            _radiusMultiplier = next;
            if (_isMoving) NextPoint();
        }

        private void LateUpdate()
        {
            if (!_isMoving) return;
            _elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _arrivalTime));
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float blend = Mathf.Lerp(progress, eased, _movementSmooth);
            Vector3 offset = Vector3.ClampMagnitude(
                Vector3.LerpUnclamped(_from, _to, blend), MaxOffsetRadius);
            transform.localPosition = _origin + offset;
            if (progress >= 1f) NextPoint();
        }

        private void NextPoint()
        {
            if (_points == null || _points.Length == 0) return;
            _from = Vector3.ClampMagnitude(transform.localPosition - _origin, MaxOffsetRadius);
            _to = _points[_nextPoint] * _radiusMultiplier;
            _nextPoint = (_nextPoint + 1) % _points.Length;
            _elapsed = 0f;
        }

        private void OnDisable() => _isMoving = false;
    }
}
