using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.item
{
    /// <summary>단일/범위, 즉시/지속 피해를 실행하고 이펙트 종료 후 풀에 반환한다.</summary>
    public sealed class ImpactInstance : MonoBehaviour
    {
        private readonly HashSet<int> _damagedTargets = new();
        private ImpactPool _pool;
        private ImpactData _data;
        private ParticleSystem[] _particles;
        private MonoBehaviour _singleTarget;
        private Transform _owner;
        private IAttackDamageResolver _damageResolver;
        private Vector3 _impactPoint;
        private float _damage;
        private float _endsAt;
        private float _nextTickAt;
        private float _earliestReturnAt;
        private float _visualFallbackAt;

        public void Initialize(ImpactPool pool, ImpactData data)
        {
            _pool = pool;
            _data = data;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in _particles)
            {
                // 풀에서 꺼낼 때는 명중 위치를 지정한 뒤 직접 재생한다.
                ParticleSystem.MainModule main = particle.main;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
            }
        }

        public void Play(RaycastHit hit, Transform owner, float damage, Vector3 fallbackNormal,
            Vector3? contactPointOverride = null, IAttackDamageResolver damageResolver = null)
        {
            _owner = owner;
            _damageResolver = damageResolver;
            _damage = damage;
            _impactPoint = contactPointOverride ?? hit.point;
            _singleTarget = FindDamageable(hit.collider);
            Vector3 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : fallbackNormal;
            transform.SetPositionAndRotation(_impactPoint + normal * 0.01f, Quaternion.LookRotation(normal));
            gameObject.SetActive(true);

            foreach (ParticleSystem particle in _particles)
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(false);
            }

            float now = Time.time;
            _endsAt = _data.Timing == EImpactTiming.Duration ? now + _data.Duration : now;
            _nextTickAt = now + _data.TickInterval;
            _earliestReturnAt = now + 0.05f;
            _visualFallbackAt = now + _data.EffectFallbackLifetime;
            ApplyTick(); // 충돌 순간 첫 피해를 준다.
        }

        private void Update()
        {
            if (_pool == null) return;
            if (_data.Timing == EImpactTiming.Duration && Time.time < _endsAt && Time.time >= _nextTickAt)
            {
                ApplyTick();
                _nextTickAt = Time.time + _data.TickInterval;
            }

            if (Time.time >= _endsAt && VisualFinished()) _pool.Return(this);
        }

        private void ApplyTick()
        {
            if (_data.TargetMode == EImpactTargetMode.Single)
            {
                Damage(_singleTarget, _impactPoint);
                return;
            }

            // 한 대상의 여러 콜라이더가 겹쳐도 한 틱에 피해는 한 번만 준다.
            _damagedTargets.Clear();
            Collider[] hits = Physics.OverlapSphere(_impactPoint, _data.Radius,
                _data.TargetLayers, QueryTriggerInteraction.Collide);
            foreach (Collider collider in hits)
            {
                MonoBehaviour target = FindDamageable(collider);
                if (target != null && _damagedTargets.Add(target.GetInstanceID()))
                    Damage(target, collider.ClosestPoint(_impactPoint));
            }
        }

        private void Damage(MonoBehaviour target, Vector3 hitPoint)
        {
            if (target == null || (_owner != null && target.transform.IsChildOf(_owner))) return;
            if (target is IDamageable damageable)
            {
                GameObject source = _owner != null ? _owner.gameObject : null;
                if (_damageResolver != null)
                    _damageResolver.ApplyDamage(damageable, _damage, source, hitPoint);
                else
                    damageable.TakeDamage(new AttackDamage(_damage, false, source, hitPoint));
            }
        }

        private bool VisualFinished()
        {
            if (Time.time < _earliestReturnAt) return false;
            if (_particles.Length == 0) return Time.time >= _visualFallbackAt;
            foreach (ParticleSystem particle in _particles)
                if (particle.IsAlive(false)) return false;
            return true;
        }

        private static MonoBehaviour FindDamageable(Collider collider)
        {
            if (collider == null) return null;
            foreach (MonoBehaviour behaviour in collider.GetComponentsInParent<MonoBehaviour>())
                if (behaviour is IDamageable) return behaviour;
            return null;
        }
    }
}
