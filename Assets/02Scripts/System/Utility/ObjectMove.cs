using UnityEngine;

namespace alpha.system
{
    /// <summary>생성 순간의 방향으로 일정 시간 직선 이동한 뒤 객체를 제거한다.</summary>
    [DisallowMultipleComponent]
    public sealed class ObjectMove : MonoBehaviour
    {
        [SerializeField] private Vector3 _localDirection = Vector3.forward;
        [SerializeField, Min(0f)] private float _speed = 10f;
        [SerializeField, Min(0.01f)] private float _lifetime = 8f;

        private Vector3 _worldDirection;
        private float _expiresAt;

        private void OnEnable()
        {
            Vector3 localDirection = _localDirection.sqrMagnitude > 0.0001f
                ? _localDirection.normalized : Vector3.forward;
            _worldDirection = transform.TransformDirection(localDirection).normalized;
            _expiresAt = Time.time + Mathf.Max(0.01f, _lifetime);
        }

        private void Update()
        {
            if (Time.time >= _expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += _worldDirection * (Mathf.Max(0f, _speed) * Time.deltaTime);
        }

        private void OnValidate()
        {
            _speed = Mathf.Max(0f, _speed);
            _lifetime = Mathf.Max(0.01f, _lifetime);
        }
    }
}
