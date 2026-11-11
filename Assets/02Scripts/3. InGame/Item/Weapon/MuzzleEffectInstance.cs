using UnityEngine;

namespace alpha.ingame.item
{
    /// <summary>총구를 따라 이펙트를 재생하고 종료되면 풀에 반환한다.</summary>
    public sealed class MuzzleEffectInstance : MonoBehaviour
    {
        private MuzzleEffectPool _pool;
        private ParticleSystem[] _particles;
        private Transform _muzzlePoint;
        private float _earliestReturnAt;
        private float _maxReturnAt;

        public void Initialize(MuzzleEffectPool pool)
        {
            _pool = pool;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in _particles)
            {
                // 풀에서 재사용하므로 재생 종료 시 인스턴스를 삭제하지 않는다.
                ParticleSystem.MainModule main = particle.main;
                main.stopAction = ParticleSystemStopAction.None;
            }
        }

        public void Play(Transform muzzlePoint, float maxLifetime)
        {
            _muzzlePoint = muzzlePoint;
            transform.SetPositionAndRotation(muzzlePoint.position, muzzlePoint.rotation);
            gameObject.SetActive(true);

            foreach (ParticleSystem particle in _particles)
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(false);
            }

            _earliestReturnAt = Time.time + 0.03f;
            _maxReturnAt = Time.time + maxLifetime;
        }

        private void LateUpdate()
        {
            if (_pool == null) return;
            if (_muzzlePoint != null)
                transform.SetPositionAndRotation(_muzzlePoint.position, _muzzlePoint.rotation);

            if (Time.time < _earliestReturnAt) return;
            if (Time.time < _maxReturnAt && ParticlesAreAlive()) return;

            _muzzlePoint = null;
            foreach (ParticleSystem particle in _particles)
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _pool.Return(this);
        }

        private bool ParticlesAreAlive()
        {
            if (_particles.Length == 0) return true; // 파티클 외 이펙트는 최대 수명까지 표시한다.
            foreach (ParticleSystem particle in _particles)
                if (particle.IsAlive(false)) return true;
            return false;
        }
    }
}
