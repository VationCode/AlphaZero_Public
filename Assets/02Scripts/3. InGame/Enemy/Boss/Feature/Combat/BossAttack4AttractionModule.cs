using System;
using alpha.ingame.player;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>Attack4 공격 객체가 활성화된 동안 대상에게 수평 인력을 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BossAttack4AttractionModule : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _radius = 60f;
        [SerializeField, Min(0f)] private float _minSpeed = 8f;
        [SerializeField, Min(0f)] private float _maxSpeed = 10f;

        private Transform _source;
        private Func<Transform> _targetProvider;
        private IPlayerAttractionReceiver _receiver;

        // 공격 객체는 대상 선택을 하지 않고 보스가 선택한 현재 대상을 사용한다.
        public void Bind(Transform source, Func<Transform> targetProvider)
        {
            ClearAttraction();
            _source = source;
            _targetProvider = targetProvider;
        }

        private void FixedUpdate()
        {
            Transform target = _targetProvider?.Invoke();
            if (_source == null || target == null ||
                !target.gameObject.activeInHierarchy || _radius <= 0f ||
                _maxSpeed <= 0f)
            {
                ClearAttraction();
                return;
            }

            IPlayerAttractionReceiver receiver =
                target.GetComponentInParent(typeof(IPlayerAttractionReceiver)) as
                    IPlayerAttractionReceiver;
            Vector3 toSource = _source.position - target.position;
            toSource.y = 0f;
            if (receiver == null || toSource.sqrMagnitude <= 0.0001f ||
                toSource.sqrMagnitude > _radius * _radius)
            {
                ClearAttraction();
                return;
            }

            if (!ReferenceEquals(_receiver, receiver)) ClearAttraction();
            float ratio = Mathf.Clamp01(toSource.magnitude / _radius);
            float speed = Mathf.SmoothStep(_minSpeed, _maxSpeed, ratio);
            receiver.SetAttraction(_source, toSource.normalized * speed);
            _receiver = receiver;
        }

        private void ClearAttraction()
        {
            if (_source != null && _receiver is Component component && component != null)
                _receiver.ClearAttraction(_source);
            _receiver = null;
        }

        private void OnDisable() => ClearAttraction();

        private void OnValidate()
        {
            _radius = Mathf.Max(0f, _radius);
            _minSpeed = Mathf.Max(0f, _minSpeed);
            _maxSpeed = Mathf.Max(_minSpeed, _maxSpeed);
        }
    }
}
