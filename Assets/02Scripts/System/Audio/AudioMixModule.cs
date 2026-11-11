using UnityEngine;
using UnityEngine.Audio;

namespace alpha.system.audio
{
    public enum EAudioBus { Master, BGM, SFX, Player, Enemy, World, Voice, UI }

    /// <summary>공용 믹서의 출력 그룹과 저장 가능한 버스별 볼륨을 제공한다.</summary>
    [DisallowMultipleComponent]
    public sealed class AudioMixModule : MonoBehaviour
    {
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private AudioMixerGroup _bgmGroup;
        [SerializeField] private AudioMixerGroup _sfxGroup;
        [SerializeField] private AudioMixerGroup _playerGroup;
        [SerializeField] private AudioMixerGroup _enemyGroup;
        [SerializeField] private AudioMixerGroup _worldGroup;
        [SerializeField] private AudioMixerGroup _voiceGroup;
        [SerializeField] private AudioMixerGroup _uiGroup;

        private bool _ready;
        public static AudioMixModule Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (_mixer == null)
            {
                Debug.LogError($"{nameof(AudioMixModule)}: AudioMixer를 연결하세요.", this);
                return;
            }
            // AudioMixer.SetFloat는 Awake/OnEnable 이후에 적용한다.
            _ready = true;
            foreach (EAudioBus bus in System.Enum.GetValues(typeof(EAudioBus)))
                ApplyVolume(bus, PlayerPrefs.GetFloat(Key(bus), 1f));
        }

        public AudioMixerGroup GetGroup(EAudioBus bus) => bus switch
        {
            EAudioBus.BGM => _bgmGroup,
            EAudioBus.SFX => _sfxGroup,
            EAudioBus.Player => _playerGroup,
            EAudioBus.Enemy => _enemyGroup,
            EAudioBus.World => _worldGroup,
            EAudioBus.Voice => _voiceGroup,
            EAudioBus.UI => _uiGroup,
            _ => null
        };

        public float GetVolume(EAudioBus bus) =>
            Mathf.Clamp01(PlayerPrefs.GetFloat(Key(bus), 1f));

        // UI 슬라이더는 0~1 값을 전달하고 믹서에는 dB로 변환해 적용한다.
        public void SetVolume(EAudioBus bus, float linearVolume)
        {
            float volume = Mathf.Clamp01(linearVolume);
            PlayerPrefs.SetFloat(Key(bus), volume);
            if (_ready) ApplyVolume(bus, volume);
        }

        private bool ApplyVolume(EAudioBus bus, float linearVolume)
        {
            if (_mixer == null) return false;
            float decibels = linearVolume <= 0f
                ? -80f : 20f * Mathf.Log10(Mathf.Max(0.0001f, linearVolume));
            if (_mixer.SetFloat(bus.ToString(), decibels)) return true;
            Debug.LogWarning($"{nameof(AudioMixModule)}: {bus} 볼륨 파라미터가 없습니다.", this);
            return false;
        }

        private static string Key(EAudioBus bus) => $"Audio.Volume.{bus}";

        private void OnApplicationPause(bool paused)
        {
            if (paused) PlayerPrefs.Save();
        }

        private void OnApplicationQuit() => PlayerPrefs.Save();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
