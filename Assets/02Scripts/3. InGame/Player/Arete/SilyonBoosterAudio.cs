using System.Collections;
using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>실리온 부스터의 시작, 주기, 종료 사운드를 재생한다.</summary>
    public sealed class SilyonBoosterAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _startClip;
        [SerializeField] private AudioClip _pulseClip;
        [SerializeField] private AudioClip _endClip;
        [SerializeField, Min(0.1f)] private float _pulseInterval = 3.5f;

        private LocomotionModule _locomotion;
        private Coroutine _pulseRoutine;

        public void SetAudioSource(AudioSource source) => _audioSource = source;

        private void OnEnable()
        {
            _locomotion = GetComponentInParent<PlayerRoot>(true)?.LocomotionModule;
            if (_locomotion == null) return;

            _locomotion.OnBoostStateChanged += HandleBoostStateChanged;
            if (_locomotion.IsBoosting) HandleBoostStateChanged(true);
        }

        private void OnDisable()
        {
            if (_locomotion != null)
                _locomotion.OnBoostStateChanged -= HandleBoostStateChanged;
            StopPulse();
            if (_audioSource != null) _audioSource.Stop();
        }

        private void HandleBoostStateChanged(bool isBoosting)
        {
            StopPulse();
            if (_audioSource == null) return;
            _audioSource.Stop();

            if (!isBoosting)
            {
                if (_endClip != null) _audioSource.PlayOneShot(_endClip);
                return;
            }

            if (_startClip != null) _audioSource.PlayOneShot(_startClip);
            if (_pulseClip == null) return;

            // 시작음이 있으면 끝난 뒤 첫 주기음을 재생한다.
            float delay = _startClip == null ? 0f : Mathf.Max(_pulseInterval, _startClip.length);
            _pulseRoutine = StartCoroutine(PlayPulses(delay));
        }

        private IEnumerator PlayPulses(float firstDelay)
        {
            if (firstDelay > 0f) yield return new WaitForSeconds(firstDelay);

            while (_locomotion != null && _locomotion.IsBoosting)
            {
                _audioSource.PlayOneShot(_pulseClip);
                // 긴 클립이 다음 재생과 겹치지 않도록 실제 클립 길이도 고려한다.
                yield return new WaitForSeconds(Mathf.Max(0.1f, _pulseInterval, _pulseClip.length));
            }
            _pulseRoutine = null;
        }

        private void StopPulse()
        {
            if (_pulseRoutine == null) return;
            StopCoroutine(_pulseRoutine);
            _pulseRoutine = null;
        }
    }
}
