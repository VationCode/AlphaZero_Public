using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>배치된 BondageChain의 자식 파티클 생명주기를 스킬 시간에 맞춘다.</summary>
    public sealed class BondageChainEffectLifetime : MonoBehaviour
    {
        private ParticleSystem[] _particles;

        private void Awake() => _particles = GetComponentsInChildren<ParticleSystem>(true);

        public void Activate(float duration)
        {
            duration = Mathf.Max(0.1f, duration);

            // 일회성 버스트가 일찍 사라지지 않도록 실제 입자 수명을 맞춘다.
            if (_particles == null) _particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in _particles)
            {
                if (particle == null) continue;
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = particle.main;
                main.playOnAwake = false;
                main.startLifetime = duration;
                main.stopAction = ParticleSystemStopAction.None;
                particle.Play(false);
            }
        }

    }
}
