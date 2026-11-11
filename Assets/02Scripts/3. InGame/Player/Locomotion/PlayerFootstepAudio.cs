using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>지상 이동 애니메이션의 발걸음 신호를 소리로 표현한다.</summary>
    public sealed class PlayerFootstepAudio : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LocomotionModule _locomotion;
        [SerializeField] private LocomotionStateFlow _stateFlow;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip[] _clips;

        [Header("Step Volume")]
        [SerializeField, Min(0.01f)] private float _walkSpeed = 2f;
        [SerializeField, Min(0.01f)] private float _runSpeed = 6f;
        [SerializeField, Range(0f, 1f)] private float _walkVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _runVolume = 1f;

        private int _lastClipIndex = -1;
        private PlayerRoot _playerCore;

        private void OnEnable()
        {
            _playerCore = GetComponentInParent<PlayerRoot>();
            if (_locomotion == null) _locomotion = GetComponent<LocomotionModule>();
            if (_stateFlow == null)
                _stateFlow = _playerCore?.GetComponentInChildren<LocomotionStateFlow>(true);
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

            // 기존 씬에 새 볼륨 필드가 없어서 둘 다 0으로 로드된 경우 기본값을 복원한다.
            if (_walkVolume <= 0f && _runVolume <= 0f)
            {
                _walkVolume = 0.5f;
                _runVolume = 1f;
            }

        }

        // AnimationView의 발걸음 시점에만 호출하며 강제 이동은 발소리에서 제외한다.
        public void PlayFootstep(EMoveType moveType)
        {
            if (_locomotion == null || _stateFlow == null ||
                _audioSource == null || _clips == null || _clips.Length == 0)
                return;

            if (_playerCore?.ActionStateFlow?.IsActionBlocked == true ||
                _playerCore?.HitModule?.IsHitMotionActive == true ||
                _playerCore?.MotionModule?.IsAttractionActive == true ||
                !_locomotion.IsGrounded || _locomotion.IsBoosting ||
                _stateFlow.CurrentState != ELocomotionState.Move ||
                moveType is not (EMoveType.Walk or EMoveType.Run or EMoveType.Combat))
                return;

            // 볼륨만 플레이어의 입력 이동 속도로 보간한다.
            float blend = Mathf.InverseLerp(_walkSpeed, _runSpeed,
                _locomotion.CurrentSpeed);
            PlayStep(moveType, Mathf.Lerp(_walkVolume, _runVolume, blend));
        }

        private void PlayStep(EMoveType moveType, float volume)
        {
            int index;
            if (moveType == EMoveType.Walk && _clips.Length >= 2)
            {
                // Walk는 인스펙터의 1·2번 클립만 번갈아 사용한다.
                index = _lastClipIndex == 0 ? 1 : 0;
            }
            else
            {
                // 다른 이동은 전체 클립을 쓰되 직전 클립은 피한다.
                index = Random.Range(0, _clips.Length);
                if (_clips.Length > 1 && index == _lastClipIndex)
                    index = (index + Random.Range(1, _clips.Length)) % _clips.Length;
            }

            _lastClipIndex = index;
            if (_clips[index] != null) _audioSource.PlayOneShot(_clips[index], volume);
        }
    }
}
