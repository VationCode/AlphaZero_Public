using System;
using System.Collections;
using System.Collections.Generic;
using alpha.system;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>한 공격 패턴의 이펙트와 소리를 애니메이션 이벤트에 맞춰 표현한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BossAttackEffectView : MonoBehaviour
    {
        public enum EImpactCue { None, GroundImpact, Explosion, ProjectileExplosion }

        private enum EffectMode
        {
            Toggle,
            Spawn
        }

        private enum SpawnPositionMode
        {
            Targeting,
            Current,
            Specified,
            RandomTargetArea
        }

        [Serializable]
        private sealed class EffectBinding
        {
            [SerializeField] private string _eventKey;
            [SerializeField] private GameObject _effectObject;
            [SerializeField] private EffectMode _mode;
            [SerializeField] private SpawnPositionMode _spawnPositionMode;
            [SerializeField] private List<Transform> _spawnPoints = new();
            [SerializeField, Min(0.01f)] private float _spawnLifetime = 2f;
            [SerializeField] private bool _preserveInactiveChildren;
            [SerializeField] private bool _randomYaw;
            // 이 이펙트가 발생시킬 카메라 충격 종류다.
            [SerializeField] private EImpactCue _impactCue;
            // 이펙트 시작 시점을 기준으로 소리의 재생 구간을 정한다.
            [SerializeField] private AudioClip _soundClip;
            [SerializeField] private AudioSource _soundSource;
            [SerializeField, Range(0f, 1f)] private float _soundVolume = 1f;
            [SerializeField, Min(0f)] private float _soundStartDelay;
            [SerializeField, Min(0f)] private float _soundClipOffset;
            [SerializeField, Min(0f)] private float _soundDuration;
            [SerializeField, Min(0f)] private float _soundFadeOut;

            public string EventKey => _eventKey;
            public GameObject EffectObject => _effectObject;
            public bool IsSpawn => _mode == EffectMode.Spawn;
            public SpawnPositionMode PositionMode => _spawnPositionMode;
            public IReadOnlyList<Transform> SpawnPoints => _spawnPoints;
            public float SpawnLifetime => _spawnLifetime;
            public bool PreserveInactiveChildren => _preserveInactiveChildren;
            public bool RandomYaw => _randomYaw;
            public EImpactCue ImpactCue => _impactCue;
            public AudioClip SoundClip => _soundClip;
            public AudioSource SoundSource => _soundSource;
            public float SoundVolume => _soundVolume;
            public float SoundStartDelay => _soundStartDelay;
            public float SoundClipOffset => _soundClipOffset;
            public float SoundDuration => _soundDuration;
            public float SoundFadeOut => _soundFadeOut;
        }

        private sealed class ActiveSound
        {
            public readonly AudioClip OriginalClip;
            public readonly float OriginalVolume;

            public ActiveSound(AudioSource source)
            {
                OriginalClip = source.clip;
                OriginalVolume = source.volume;
            }
        }

        [SerializeField] private Transform _spawnOrigin;
        [SerializeField] private List<EffectBinding> _bindings = new();

        private readonly Dictionary<string, EffectBinding> _effects = new();
        private readonly Dictionary<AudioSource, ActiveSound> _activeSounds = new();
        private int _soundGeneration;
        // 패턴 이펙트의 충격 종류와 월드 발생 위치를 외부에 알린다.
        public event Action<EImpactCue, Vector3> ImpactPlayed;
        private void Awake()
        {
            foreach (EffectBinding binding in _bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.EventKey) ||
                    binding.EffectObject == null)
                {
                    Debug.LogError($"{name}: 이펙트의 EventKey 또는 객체가 비어 있습니다.", this);
                    continue;
                }

                string key = binding.EventKey.Trim();
                if (_effects.ContainsKey(key))
                {
                    Debug.LogError($"{name}: 중복 이펙트 키 {key}", this);
                    continue;
                }

                _effects.Add(key, binding);
                if (!binding.IsSpawn)
                    DisableEffect(binding.EffectObject);
            }
        }

        public void SetEffect(string eventKey, bool active)
        {
            if (string.IsNullOrEmpty(eventKey) ||
                !_effects.TryGetValue(eventKey, out EffectBinding binding)) return;

            if (binding.IsSpawn || binding.EffectObject == null) return;

            GameObject effect = binding.EffectObject;
            if (!active)
            {
                BossAttackInstance attackObject =
                    effect.GetComponent<BossAttackInstance>();
                if (attackObject != null) { attackObject.End(); return; }
                DisableEffect(effect);
                return;
            }

            // 반복 공격에서 이전 트레일과 파티클 상태를 이어받지 않도록 초기화한다.
            DisableEffect(effect);
            BossAttackInstance attack = effect.GetComponent<BossAttackInstance>();
            if (attack != null) attack.Begin();
            else effect.SetActive(true);
            PlaySound(binding);
            if (binding.ImpactCue != EImpactCue.None)
                ImpactPlayed?.Invoke(binding.ImpactCue, effect.transform.position);
        }

        public void SpawnEffect(string eventKey,
            bool hasTargetPosition, Vector3 targetPosition)
        {
            if (!_effects.TryGetValue(eventKey, out EffectBinding binding) ||
                !binding.IsSpawn || binding.EffectObject == null ||
                binding.PositionMode == SpawnPositionMode.RandomTargetArea) return;

            if (binding.PositionMode == SpawnPositionMode.Specified)
            {
                if (binding.SpawnPoints == null || binding.SpawnPoints.Count == 0)
                {
                    Debug.LogError($"{name}: {binding.EventKey}의 생성 위치가 비어 있습니다.", this);
                    return;
                }
                bool spawned = false;
                foreach (Transform point in binding.SpawnPoints)
                {
                    if (point == null)
                    {
                        Debug.LogError($"{name}: {binding.EventKey}의 생성 위치에 빈 항목이 있습니다.", this);
                        continue;
                    }
                    spawned |= SpawnAt(binding, point.position, point.rotation,
                        hasTargetPosition, targetPosition);
                }
                if (spawned) PlaySound(binding);
                return;
            }

            if (TryGetSpawnPosition(binding, hasTargetPosition, targetPosition, out Vector3 position))
            {
                if (SpawnAt(binding, position, binding.EffectObject.transform.rotation,
                    hasTargetPosition, targetPosition)) PlaySound(binding);
            }
        }

        public bool IsRandomTargetArea(string eventKey) =>
            _effects.TryGetValue(eventKey, out EffectBinding binding) &&
            binding.IsSpawn && binding.PositionMode == SpawnPositionMode.RandomTargetArea;

        public bool TryGetSpawnSpacing(string eventKey, out float spacing)
        {
            spacing = 0f;
            if (!_effects.TryGetValue(eventKey, out EffectBinding binding) ||
                !binding.IsSpawn || binding.PositionMode != SpawnPositionMode.RandomTargetArea ||
                binding.EffectObject == null) return false;

            GameObject prefab = binding.EffectObject;
            float footprint = 0f;
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled) continue;
                Vector3 scale = collider.transform.lossyScale;
                float radius;
                Vector3 localCenter;
                switch (collider)
                {
                    case SphereCollider sphere:
                        radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                        localCenter = sphere.center;
                        break;
                    case CapsuleCollider capsule when capsule.direction == 1:
                        radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                        localCenter = capsule.center;
                        break;
                    default:
                        continue;
                }
                Vector3 offset = collider.transform.TransformPoint(localCenter) - prefab.transform.position;
                offset.y = 0f;
                footprint = Mathf.Max(footprint, offset.magnitude + radius);
            }
            if (footprint <= 0f)
            {
                Debug.LogError($"{name}: {eventKey} 프리팹에서 소환 간격을 계산할 콜라이더가 없습니다.", this);
                return false;
            }

            spacing = footprint * 2f + 0.1f; // Trigger가 생성 직후 서로 닿지 않도록 여유를 둔다.
            return true;
        }

        public void SpawnEffectAtPositions(string eventKey,
            IReadOnlyList<Vector3> positions)
        {
            if (!_effects.TryGetValue(eventKey, out EffectBinding binding) ||
                !binding.IsSpawn || binding.PositionMode != SpawnPositionMode.RandomTargetArea ||
                binding.EffectObject == null || positions == null) return;

            Quaternion rotation = binding.EffectObject.transform.rotation;
            bool spawned = false;
            foreach (Vector3 position in positions)
                spawned |= SpawnAt(binding, position, rotation, false, default);
            if (spawned) PlaySound(binding);
        }

        private bool SpawnAt(EffectBinding binding, Vector3 position, Quaternion rotation,
            bool hasTargetPosition, Vector3 targetPosition)
        {
            if (binding.RandomYaw)
                rotation = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.up) * rotation;
            // 자식 Rigidbody가 생성될 때부터 같은 방향을 갖도록 회전값을 먼저 정한다.
            GameObject instance = Instantiate(binding.EffectObject, position, rotation);
            BossAttackAreaModule area = instance.GetComponent<BossAttackAreaModule>();
            if (area != null) area.Bind(instance.transform);
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            if (!binding.PreserveInactiveChildren)
                foreach (ParticleSystem particle in particles)
                    for (Transform node = particle.transform;
                         node != null && node != instance.transform; node = node.parent)
                        node.gameObject.SetActive(true);
            BossAttackInstance spawnedAttack = instance.GetComponent<BossAttackInstance>();
            if (spawnedAttack != null) spawnedAttack.Begin();
            else instance.SetActive(true);
            Projectile projectile = instance.GetComponent<Projectile>();
            if (projectile != null)
            {
                if (!hasTargetPosition)
                {
                    Debug.LogError($"{name}: {binding.EventKey} 투사체의 타깃 위치가 없습니다.", this);
                    Destroy(instance);
                    return false;
                }
                if (!projectile.Launch(targetPosition,
                        EProjectileTrackingType.FixedPosition))
                {
                    Debug.LogError($"{name}: {binding.EventKey} 투사체를 발사하지 못했습니다.", this);
                    Destroy(instance);
                    return false;
                }
                if (binding.ImpactCue == EImpactCue.ProjectileExplosion)
                    projectile.Impacted += point => ImpactPlayed?.Invoke(EImpactCue.Explosion, point);
            }
            else if (binding.ImpactCue != EImpactCue.None)
                ImpactPlayed?.Invoke(binding.ImpactCue, position);
            foreach (ParticleSystem particle in particles)
            {
                if (!particle.gameObject.activeInHierarchy) continue;
                particle.Clear();
                particle.Play();
            }

            // 공격·이동 객체는 자신의 수명으로 종료하고 일반 이펙트만 바인딩 수명을 사용한다.
            if (projectile == null && instance.GetComponent<ObjectMove>() == null &&
                spawnedAttack == null)
                Destroy(instance, Mathf.Max(0.01f, binding.SpawnLifetime));
            return true;
        }

        private bool TryGetSpawnPosition(EffectBinding binding, bool hasTargetPosition,
            Vector3 targetPosition, out Vector3 position)
        {
            position = default;
            switch (binding.PositionMode)
            {
                case SpawnPositionMode.Targeting:
                    if (!hasTargetPosition) return false;
                    position = targetPosition;
                    return true;
                case SpawnPositionMode.Current:
                    if (_spawnOrigin == null)
                    {
                        Debug.LogError($"{name}: {binding.EventKey}의 Spawn Origin이 없습니다.", this);
                        return false;
                    }
                    position = _spawnOrigin.position;
                    return true;
                default:
                    return false;
            }
        }

        public void StopAll()
        {
            // 공격이 끝난 뒤 시작할 예약음만 취소하고 재생 중인 충격음의 여운은 남긴다.
            _soundGeneration++;
            foreach (EffectBinding binding in _effects.Values)
                if (!binding.IsSpawn && binding.EffectObject != null)
                {
                    BossAttackInstance attackObject =
                        binding.EffectObject.GetComponent<BossAttackInstance>();
                    if (attackObject != null) attackObject.End();
                    else DisableEffect(binding.EffectObject);
                }
        }

        private void PlaySound(EffectBinding binding)
        {
            // 소리가 없는 이펙트도 같은 바인딩으로 처리한다.
            if (binding.SoundClip == null) return;
            if (binding.SoundSource == null)
            {
                Debug.LogError($"{name}: {binding.EventKey}의 Sound Source가 비어 있습니다.", this);
                return;
            }
            StartCoroutine(PlaySoundRoutine(binding, _soundGeneration));
        }

        private IEnumerator PlaySoundRoutine(EffectBinding binding, int generation)
        {
            if (binding.SoundStartDelay > 0f)
                yield return new WaitForSecondsRealtime(binding.SoundStartDelay);
            if (generation != _soundGeneration) yield break;

            AudioSource source = binding.SoundSource;
            AudioClip clip = binding.SoundClip;
            if (source == null || !source.isActiveAndEnabled || clip == null) yield break;
            if (binding.SoundClipOffset >= clip.length)
            {
                Debug.LogWarning($"{name}: {binding.EventKey}의 Clip Offset이 클립 길이 이상입니다.", this);
                yield break;
            }

            if (_activeSounds.TryGetValue(source, out ActiveSound previous))
                RestoreSound(source, previous);

            ActiveSound current = new(source);
            _activeSounds[source] = current;
            source.clip = clip;
            source.time = binding.SoundClipOffset;
            float volume = current.OriginalVolume * binding.SoundVolume;
            source.volume = volume;
            source.Play();

            float remaining = (clip.length - binding.SoundClipOffset) /
                Mathf.Max(0.01f, Mathf.Abs(source.pitch));
            float duration = binding.SoundDuration > 0f
                ? Mathf.Min(binding.SoundDuration, remaining) : remaining;
            float fadeOut = Mathf.Min(binding.SoundFadeOut, duration);
            float elapsed = 0f;
            while (elapsed < duration && source != null &&
                   _activeSounds.TryGetValue(source, out ActiveSound active) &&
                   ReferenceEquals(active, current))
            {
                elapsed += Time.unscaledDeltaTime;
                if (fadeOut > 0f && elapsed > duration - fadeOut)
                    source.volume = volume * Mathf.Clamp01((duration - elapsed) / fadeOut);
                yield return null;
            }

            if (source == null || !_activeSounds.TryGetValue(source, out ActiveSound last) ||
                !ReferenceEquals(last, current)) yield break;
            RestoreSound(source, current);
            _activeSounds.Remove(source);
        }

        private static void RestoreSound(AudioSource source, ActiveSound sound)
        {
            source.Stop();
            source.clip = sound.OriginalClip;
            source.volume = sound.OriginalVolume;
        }

        private static void DisableEffect(GameObject effect)
        {
            effect.SetActive(false);
            foreach (TrailRenderer trail in effect.GetComponentsInChildren<TrailRenderer>(true))
                trail.Clear();
        }

        private void OnDisable()
        {
            StopAll();
            foreach (KeyValuePair<AudioSource, ActiveSound> playing in _activeSounds)
                if (playing.Key != null)
                    RestoreSound(playing.Key, playing.Value);
            _activeSounds.Clear();
        }
    }
}
