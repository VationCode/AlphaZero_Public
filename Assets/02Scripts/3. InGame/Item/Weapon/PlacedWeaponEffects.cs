using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.item
{
    /// <summary>무기 이펙트 위치 아래에 미리 배치한 자식들을 재생하고 정지한다.</summary>
    public sealed class PlacedWeaponEffects
    {
        private sealed class Slot
        {
            private readonly GameObject _root;
            private readonly ParticleSystem[] _particles;
            private bool _playing;
            private float _earliestStopAt;
            private float _stopAt;

            public Slot(GameObject root)
            {
                _root = root;
                _particles = root.GetComponentsInChildren<ParticleSystem>(true);
                // 파티클의 Stop Action이 자식을 삭제하거나 비활성화하지 않게 한다.
                foreach (ParticleSystem particle in _particles)
                {
                    ParticleSystem.MainModule main = particle.main;
                    main.stopAction = ParticleSystemStopAction.None;
                }
                Stop();
            }

            public Transform Root => _root != null ? _root.transform : null;

            public void Play(float maxLifetime)
            {
                if (_root == null) return;

                _root.SetActive(true);
                foreach (ParticleSystem particle in _particles)
                {
                    if (particle == null) continue;
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.Play(false);
                }

                _playing = true;
                _earliestStopAt = Time.time + 0.03f;
                _stopAt = maxLifetime > 0f ? Time.time + maxLifetime : float.PositiveInfinity;
            }

            public void Tick()
            {
                if (!_playing || Time.time < _earliestStopAt) return;
                if (Time.time < _stopAt && ParticlesAreAlive()) return;
                Stop();
            }

            public void Stop()
            {
                _playing = false;
                if (_root == null) return;

                if (_root.activeSelf)
                {
                    foreach (ParticleSystem particle in _particles)
                        if (particle != null)
                            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _root.SetActive(false);
                }
            }

            private bool ParticlesAreAlive()
            {
                if (_particles.Length == 0) return true;
                foreach (ParticleSystem particle in _particles)
                    if (particle != null && particle.IsAlive(false)) return true;
                return false;
            }
        }

        private readonly Slot[] _slots;
        private readonly float _maxLifetime;
        private int _nextSlot;

        public PlacedWeaponEffects(Transform point, float maxLifetime = 0f)
        {
            _maxLifetime = maxLifetime;
            if (point == null)
            {
                _slots = System.Array.Empty<Slot>();
                return;
            }

            var slots = new List<Slot>(point.childCount);
            foreach (Transform child in point)
                slots.Add(new Slot(child.gameObject));
            _slots = slots.ToArray();
        }

        // 여러 자식이 있으면 발사마다 순서대로 재생해 효과가 겹칠 수 있게 한다.
        public void PlayNext()
        {
            if (_slots.Length == 0) return;
            _slots[_nextSlot].Play(_maxLifetime);
            _nextSlot = (_nextSlot + 1) % _slots.Length;
        }

        public void PlayAll()
        {
            foreach (Slot slot in _slots) slot.Play(_maxLifetime);
        }

        // 배치된 자식 중 이벤트에 지정한 이펙트만 재생한다.
        public void Play(Transform root)
        {
            if (root == null) return;
            foreach (Slot slot in _slots)
                if (slot.Root == root)
                {
                    slot.Play(_maxLifetime);
                    return;
                }
        }

        public void Tick()
        {
            foreach (Slot slot in _slots) slot.Tick();
        }

        public void StopAll()
        {
            foreach (Slot slot in _slots) slot.Stop();
        }
    }
}
