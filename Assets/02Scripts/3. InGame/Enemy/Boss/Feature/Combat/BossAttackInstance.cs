using System.Collections.Generic;
using alpha.ingame.enemy;
using alpha.ingame.player;
using alpha.system.combat;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    public enum EBossAttackHitMode
    {
        /// <summary>첫 접촉 때 범위 안의 대상들을 한 번 타격하고 판정을 닫는다.</summary>
        OnceOnContact,
        /// <summary>판정 시간 동안 대상별 간격에 맞춰 반복 타격한다.</summary>
        Continuous
    }

    public enum EBossAttackEndAction
    {
        /// <summary>씬에 배치된 공격 객체를 비활성화한다.</summary>
        Disable,
        /// <summary>생성된 공격 객체를 삭제한다.</summary>
        Destroy
    }

    public enum EBossAttackTimingMode
    {
        /// <summary>Start Time과 Duration으로 판정 구간을 계산한다.</summary>
        Duration,
        /// <summary>애니메이션 이벤트가 Begin과 End를 호출한다.</summary>
        AnimationEvent
    }

    public enum EBossAttackDetectionMode
    {
        /// <summary>설정된 Area를 판정 시간 동안 조회한다.</summary>
        AreaQuery,
        /// <summary>공격 객체의 접촉 기능이 검증한 대상을 전달한다.</summary>
        ExternalContact
    }

    /// <summary>공격 한 번의 판정 시간, Hit 전달, 종료를 관리한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BossAttackInstance : MonoBehaviour
    {
        [SerializeField] private EBossAttackTimingMode _timingMode;
        [SerializeField] private EBossAttackDetectionMode _detectionMode;
        [SerializeField, Min(0f)] private float _startTime;
        [SerializeField, Min(0.01f)] private float _duration = 0.25f;
        [SerializeField] private EBossAttackHitMode _hitMode;
        [SerializeField, Min(0.01f)] private float _hitInterval = 0.5f;
        [SerializeField] private EnemyHitProfile _hit =
            new(0f, EPlayerHitType.Normal);
        [SerializeField] private EBossAttackEndAction _endAction;
        [SerializeField] private bool _endAfterParticles;
        [SerializeField] private bool _shieldBlockable;

        private readonly Dictionary<Transform, float> _nextHitAt = new();
        private BossAttackAreaModule _area;
        private ParticleSystem[] _particles;
        private float _startsAt;
        private float _endsAt;
        private bool _hitWindowActive;
        private bool _particlesPlayed;
        private bool _ending;
        private Vector3 _previousShieldPosition;

        public EPlayerHitType HitType => _hit.HitType;
        public bool IsHitWindowActive => _hitWindowActive;

        private void Awake()
        {
            _area = GetComponent<BossAttackAreaModule>();
            if (_endAfterParticles)
                _particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        // 자체 시간 방식은 설정한 구간을, 이벤트 방식은 Begin부터 End까지 검사한다.
        public void Begin()
        {
            gameObject.SetActive(true);
            _ending = false;
            _nextHitAt.Clear();
            _particlesPlayed = false;
            _hitWindowActive = _detectionMode == EBossAttackDetectionMode.ExternalContact ||
                _area != null && _area.IsValid;
            _startsAt = Time.time + (_timingMode == EBossAttackTimingMode.Duration
                ? _startTime : 0f);
            _endsAt = _timingMode == EBossAttackTimingMode.Duration
                ? _startsAt + _duration : float.PositiveInfinity;
            _previousShieldPosition = transform.position;
            if (_hitWindowActive && Time.time >= _startsAt &&
                _detectionMode == EBossAttackDetectionMode.AreaQuery)
                ScanTargets();
        }

        private void Update()
        {
            if (_shieldBlockable && _hitWindowActive)
            {
                Vector3 position = transform.position;
                if (ShieldBarrierRegistry.TryIntercept(_previousShieldPosition,
                        position, _area != null ? _area.Radius : 0f, out _))
                {
                    End();
                    Destroy(gameObject);
                    return;
                }
                _previousShieldPosition = position;
            }
            if (_hitWindowActive)
            {
                if (_timingMode == EBossAttackTimingMode.Duration &&
                    Time.time >= _endsAt) CloseHitWindow();
                else if (Time.time >= _startsAt &&
                    _detectionMode == EBossAttackDetectionMode.AreaQuery)
                    ScanTargets();
            }

            if (_ending || _timingMode == EBossAttackTimingMode.AnimationEvent) return;
            // 파티클이 없는 생성 공격은 판정 시간이 끝나면 스스로 삭제한다.
            if (!_endAfterParticles)
            {
                if (_endAction == EBossAttackEndAction.Destroy &&
                    !_hitWindowActive && Time.time >= _endsAt) End();
                return;
            }
            bool alive = false;
            foreach (ParticleSystem particle in _particles)
                if (particle != null && particle.gameObject.activeInHierarchy &&
                    particle.IsAlive(true))
                {
                    alive = true;
                    break;
                }
            if (alive) _particlesPlayed = true;
            else if ((_particlesPlayed || _particles.Length == 0) &&
                     !_hitWindowActive) End();
        }

        private void ScanTargets()
        {
            Collider[] candidates = _area.OverlapTargets(out int count, out Vector3 center,
                out Quaternion rotation);
            bool delivered = false;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = candidates[i];
                if (!_area.Contains(candidate, center, rotation)) continue;
                if (_shieldBlockable && ShieldBarrierRegistry.TryIntercept(
                        center, candidate.bounds.center, 0f, out _)) continue;
                IPlayerHitReceiver receiver = candidate.GetComponentInParent(
                    typeof(IPlayerHitReceiver)) as IPlayerHitReceiver;
                if (receiver is not Component component) continue;
                Transform target = component.transform;
                if (_nextHitAt.TryGetValue(target, out float nextTime) &&
                    Time.time < nextTime) continue;

                Vector3 direction = Vector3.ProjectOnPlane(
                    candidate.bounds.center - center, Vector3.up);
                if (direction.sqrMagnitude <= 0.0001f)
                    direction = Vector3.ProjectOnPlane(
                        candidate.bounds.center - _area.Source.position, Vector3.up);
                direction = direction.sqrMagnitude > 0.0001f
                    ? direction.normalized : rotation * Vector3.forward;

                // 피해 무적에 막힌 시도도 한 차례의 판정으로 센다.
                _nextHitAt[target] = _hitMode == EBossAttackHitMode.OnceOnContact
                    ? float.PositiveInfinity : Time.time + _hitInterval;
                receiver.TryReceiveHit(_hit.CreateHit(_area.Source, direction));
                delivered = true;
            }
            if (delivered && _hitMode == EBossAttackHitMode.OnceOnContact)
                CloseHitWindow();
        }

        // 접촉 기능은 명중 조건과 방향을 계산하고, 피격 상태는 공격 인스턴스가 소유한다.
        public bool TryHit(Collider candidate, Vector3 direction,
            float minimumDistance = 0f, float moveDurationOverride = 0f,
            Collider[] contactBodies = null, float contactPadding = 0f)
        {
            if (_detectionMode != EBossAttackDetectionMode.ExternalContact ||
                !_hitWindowActive || candidate == null || !candidate.enabled ||
                Time.time < _startsAt || Time.time >= _endsAt) return false;
            IPlayerHitReceiver receiver = candidate.GetComponentInParent(
                typeof(IPlayerHitReceiver)) as IPlayerHitReceiver;
            if (receiver is not Component component) return false;
            Transform target = component.transform;
            if (_nextHitAt.TryGetValue(target, out float nextTime) &&
                Time.time < nextTime) return false;

            _nextHitAt[target] = _hitMode == EBossAttackHitMode.OnceOnContact
                ? float.PositiveInfinity : Time.time + _hitInterval;
            receiver.TryReceiveHit(_hit.CreateHit(_area != null ? _area.Source : transform,
                direction, minimumDistance, moveDurationOverride,
                contactBodies, contactPadding));
            if (_hitMode == EBossAttackHitMode.OnceOnContact) CloseHitWindow();
            return true;
        }

        private void CloseHitWindow()
        {
            _hitWindowActive = false;
            _nextHitAt.Clear();
        }

        // 배치 객체는 EffectEnd에서, 생성 객체는 파티클 또는 자신의 종료 시점에 닫는다.
        public void End()
        {
            if (_ending) return;
            _ending = true;
            CloseHitWindow();
            if (_endAction == EBossAttackEndAction.Destroy)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }

        private void OnDisable() => CloseHitWindow();

        private void OnValidate()
        {
            _startTime = Mathf.Max(0f, _startTime);
            _duration = Mathf.Max(0.01f, _duration);
            _hitInterval = Mathf.Max(0.01f, _hitInterval);
            _hit.Validate();
        }
    }
}
