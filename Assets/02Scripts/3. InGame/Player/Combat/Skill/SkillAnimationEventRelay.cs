using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>실리온 Animator의 스킬, 재장전, 이동 사운드 이벤트를 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class SkillAnimationEventRelay : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;

        [Header("Locomotion Audio")]
        [SerializeField] private string _jumpAudioKey = "Jump";
        [SerializeField] private AudioClip _jumpAudio;
        [SerializeField] private string _doubleJumpAudioKey = "DoubleJump";
        [SerializeField] private AudioClip _doubleJumpAudio;
        [SerializeField] private string _landAudioKey = "Land";
        [SerializeField] private AudioClip _landAudio;

        public AudioSource AudioSource => _audioSource;

        public void SetAudioSource(AudioSource source) => _audioSource = source;

        private PlayerRoot _playerCore;
        private SkillModule _skillModule;

        private PlayerRoot Player => _playerCore != null
            ? _playerCore : _playerCore = GetComponentInParent<PlayerRoot>();

        private SkillModule Module
        {
            get
            {
                if (_skillModule != null) return _skillModule;
                PlayerRoot player = Player;
                return _skillModule = player != null
                    ? player.GetComponentInChildren<SkillModule>(true) : null;
            }
        }

        // 클립의 String Parameter를 현재 스킬 데이터의 이벤트 키와 대조한다.
        public void OnSkillEvent(string eventKey) => Module?.HandleAnimationEvent(eventKey, _audioSource);

        // 현재 장착 중인 총만 Reload 이벤트를 처리한다.
        public void OnReloadEvent(string eventKey) =>
            Player?.CombatModule?.CurrentGun?.HandleReloadAnimationEvent(eventKey);

        // 이동 클립의 String Parameter와 인스펙터 키를 비교해 한 번 재생한다.
        public void OnLocomotionAudioEvent(string eventKey)
        {
            if (_audioSource == null || !_audioSource.isActiveAndEnabled) return;

            AudioClip clip = eventKey == _jumpAudioKey ? _jumpAudio
                : eventKey == _doubleJumpAudioKey ? _doubleJumpAudio
                : eventKey == _landAudioKey ? _landAudio : null;
            if (clip != null) _audioSource.PlayOneShot(clip);
        }
    }
}
