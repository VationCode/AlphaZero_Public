using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace alpha.ingame.camera
{
    public enum ECameraShakeType
    {
        LightHit = 0,    // 일반 피격
        HeavyHit = 1,    // 경직 피격
        Recoil = 2,      // 사격 반동
        Explosion = 3,   // 폭발
        GroundImpact = 4, // 지면 충격
        StrongHit = 5,   // 넉백·넉다운 피격
        CriticalHit = 6 // 플레이어 공격 치명타
    }

    [Serializable]
    public struct CameraShakeSettings
    {
        // 이 설정을 적용할 상황이다.
        [Tooltip("흔들림을 재생할 상황. LightHit=일반 피격, HeavyHit=경직, StrongHit=넉백·넉다운, CriticalHit=공격 치명타")]
        public ECameraShakeType Type;
        // 흔들림이 완전히 사라질 때까지의 시간(초)이다.
        [Tooltip("흔들림이 시작해서 완전히 사라질 때까지의 시간(초)")]
        [Min(0.01f)] public float Duration;
        // 기본 쉐이크의 축별 위치 진폭, StrongHit의 상하 진폭(유닛)이다.
        [Tooltip("일반 타입은 XYZ 위치 흔들림 진폭, StrongHit은 상하(Y) 진폭(유닛)")]
        [Min(0f)] public float PositionAmplitude;
        // StrongHit에서만 사용하는 좌우 위치 진폭(유닛)이다.
        [Tooltip("StrongHit 전용 좌우(X) 위치 흔들림 진폭(유닛)")]
        [Min(0f)] public float HorizontalAmplitude;
        // StrongHit 시작 순간 위로 튀는 위치 충격(유닛)이다.
        [Tooltip("StrongHit 전용. 피격 직후 위로 튀었다가 약 0.12초 동안 사라지는 위치 충격(유닛)")]
        [Min(0f)] public float ImpactKick;
        // 기본 쉐이크의 축별 회전 진폭, StrongHit의 좌우 회전 진폭(도)이다.
        [Tooltip("일반 타입은 XYZ 회전 흔들림 진폭, StrongHit은 좌우(Yaw) 회전 진폭(도)")]
        [Min(0f)] public float RotationAmplitude;
        // Recoil에서 발사 직후 위로 튀는 회전 각도(도)다.
        [Tooltip("Recoil에서만 사용하는 위쪽 회전 반동 각도(도)")]
        [Min(0f)] public float KickPitch;
        // StrongHit에서 왕복하는 화면 기울기(도)다.
        [Tooltip("StrongHit 전용. 화면이 좌우로 기울어지는 Roll 회전 진폭(도)")]
        [FormerlySerializedAs("KickRoll"), Min(0f)] public float RollAmplitude;
        // 위치·회전 잡음 좌표가 초당 이동하는 속도다.
        [Tooltip("흔들림 패턴의 변화 속도. 값이 클수록 빠르게 떨림")]
        [Min(0.01f)] public float Frequency;
        // 월드 충격이 100% 강도로 전달되는 최대 거리다.
        [Tooltip("PlayAt으로 재생하는 폭발·지면 충격 전용. 이 거리 안에서는 최대 강도(유닛)")]
        [Min(0f)] public float FullStrengthDistance;
        // 월드 충격이 더 이상 전달되지 않는 거리다.
        [Tooltip("PlayAt으로 재생하는 폭발·지면 충격 전용. 이 거리 밖에서는 흔들림 없음(유닛)")]
        [Min(0f)] public float MaxDistance;
    }

    /// <summary>카메라의 화면 표현만 흔들고 조준 기준인 부모 Transform은 유지한다.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class CameraShakeModule : MonoBehaviour
    {
        // 상황별 지속 시간·진폭·거리 감쇠 설정이다.
        [Tooltip("상황별 흔들림 설정. Type별로 하나씩 등록")]
        [SerializeField] private CameraShakeSettings[] _settings = Array.Empty<CameraShakeSettings>();
        // 일반 흔들림을 합친 위치 이동의 상한이다.
        [Tooltip("일반 흔들림의 최종 위치 상한(유닛). StrongHit과 무기 반동은 별도 상한 사용")]
        [SerializeField, Min(0f)] private float _maxPositionOffset = 0.25f;
        // 일반 흔들림을 합친 회전 각도의 상한이다.
        [Tooltip("일반 흔들림의 최종 회전 상한(도). StrongHit과 무기 반동은 별도 상한 사용")]
        [SerializeField, Min(0f)] private float _maxRotationOffset = 3f;

        // 현재 재생 중인 흔들림 목록이다.
        private readonly List<ActiveShake> _active = new();
        // 무기별 상승·복구 시간을 가진 화면 반동 목록이다.
        private readonly List<ActiveWeaponRecoil> _activeRecoil = new();
        // 흔들림 종료 후 돌아갈 로컬 위치다.
        private Vector3 _restPosition;
        // 흔들림 종료 후 돌아갈 로컬 회전이다.
        private Quaternion _restRotation;

        private struct ActiveShake
        {
            // 시작 시 복사한 타입별 설정이다.
            public CameraShakeSettings Settings;
            // 흔들림을 시작한 게임 시간이다.
            public float StartedAt;
            // 거리 감쇠 등 흔들림 전체에 적용할 강도다.
            public float Strength;
            // 동시에 재생되는 흔들림의 잡음 패턴을 분리하는 값이다.
            public float Seed;
        }

        private struct ActiveWeaponRecoil
        {
            public Vector3 Direction;
            public float StartedAt;
            public float KickDuration;
            public float RecoveryDuration;
        }

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
        }

        public void Play(ECameraShakeType type) => Add(type, 1f);

        // 지정한 강도로 흔들림을 재생한다.
        public void Play(ECameraShakeType type, float strength) => Add(type, strength);

        // 공통 Recoil 흔들림에 무기별 방향·상승·복구 효과를 더한다.
        public void PlayRecoil(Vector3 direction, float kickDuration, float recoveryDuration)
        {
            if (direction.sqrMagnitude <= 0f) return;
            Add(ECameraShakeType.Recoil, 1f);
            _activeRecoil.Add(new ActiveWeaponRecoil
            {
                Direction = direction,
                StartedAt = Time.time,
                KickDuration = Mathf.Max(0.01f, kickDuration),
                RecoveryDuration = Mathf.Max(0.01f, recoveryDuration)
            });
            if (_activeRecoil.Count > 8) _activeRecoil.RemoveAt(0);
        }

        public void PlayAt(ECameraShakeType type, Vector3 origin)
        {
            if (!TryGetSettings(type, out CameraShakeSettings settings)) return;
            // 카메라의 흔들림 이전 위치와 충격 지점 사이의 거리다.
            float distance = Vector3.Distance(transform.parent != null
                ? transform.parent.position : transform.position, origin);
            // 충격 거리에서 계산한 재생 강도다.
            float strength = settings.MaxDistance <= settings.FullStrengthDistance
                ? 1f : Mathf.Clamp01((settings.MaxDistance - distance) /
                    (settings.MaxDistance - settings.FullStrengthDistance));
            Add(type, strength);
        }

        private void Add(ECameraShakeType type, float strength)
        {
            if (strength <= 0f || !TryGetSettings(type, out CameraShakeSettings settings) ||
                settings.Duration <= 0f) return;
            _active.Add(new ActiveShake
            {
                Settings = settings,
                StartedAt = Time.time,
                Strength = strength,
                Seed = UnityEngine.Random.Range(0f, 1000f)
            });
            // 연사나 다중 폭발에서도 누적 비용과 화면 진폭을 제한한다.
            if (_active.Count > 8) _active.RemoveAt(0);
        }

        private bool TryGetSettings(ECameraShakeType type, out CameraShakeSettings settings)
        {
            foreach (CameraShakeSettings candidate in _settings)
                if (candidate.Type == type)
                {
                    settings = candidate;
                    return true;
                }
            settings = default;
            return false;
        }

        private void LateUpdate()
        {
            // 활성 흔들림의 위치 이동을 합산한다.
            Vector3 position = Vector3.zero;
            // 활성 흔들림의 회전 각도를 합산한다.
            Vector3 rotation = Vector3.zero;
            // 강한 피격의 위치 중심 흔들림은 공통 상한과 따로 합산한다.
            Vector3 strongPosition = Vector3.zero;
            Vector3 strongRotation = Vector3.zero;
            float maxStrongPosition = 0f;
            float maxStrongRotation = 0f;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                // 이번에 계산할 흔들림의 시작 상태다.
                ActiveShake shake = _active[i];
                // 해당 흔들림이 시작된 뒤 지난 시간이다.
                float elapsed = Time.time - shake.StartedAt;
                if (elapsed >= shake.Settings.Duration)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                // 종료 시점까지 남은 비율이다.
                float remaining = 1f - elapsed / shake.Settings.Duration;
                // 잡음을 읽을 현재 시간 좌표다.
                float phase = shake.Seed + elapsed * shake.Settings.Frequency;
                // 위치와 회전에 공통으로 적용할 3축 잡음이다.
                Vector3 noise = new(
                    Mathf.PerlinNoise(phase, 0f) * 2f - 1f,
                    Mathf.PerlinNoise(phase, 17f) * 2f - 1f,
                    Mathf.PerlinNoise(phase, 37f) * 2f - 1f);
                if (shake.Settings.Type == ECameraShakeType.StrongHit)
                {
                    // 피격 첫 순간에는 확실한 위치 충격을 주고, 이후 불규칙한 흔들림을 감쇠한다.
                    float envelope = Mathf.SmoothStep(0f, 1f, remaining) * shake.Strength;
                    strongPosition.x += noise.x * shake.Settings.HorizontalAmplitude * envelope;
                    strongPosition.y += noise.y * shake.Settings.PositionAmplitude * envelope;
                    float kickDuration = Mathf.Min(0.12f, shake.Settings.Duration * 0.35f);
                    float kickProgress = Mathf.Clamp01(elapsed / kickDuration);
                    strongPosition.y += shake.Settings.ImpactKick *
                        (1f - Mathf.SmoothStep(0f, 1f, kickProgress)) * shake.Strength;
                    strongRotation.y += noise.z * shake.Settings.RotationAmplitude * envelope;
                    strongRotation.z += Mathf.Sin(phase) * shake.Settings.RollAmplitude * envelope;
                    maxStrongPosition = Mathf.Max(maxStrongPosition,
                        new Vector2(shake.Settings.HorizontalAmplitude,
                            shake.Settings.PositionAmplitude + shake.Settings.ImpactKick).magnitude *
                        shake.Strength);
                    maxStrongRotation = Mathf.Max(maxStrongRotation,
                        new Vector2(shake.Settings.RotationAmplitude,
                            shake.Settings.RollAmplitude).magnitude * shake.Strength);
                    continue;
                }

                // 일반 흔들림은 종료 시점으로 갈수록 빠르게 약해진다.
                float weight = remaining * remaining * shake.Strength;
                position += noise * (shake.Settings.PositionAmplitude * weight);
                rotation += noise * (shake.Settings.RotationAmplitude * weight);
                if (shake.Settings.Type == ECameraShakeType.Recoil)
                {
                    // 공통 Recoil 설정은 기존 상승·복귀 곡선을 유지한다.
                    float progress = Mathf.Clamp01(elapsed / shake.Settings.Duration);
                    float kick = Mathf.Sin(Mathf.PI * Mathf.Sqrt(progress));
                    rotation.x -= shake.Settings.KickPitch * shake.Strength * kick;
                }
            }

            Vector3 recoilPosition = Vector3.zero;
            Vector3 recoilRotation = Vector3.zero;
            float maxRecoilPosition = 0f;
            float maxRecoilRotation = 0f;
            for (int i = _activeRecoil.Count - 1; i >= 0; i--)
            {
                ActiveWeaponRecoil recoil = _activeRecoil[i];
                float elapsed = Time.time - recoil.StartedAt;
                if (elapsed >= recoil.KickDuration + recoil.RecoveryDuration)
                {
                    _activeRecoil.RemoveAt(i);
                    continue;
                }

                // 상승과 복구 시간을 무기별로 지정하고 양 끝에서는 부드럽게 멈춘다.
                float amount = elapsed < recoil.KickDuration
                    ? Mathf.SmoothStep(0f, 1f, elapsed / recoil.KickDuration)
                    : 1f - Mathf.SmoothStep(0f, 1f,
                        (elapsed - recoil.KickDuration) / recoil.RecoveryDuration);
                Vector3 direction = recoil.Direction;
                recoilRotation.x -= direction.y * amount;
                recoilRotation.y += direction.x * amount;
                recoilPosition.z += direction.z * amount;
                maxRecoilRotation = Mathf.Max(maxRecoilRotation,
                    new Vector2(direction.x, direction.y).magnitude);
                maxRecoilPosition = Mathf.Max(maxRecoilPosition, Mathf.Abs(direction.z));
            }

            // 기본 흔들림 상한은 유지하고 강한 피격의 위치 이동은 별도로 적용한다.
            transform.localPosition = _restPosition + Vector3.ClampMagnitude(position, _maxPositionOffset) +
                Vector3.ClampMagnitude(strongPosition, maxStrongPosition) +
                Vector3.ClampMagnitude(recoilPosition, maxRecoilPosition);
            transform.localRotation = _restRotation * Quaternion.Euler(
                Vector3.ClampMagnitude(rotation, _maxRotationOffset) +
                Vector3.ClampMagnitude(strongRotation, maxStrongRotation) +
                Vector3.ClampMagnitude(recoilRotation, maxRecoilRotation));
        }

        private void OnDisable()
        {
            _active.Clear();
            _activeRecoil.Clear();
            transform.localPosition = _restPosition;
            transform.localRotation = _restRotation;
        }
    }
}
