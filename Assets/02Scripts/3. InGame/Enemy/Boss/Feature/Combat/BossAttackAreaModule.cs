using System;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    public enum EBossAttackAreaShape
    {
        /// <summary>각도 360도는 원형, 그보다 작으면 전방 부채꼴이다.</summary>
        Circle,
        /// <summary>공격 원점에서 전방으로 뻗는 직육면체다.</summary>
        Box
    }

    /// <summary>공격 범위를 정의하고 영역 안의 콜라이더를 조회한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BossAttackAreaModule : MonoBehaviour
    {
        [SerializeField] private LayerMask _targetLayers = 1 << 7;
        [SerializeField] private EBossAttackAreaShape _shape;
        [SerializeField] private Vector3 _localOffset;
        [SerializeField] private float _yaw;
        [SerializeField, Min(0f)] private float _radius = 1f;
        [SerializeField, Range(0f, 360f)] private float _angle = 90f;
        [SerializeField, Min(0f)] private float _width = 1f;
        [SerializeField, Min(0f)] private float _length = 1f;
        [SerializeField, Min(0f)] private float _height = 2f;
        [SerializeField] private bool _showArea;
        [SerializeField] private Color _areaColor = new(1f, 0.4f, 0.2f, 0.8f);

        private Transform _source;
        private readonly Collider[] _targetCandidates = new Collider[32];
        public Transform Source => _source != null ? _source : transform;
        public EBossAttackAreaShape Shape => _shape;
        public float Width => _width;
        public float Length => _length;
        public float Height => _height;
        public float Angle => _angle;
        public bool ShowArea => _showArea;
        public Color AreaColor => _areaColor;
        public float Radius => _radius;

        // Attack1의 배치 이펙트는 보스 앞에 있지만 판정 원점은 보스다.
        public void Bind(Transform source) => _source = source;

        public bool IsValid => _height > 0f &&
            (_shape == EBossAttackAreaShape.Circle ? _radius > 0f :
                _width > 0f && _length > 0f);

        // 실제 박스 또는 원형 후보를 찾고, 부채꼴의 각도는 Contains에서 확인한다.
        public Collider[] OverlapTargets(out int count, out Vector3 center,
            out Quaternion rotation)
        {
            count = 0;
            center = default;
            rotation = default;
            if (!IsValid || !isActiveAndEnabled) return Array.Empty<Collider>();
            Physics.SyncTransforms();
            GetAreaPose(Source, out center, out rotation);
            if (_shape == EBossAttackAreaShape.Box)
            {
                Vector3 boxCenter = center + rotation * Vector3.forward * (_length * 0.5f);
                Vector3 halfExtents = new Vector3(_width, _height, _length) * 0.5f;
                count = Physics.OverlapBoxNonAlloc(boxCenter, halfExtents,
                    _targetCandidates, rotation, _targetLayers, QueryTriggerInteraction.Collide);
                // 버퍼가 찬 경우에는 기존 조회로 모든 후보를 확인한다.
                if (count == _targetCandidates.Length)
                {
                    Collider[] all = Physics.OverlapBox(boxCenter, halfExtents,
                        rotation, _targetLayers, QueryTriggerInteraction.Collide);
                    count = all.Length;
                    return all;
                }
            }
            else
            {
                float queryRadius = Mathf.Sqrt(_radius * _radius +
                    _height * _height * 0.25f);
                count = Physics.OverlapSphereNonAlloc(center, queryRadius,
                    _targetCandidates, _targetLayers, QueryTriggerInteraction.Collide);
                if (count == _targetCandidates.Length)
                {
                    Collider[] all = Physics.OverlapSphere(center, queryRadius,
                        _targetLayers, QueryTriggerInteraction.Collide);
                    count = all.Length;
                    return all;
                }
            }
            return _targetCandidates;
        }

        public bool Contains(Collider candidate, Vector3 center, Quaternion rotation)
        {
            return candidate != null && candidate.enabled &&
                   (_shape == EBossAttackAreaShape.Box ||
                    IsInCircle(candidate, center, rotation));
        }

        private bool IsInCircle(Collider candidate, Vector3 center, Quaternion rotation)
        {
            Vector3 offset = candidate.ClosestPoint(center) - center;
            if (Mathf.Abs(offset.y) > _height * 0.5f) return false;
            offset.y = 0f;
            return offset.sqrMagnitude <= _radius * _radius &&
                   (_angle >= 360f || offset.sqrMagnitude < 0.0001f ||
                    Vector3.Angle(rotation * Vector3.forward, offset) <= _angle * 0.5f);
        }

        public void GetAreaPose(Transform source, out Vector3 center,
            out Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(source.forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f) forward = Vector3.forward;
            rotation = Quaternion.LookRotation(forward) * Quaternion.AngleAxis(_yaw, Vector3.up);
            center = source.position + rotation * _localOffset;
        }

        private void OnValidate()
        {
            _radius = Mathf.Max(0f, _radius);
            _angle = Mathf.Clamp(_angle, 0f, 360f);
            _width = Mathf.Max(0f, _width);
            _length = Mathf.Max(0f, _length);
            _height = Mathf.Max(0f, _height);
        }

    }
}
