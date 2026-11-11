using alpha.system.combat;
using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>설치된 쉴드의 수명과 공격 차단 경계를 제공한다.</summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class ShieldInstance : MonoBehaviour, IShieldBarrier
    {
        [SerializeField, Min(8f)] private float _miniCrabPushImpulse = 12f;
        private SphereCollider _collider;
        private float _expiresAt;
        private float _duration;
        private bool _activated;

        public bool IsActive => _activated && isActiveAndEnabled && Time.time < _expiresAt;
        public Vector3 Center => transform.TransformPoint(_collider.center);
        public float Radius => _collider.radius * Mathf.Max(
            Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y),
            Mathf.Abs(transform.lossyScale.z));
        public float PushImpulse => _miniCrabPushImpulse;
        public float ActiveProgress => _activated && isActiveAndEnabled
            ? Mathf.Clamp01((_expiresAt - Time.time) / _duration) : 0f;

        private void Awake() => _collider = GetComponent<SphereCollider>();

        private void OnDisable() => ShieldBarrierRegistry.Unregister(this);

        public void Activate(float duration)
        {
            duration = Mathf.Max(0.1f, duration);
            _duration = duration;
            _expiresAt = Time.time + duration;

            // 프리팹의 짧은 구체 버스트와 8초 지속 파티클을 설치 시간에 맞춘다.
            foreach (ParticleSystem particle in GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = particle.main;
                if (main.duration <= 0.1f)
                    main.startLifetime = duration;
                else
                    main.duration = duration;
                particle.Play(false);
            }

            _activated = true;
            ShieldBarrierRegistry.Register(this);
        }

        private void Update()
        {
            if (_activated && Time.time >= _expiresAt)
                Destroy(gameObject);
        }

        private void OnValidate() =>
            _miniCrabPushImpulse = Mathf.Max(8f, _miniCrabPushImpulse);
    }
}
