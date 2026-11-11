using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>빔 끝의 콜라이더 중심을 명중점에 맞춰 길이와 굵기를 조절한다.</summary>
    public sealed class BeamPodBeamVisual : MonoBehaviour
    {
        [SerializeField] private Transform _beamEffect;
        [Tooltip("빔 끝에 둔 SphereCollider. 중심 위치를 길이 기준점으로 사용한다.")]
        [SerializeField] private SphereCollider _beamEndMarker;
        [Tooltip("빔의 가로·세로 굵기 배율")]
        [SerializeField, Min(0.01f)] private float _thickness = 1f;

        private Vector3 _baseScale;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private bool _initialized;

        private void Awake() => Initialize();

        public bool IsConfigured => _beamEffect != null && _beamEndMarker != null;

        // 콜라이더 중심까지의 원래 거리로 Z 배율을 구해 그 중심을 명중점에 놓는다.
        public void Show(Vector3 start, Vector3 end)
        {
            if (!IsConfigured) return;
            Initialize();
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length < 0.01f)
            {
                Hide();
                return;
            }

            Vector3 forward = direction / length;
            _beamEffect.rotation = Quaternion.LookRotation(forward, Vector3.up) * _baseRotation;
            float thickness = Mathf.Max(0.01f, _thickness);
            _beamEffect.localScale = new Vector3(_baseScale.x * thickness, _baseScale.y * thickness, _baseScale.z);
            _beamEffect.position = start;

            float sourceLength = Vector3.Dot(MarkerCenter - start, forward);
            if (sourceLength <= 0.01f)
            {
                Hide();
                return;
            }

            _beamEffect.localScale = new Vector3(_baseScale.x * thickness,
                _baseScale.y * thickness, _baseScale.z * length / sourceLength);
            // 자식의 가로 오프셋까지 반영하여 콜라이더 중심을 정확히 hit.point에 둔다.
            _beamEffect.position += end - MarkerCenter;
            if (!_beamEffect.gameObject.activeSelf) _beamEffect.gameObject.SetActive(true);
        }

        private Vector3 MarkerCenter =>
            _beamEndMarker.transform.TransformPoint(_beamEndMarker.center);

        public void Hide()
        {
            if (_beamEffect == null) return;
            if (_beamEffect.gameObject.activeSelf) _beamEffect.gameObject.SetActive(false);
            if (!_initialized) return;
            _beamEffect.localPosition = _basePosition;
            _beamEffect.localRotation = _baseRotation;
            _beamEffect.localScale = _baseScale;
        }

        private void Initialize()
        {
            if (_initialized || _beamEffect == null) return;
            _initialized = true;
            _basePosition = _beamEffect.localPosition;
            _baseRotation = _beamEffect.localRotation;
            _baseScale = _beamEffect.localScale;

            // 기준점으로만 사용하며 공격 중 물리 충돌은 발생시키지 않는다.
            if (_beamEndMarker != null) _beamEndMarker.enabled = false;

            // 이미 나온 파티클도 부모의 길이 변경을 따르게 한다.
            foreach (ParticleSystem particle in _beamEffect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particle.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        private void OnDisable() => Hide();
    }
}
