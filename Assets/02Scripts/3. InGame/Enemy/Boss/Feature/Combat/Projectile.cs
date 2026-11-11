using System;
using System.Collections.Generic;
using alpha.ingame.enemy;
using alpha.ingame.player;
using alpha.system.combat;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    public enum EProjectilePathType
    {
        Straight, // 시작점에서 목표까지 직선으로 이동한다.
        Curve     // 로컬 +Z 방향의 시작과 두 제어점으로 휘는 경로를 정한다.
    }

    public enum EProjectileTrackingType
    {
        FixedPosition, // 발사 시 받은 목표 좌표를 유지한다.
        FollowTarget   // 이동 중 대상의 현재 위치를 따라간다.
    }

    /// <summary>목표를 향해 이동하고 충돌 또는 도착 시 범위 피해를 주는 투사체다.</summary>
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] private EProjectilePathType _pathType = EProjectilePathType.Curve;
        [SerializeField, Min(0.01f)] private float _flightSpeed = 22f;
        [SerializeField, Min(0.01f)] private float _maxFlightDuration = 10f;
        [Tooltip("출발점 기준 로컬 좌표. +Z가 처음 나아갈 방향입니다.")]
        [SerializeField] private Vector3 _curveStartControl = new(0f, 0f, 10f);
        [Tooltip("목표점 기준 로컬 좌표. 곡선이 목표로 꺾이는 정도를 조정합니다.")]
        [SerializeField] private Vector3 _curveEndControl = new(0f, 5f, 10f);
        [SerializeField] private Vector3 _previewTargetOffset = new(0f, -20f, 0f);

        [Header("Impact")]
        [SerializeField, Min(0f)] private float _blastRadius = 4f;
        [SerializeField] private LayerMask _impactLayers = (1 << 6) | (1 << 7);
        [SerializeField] private EnemyHitProfile _hit =
            new(1f, EPlayerHitType.Normal);
        [SerializeField] private GameObject _explosionPrefab;
        [SerializeField, Min(0.01f)] private float _explosionLifetime = 4f;

        private readonly HashSet<Transform> _hitTargets = new();
        private Collider _impactCollider;
        private Rigidbody _body;
        private Vector3 _flightStart;
        private Vector3 _destination;
        private Transform _target;
        private EProjectileTrackingType _trackingType;
        private Quaternion _launchRotation;
        private float _pathProgress;
        private float _pathDuration;
        private float _expiresAt;
        private bool _isFlying;

        public bool HasImpacted { get; private set; }
        // 투사체가 실제로 폭발한 월드 위치를 알린다.
        public event Action<Vector3> Impacted;

        private void OnEnable()
        {
            HasImpacted = false;
            _isFlying = false;
            if (_impactCollider != null) _impactCollider.enabled = true;
        }

        private void Awake()
        {
            _impactCollider = transform.Find("DetectionCollider")?.GetComponent<Collider>();
            if (_impactCollider == null)
            {
                Debug.LogError($"{name}: DetectionCollider가 없습니다.", this);
                enabled = false;
                return;
            }

            _impactCollider.isTrigger = true;
            _body = GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        public bool Launch(Vector3 destination, EProjectileTrackingType trackingType,
            Transform target = null)
        {
            if (_body == null || !isActiveAndEnabled || HasImpacted ||
                (trackingType == EProjectileTrackingType.FollowTarget && target == null))
                return false;
            _trackingType = trackingType;
            _target = target;
            _destination = trackingType == EProjectileTrackingType.FollowTarget
                ? target.position : destination;
            _flightStart = _body.position;
            _launchRotation = _body.rotation;
            _pathDuration = _pathType == EProjectilePathType.Curve
                ? Mathf.Max(Time.fixedDeltaTime,
                    EstimateCurveLength(_flightStart, _destination) /
                    Mathf.Max(0.01f, _flightSpeed))
                : 0f;
            _pathProgress = 0f;
            _expiresAt = Time.time + Mathf.Max(0.01f, _maxFlightDuration);
            _isFlying = true;
            if (_pathType == EProjectilePathType.Curve)
            {
                GetCurveControls(_flightStart, _destination, _curveStartControl,
                    _curveEndControl, out Vector3 first, out _);
                Vector3 initialDirection = first - _flightStart;
                if (initialDirection.sqrMagnitude > 0.0001f)
                    RotateAlong(initialDirection);
            }
            return true;
        }

        private void FixedUpdate()
        {
            if (!_isFlying || HasImpacted || _body == null) return;
            // 추적 대상이 사라지면 마지막으로 확인한 위치까지 진행한다.
            if (_trackingType == EProjectileTrackingType.FollowTarget && _target != null)
                _destination = _target.position;
            if (Time.time >= _expiresAt)
            {
                _isFlying = false;
                Destroy(gameObject);
                return;
            }

            if (_pathType == EProjectilePathType.Straight)
            {
                Vector3 next = Vector3.MoveTowards(_body.position, _destination,
                    _flightSpeed * Time.fixedDeltaTime);
                if (!MoveTo(next, next - _body.position)) return;
                if ((next - _destination).sqrMagnitude <= 0.0001f)
                    TryImpact(null, _destination);
                return;
            }

            _pathProgress = Mathf.Min(1f,
                _pathProgress + Time.fixedDeltaTime / _pathDuration);
            Vector3 current = _body.position;
            GetCurveControls(_flightStart, _destination, _curveStartControl,
                _curveEndControl, out Vector3 firstControl, out Vector3 secondControl);
            Vector3 nextCurvePosition = EvaluateCurve(_flightStart, firstControl,
                secondControl, _destination, _pathProgress);
            if (!MoveTo(nextCurvePosition, nextCurvePosition - current)) return;
            if (_pathProgress >= 1f)
                TryImpact(null, _destination);
        }

        private bool MoveTo(Vector3 position, Vector3 direction)
        {
            // 미사일 앞쪽이 쉴드에 닿으면 피해 없는 폭발로 전환한다.
            Vector3 forward = direction.sqrMagnitude > 0.0001f
                ? direction.normalized : _body.rotation * Vector3.forward;
            Vector3 front = _impactCollider.bounds.center;
            float radius = 0f;
            if (_impactCollider is CapsuleCollider capsule)
            {
                front += forward * ((capsule.height * 0.5f - capsule.radius) *
                    Mathf.Abs(capsule.transform.lossyScale.z));
                radius = capsule.radius * Mathf.Max(
                    Mathf.Abs(capsule.transform.lossyScale.x),
                    Mathf.Abs(capsule.transform.lossyScale.y));
            }
            if (ShieldBarrierRegistry.TryIntercept(front,
                    front + position - _body.position, radius, out Vector3 contact))
            {
                HasImpacted = true;
                OnImpact(null, contact, true);
                return false;
            }
            if (direction.sqrMagnitude > 0.0001f) RotateAlong(direction);
            _body.MovePosition(position);
            return true;
        }

        private void RotateAlong(Vector3 direction)
        {
            // 미사일의 로컬 +Z를 현재 경로의 진행 방향에 맞춘다.
            Quaternion turn = Quaternion.FromToRotation(
                _launchRotation * Vector3.forward, direction.normalized);
            _body.MoveRotation(turn * _launchRotation);
        }

        // 발사 시 회전을 고정하여 제어점의 로컬 +Z가 처음 진행 방향을 뜻하게 한다.
        private void GetCurveControls(Vector3 start, Vector3 end,
            Vector3 startOffset, Vector3 endOffset,
            out Vector3 first, out Vector3 second)
        {
            first = start + _launchRotation * startOffset;
            second = end + _launchRotation * endOffset;
        }

        private static Vector3 EvaluateCurve(Vector3 start, Vector3 first,
            Vector3 second, Vector3 end, float progress)
        {
            float remaining = 1f - progress;
            return remaining * remaining * remaining * start +
                3f * remaining * remaining * progress * first +
                3f * remaining * progress * progress * second +
                progress * progress * progress * end;
        }

        private float EstimateCurveLength(Vector3 start, Vector3 end)
        {
            GetCurveControls(start, end, _curveStartControl, _curveEndControl,
                out Vector3 first, out Vector3 second);
            const int samples = 16;
            float length = 0f;
            Vector3 previous = start;
            for (int i = 1; i <= samples; i++)
            {
                Vector3 position = EvaluateCurve(start, first, second, end,
                    i / (float)samples);
                length += Vector3.Distance(previous, position);
                previous = position;
            }
            return length;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isFlying || other == null ||
                (_impactLayers.value & (1 << other.gameObject.layer)) == 0) return;
            TryImpact(other, transform.position);
        }

        public void Explode() => TryImpact(null, transform.position);

        // 직접 충돌과 목표 지점 도착이 같은 폭발을 중복 실행하지 않도록 막는다.
        public bool TryImpact(Collider target, Vector3 point)
        {
            if (!isActiveAndEnabled || HasImpacted) return false;
            HasImpacted = true;
            OnImpact(target, point, false);
            return true;
        }

        // 직접 맞은 대상도 폭발 범위에서 한 번만 판정한다.
        private void OnImpact(Collider target, Vector3 point, bool blockedByShield)
        {
            _isFlying = false;
            if (_impactCollider != null) _impactCollider.enabled = false;

            Vector3 center = point;
            _hitTargets.Clear();
            if (!blockedByShield) DamageTarget(target, center);
            if (!blockedByShield && _blastRadius > 0f)
            {
                Physics.SyncTransforms();
                foreach (Collider candidate in Physics.OverlapSphere(center, _blastRadius,
                             Physics.AllLayers, QueryTriggerInteraction.Collide))
                    DamageTarget(candidate, center);
            }

            if (_explosionPrefab != null)
            {
                GameObject explosion = Instantiate(_explosionPrefab, center, Quaternion.identity);
                Destroy(explosion, Mathf.Max(0.01f, _explosionLifetime));
            }
            Impacted?.Invoke(center);
            Destroy(gameObject);
        }

        private void DamageTarget(Collider candidate, Vector3 center)
        {
            if (candidate == null) return;
            CharacterController player =
                candidate.GetComponentInParent<CharacterController>();
            if (player == null || !player.enabled) return;
            IPlayerHitReceiver receiver = player.GetComponentInParent(
                typeof(IPlayerHitReceiver)) as IPlayerHitReceiver;
            if (receiver == null || !_hitTargets.Add(player.transform)) return;
            if (ShieldBarrierRegistry.TryIntercept(center, player.bounds.center,
                    0f, out _)) return;

            Vector3 direction = Vector3.ProjectOnPlane(
                player.bounds.center - center, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            receiver.TryReceiveHit(_hit.CreateHit(transform, direction.normalized));
        }

        private void OnValidate()
        {
            _flightSpeed = Mathf.Max(0.01f, _flightSpeed);
            _maxFlightDuration = Mathf.Max(0.01f, _maxFlightDuration);
            _blastRadius = Mathf.Max(0f, _blastRadius);
            _explosionLifetime = Mathf.Max(0.01f, _explosionLifetime);
            _hit.Validate();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _blastRadius);
        }
    }
}
