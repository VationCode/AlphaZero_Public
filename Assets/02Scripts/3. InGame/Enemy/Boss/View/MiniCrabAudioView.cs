using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>MiniCrab이 움직이는 동안 두 음성 클립을 번갈아 무작위로 반복한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class MiniCrabAudioView : MonoBehaviour
    {
        [SerializeField] private AudioClip[] _clips;

        private MiniCrabMotionModule _motion;
        private AudioSource _source;
        private int _lastClipIndex = -1;

        private void Awake()
        {
            _motion = GetComponent<MiniCrabMotionModule>();
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false; // 클립이 끝날 때마다 다시 선택한다.
            _source.spatialBlend = 1f;
        }

        private void OnEnable()
        {
            if (_motion == null) return;
            _motion.DirectionChanged += HandleDirectionChanged;
            if (_motion.Direction != 0) PlayNext();
        }

        private void Update()
        {
            if (_motion != null && _motion.Direction != 0 && _source != null &&
                !_source.isPlaying && Time.timeScale > 0f && !AudioListener.pause)
                PlayNext();
        }

        private void HandleDirectionChanged(int direction)
        {
            if (direction != 0)
            {
                if (_source != null && !_source.isPlaying) PlayNext();
                return;
            }

            if (_source != null) _source.Stop();
        }

        private void PlayNext()
        {
            if (_source == null || !_source.isActiveAndEnabled ||
                _clips == null || _clips.Length == 0) return;

            int index = Random.Range(0, _clips.Length);
            if (_clips.Length > 1 && index == _lastClipIndex)
                index = (index + Random.Range(1, _clips.Length)) % _clips.Length;
            AudioClip clip = _clips[index];
            if (clip == null) return;

            _lastClipIndex = index;
            _source.clip = clip;
            _source.Play();
        }

        private void OnDisable()
        {
            if (_motion != null) _motion.DirectionChanged -= HandleDirectionChanged;
            if (_source != null) _source.Stop();
            _lastClipIndex = -1;
        }
    }
}
