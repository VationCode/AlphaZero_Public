using System;
using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.player
{
    [Serializable]
    public sealed class SkillAudioCue
    {
        [SerializeField] private AudioClip _clip;
        [SerializeField, Min(0f)] private float _delay;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        public AudioClip Clip => _clip;
        public float Delay => Mathf.Max(0f, _delay);
        public float Volume => Mathf.Clamp01(_volume);
    }

    public enum ESkillId
    {
        Shield = 0,
        Turret = 1,
        OrbitalArmament = 2,
        AreaAttack = 3,
        WeaponBuff = 4
    }

    public enum ESkillActivationMode
    {
        Aim,
        Self
    }

    /// <summary>모든 스킬 에셋이 공유하는 변경되지 않는 설정값.</summary>
    public abstract class SkillData : ScriptableObject
    {
        [SerializeField] private ESkillId _id;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea(2, 4)] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField, Min(0f)] private float _cooldown;
        [SerializeField] private ESkillActivationMode _activationMode;

        [Header("Animation Event Keys")]
        [SerializeField] private string _executeEventKey = "Excute";
        [SerializeField] private string _audio1EventKey = "Audio1";
        [SerializeField] private string _audio2EventKey = "Audio2";

        [Header("Animation Event Audio")]
        [SerializeField] private AudioClip _audio1;
        [SerializeField] private AudioClip _audio2;

        [Header("Execution Cues")]
        [SerializeField] private SkillAudioCue[] _activationAudioCues = Array.Empty<SkillAudioCue>();
        [SerializeField] private GameObject _activationEffectPrefab;
        [SerializeField, Min(0.1f)] private float _activationEffectLifetime = 3f;

        public ESkillId Id => _id;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public float Cooldown => Mathf.Max(0f, _cooldown);
        public ESkillActivationMode ActivationMode => _activationMode;
        public string ExecuteEventKey => _executeEventKey;
        public string Audio1EventKey => _audio1EventKey;
        public string Audio2EventKey => _audio2EventKey;
        public AudioClip Audio1 => _audio1;
        public AudioClip Audio2 => _audio2;
        public IReadOnlyList<SkillAudioCue> ActivationAudioCues =>
            _activationAudioCues ?? Array.Empty<SkillAudioCue>();
        public GameObject ActivationEffectPrefab => _activationEffectPrefab;
        public float ActivationEffectLifetime => Mathf.Max(0.1f, _activationEffectLifetime);

        protected virtual void OnValidate()
        {
            _cooldown = Mathf.Max(0f, _cooldown);
            _activationEffectLifetime = Mathf.Max(0.1f, _activationEffectLifetime);
        }
    }
}
