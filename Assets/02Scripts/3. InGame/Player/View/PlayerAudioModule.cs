using alpha.system.audio;
using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>플레이어의 피격·치명타와 모델별 소리를 전용 채널로 재생한다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAudioModule : MonoBehaviour
    {
        [Header("Hit")]
        [SerializeField] private AudioClip _normalHitClip;
        [SerializeField] private AudioClip _staggerHitClip;
        [SerializeField] private AudioClip _knockbackHitClip;
        [SerializeField] private AudioClip _knockdownHitClip;
        [SerializeField] private AudioSource _hitSource;
        [SerializeField, Range(0f, 1f)] private float _hitVolume = 1f;
        [SerializeField, Min(0f)] private float _hitInterval = 0.12f;
        [SerializeField, Tooltip("피격 음성과 동시에 재생할 공통 타격음입니다.")]
        private AudioClip _hitImpactClip;
        [SerializeField, Range(0f, 1f)] private float _hitImpactVolume = 0.5f;

        [Header("Critical Hit")]
        [SerializeField, Tooltip("실제 치명타 피해가 적용됐을 때 재생할 소리입니다. 비워두면 재생하지 않습니다.")]
        private AudioClip _criticalHitClip;
        [SerializeField, Tooltip("치명타 소리만 재생하는 AudioSource입니다. 비워두면 Audios 아래에 자동으로 준비합니다.")]
        private AudioSource _criticalHitSource;
        [SerializeField, Range(0f, 1f), Tooltip("치명타 소리의 재생 볼륨입니다.")]
        private float _criticalHitVolume = 1f;
        [SerializeField, Min(0f), Tooltip("연속 치명타 소리의 최소 재생 간격(초)입니다.")]
        private float _criticalHitInterval = 0.12f;

        private AudioSource _animationSource;
        private AudioSource _boosterSource;
        private AudioSource _hitImpactSource;
        private float _nextHitSoundAt;
        private float _nextCriticalHitSoundAt;

        // AnimationView가 피격 클립을 시작한 타입에만 소리를 재생한다.
        public void PlayHit(EPlayerHitType hitType)
        {
            AudioClip clip = hitType switch
            {
                EPlayerHitType.Normal => _normalHitClip,
                EPlayerHitType.Stagger => _staggerHitClip,
                EPlayerHitType.Knockback => _knockbackHitClip,
                EPlayerHitType.Knockdown => _knockdownHitClip,
                _ => null
            };
            if (clip == null || Time.time < _nextHitSoundAt) return;

            if (_hitSource == null)
            {
                Transform channel = transform.Find("Hit");
                _hitSource = channel != null ? channel.GetComponent<AudioSource>() : null;
                if (_hitSource == null) _hitSource = CreateSource("Hit", EAudioBus.Voice);
            }

            _nextHitSoundAt = Time.time + _hitInterval;
            _hitSource.PlayOneShot(clip, _hitVolume);
            // 같은 시점에 재생하되 믹서에서 음성과 타격음을 별도로 조절한다.
            if (_hitImpactClip != null)
            {
                if (_hitImpactSource == null)
                    _hitImpactSource = CreateSource("HitImpact", EAudioBus.Player);
                _hitImpactSource.PlayOneShot(_hitImpactClip, _hitImpactVolume);
            }
        }

        public void PlayCriticalHit()
        {
            if (_criticalHitClip == null || Time.time < _nextCriticalHitSoundAt) return;

            if (_criticalHitSource == null)
            {
                Transform channel = transform.Find("CriticalHit");
                _criticalHitSource = channel != null ? channel.GetComponent<AudioSource>() : null;
                if (_criticalHitSource == null)
                    _criticalHitSource = CreateSource("CriticalHit", EAudioBus.Player);
            }

            _nextCriticalHitSoundAt = Time.time + _criticalHitInterval;
            _criticalHitSource.PlayOneShot(_criticalHitClip, _criticalHitVolume);
        }

        public void BindModel(GameObject model)
        {
            if (model == null) return;

            foreach (SkillAnimationEventRelay relay in model.GetComponentsInChildren<SkillAnimationEventRelay>(true))
            {
                if (_animationSource == null)
                {
                    // 기존 SFX2가 있으면 재사용하고, 없으면 Audios 아래에 만든다.
                    Transform existingChannel = transform.Find("SFX2");
                    _animationSource = existingChannel != null
                        ? existingChannel.GetComponent<AudioSource>() : null;
                    if (_animationSource == null)
                        _animationSource = CreateSource("Animation", EAudioBus.Player);
                }
                relay.SetAudioSource(_animationSource);
            }

            foreach (SilyonBoosterAudio booster in model.GetComponentsInChildren<SilyonBoosterAudio>(true))
            {
                if (_boosterSource == null)
                    _boosterSource = CreateSource("Booster", EAudioBus.Player);
                booster.SetAudioSource(_boosterSource);
            }
        }

        private AudioSource CreateSource(string channelName, EAudioBus bus)
        {
            // 발소리 채널과 분리해 Stop() 호출이 다른 효과음을 끊지 않게 한다.
            GameObject channel = new GameObject(channelName);
            channel.transform.SetParent(transform, false);
            AudioSource source = channel.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = AudioMixModule.Instance?.GetGroup(bus);
            return source;
        }
    }
}
