using alpha.ingame.enemy.boss;
using alpha.ingame.player;
using UnityEngine;

namespace alpha.ingame.camera
{
    /// <summary>플레이어와 보스의 전투 결과를 카메라 표현에 연결한다.</summary>
    public sealed class CameraRoot : MonoBehaviour
    {
        // 화면 흔들림을 실행하는 카메라 자식 컴포넌트다.
        [SerializeField] private CameraShakeModule _shake;
        // 피격과 사격 결과를 제공하는 플레이어 진입점이다.
        [SerializeField] private PlayerRoot _player;
        // 패턴별 충격 이벤트를 제공하는 보스 진입점이다.
        [SerializeField] private BossRoot _boss;

        // 이벤트 구독을 해제할 때 사용할 패턴 View 목록이다.
        private BossAttackEffectView[] _bossEffects;
        // 최초 Start 이후 재활성화 시 이벤트를 다시 연결할지 나타낸다.
        private bool _initialized;
        // 연속 치명타의 화면 흔들림을 제한한다.
        private float _nextCriticalShakeAt;

        private void Start()
        {
            _initialized = true;
            Bind();
        }

        private void OnEnable()
        {
            if (_initialized) Bind();
        }

        private void Bind()
        {
            if (_shake == null) return;
            if (_player != null)
            {
                if (_player.ActionStateFlow != null)
                    _player.ActionStateFlow.HitReceived += OnPlayerHit;
                if (_player.CombatModule != null)
                {
                    _player.CombatModule.CameraRecoilRequested += OnCameraRecoilRequested;
                    _player.CombatModule.CriticalHitApplied += OnCriticalHitApplied;
                }
            }
            if (_boss == null) return;
            _bossEffects = _boss.GetComponentsInChildren<BossAttackEffectView>(true);
            foreach (BossAttackEffectView effect in _bossEffects)
                effect.ImpactPlayed += OnBossImpact;
        }

        private void OnPlayerHit(PlayerHitInfo hit)
        {
            ECameraShakeType type = hit.Type switch
            {
                EPlayerHitType.Normal => ECameraShakeType.LightHit,
                EPlayerHitType.Stagger => ECameraShakeType.HeavyHit,
                EPlayerHitType.Knockback or EPlayerHitType.Knockdown => ECameraShakeType.StrongHit,
                _ => ECameraShakeType.HeavyHit
            };
            _shake.Play(type);
        }

        private void OnCameraRecoilRequested(Vector3 direction, float kickDuration, float recoveryDuration) =>
            _shake.PlayRecoil(direction, kickDuration, recoveryDuration);

        private void OnCriticalHitApplied()
        {
            if (Time.time < _nextCriticalShakeAt) return;
            _nextCriticalShakeAt = Time.time + 0.12f;
            _shake.Play(ECameraShakeType.CriticalHit);
        }

        private void OnBossImpact(BossAttackEffectView.EImpactCue cue, Vector3 position)
        {
            if (cue == BossAttackEffectView.EImpactCue.GroundImpact)
                _shake.PlayAt(ECameraShakeType.GroundImpact, position);
            else if (cue == BossAttackEffectView.EImpactCue.Explosion)
                _shake.PlayAt(ECameraShakeType.Explosion, position);
        }

        private void OnDisable()
        {
            if (_player != null)
            {
                if (_player.ActionStateFlow != null)
                    _player.ActionStateFlow.HitReceived -= OnPlayerHit;
                if (_player.CombatModule != null)
                {
                    _player.CombatModule.CameraRecoilRequested -= OnCameraRecoilRequested;
                    _player.CombatModule.CriticalHitApplied -= OnCriticalHitApplied;
                }
            }
            if (_bossEffects == null) return;
            foreach (BossAttackEffectView effect in _bossEffects)
                if (effect != null) effect.ImpactPlayed -= OnBossImpact;
            _bossEffects = null;
        }
    }
}
