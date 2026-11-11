using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>보스 추적 애니메이션의 발 디딤을 전진·제자리 회전 중에 재생한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class BossLocomotionAudioView : MonoBehaviour
    {
        [SerializeField] private AudioClip[] _footsteps;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;

        private BossLocomotionStateFlow _stateFlow;
        private AudioSource _source;
        private int _lastClipIndex = -1;

        private void Awake()
        {
            _stateFlow = GetComponent<BossLocomotionStateFlow>();
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 1f;
        }

        // Walk_F 이벤트를 사용하므로 추적 중 제자리 회전에도 발소리가 난다.
        public void PlayFootstep()
        {
            if (!isActiveAndEnabled || _stateFlow == null ||
                _source == null || !_source.isActiveAndEnabled ||
                _stateFlow.CurrentState != EBossLocomotionState.Chasing ||
                _footsteps == null || _footsteps.Length == 0) return;

            int index = Random.Range(0, _footsteps.Length);
            if (_footsteps.Length > 1 && index == _lastClipIndex)
                index = (index + Random.Range(1, _footsteps.Length)) % _footsteps.Length;
            _lastClipIndex = index;
            if (_footsteps[index] != null)
                _source.PlayOneShot(_footsteps[index], _volume);
        }

        public void StopFootsteps() => _source?.Stop();
    }
}
