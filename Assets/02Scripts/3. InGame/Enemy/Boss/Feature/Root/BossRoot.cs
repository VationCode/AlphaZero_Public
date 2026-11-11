using alpha.ingame.item;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>보스의 체력과 전투·이동 상태를 연결하는 진입점이다.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class BossRoot : MonoBehaviour, IDamageable
    {
        public EnemyHealth Health { get; private set; }
        public bool IsDead => Health != null && Health.IsDead;
        public BossActionStateFlow ActionStateFlow { get; private set; }
        public BossCombatFlow CombatFlow { get; private set; }
        public BossCombatModule CombatModule { get; private set; }
        public BossLocomotionModule Locomotion { get; private set; }
        public BossLocomotionStateFlow LocomotionStateFlow { get; private set; }
        public BossAnimationView Animation { get; private set; }

        private BossMovementModule _movement;

        private void Awake()
        {
            Health = GetComponentInChildren<EnemyHealth>(true);
            ActionStateFlow = GetComponentInChildren<BossActionStateFlow>(true);
            CombatFlow = GetComponentInChildren<BossCombatFlow>(true);
            CombatModule = GetComponentInChildren<BossCombatModule>(true);
            Locomotion = GetComponentInChildren<BossLocomotionModule>(true);
            LocomotionStateFlow = GetComponentInChildren<BossLocomotionStateFlow>(true);
            Animation = GetComponentInChildren<BossAnimationView>(true);
            BossAttackPattern[] patterns = CombatFlow != null
                ? CombatFlow.GetComponentsInChildren<BossAttackPattern>(true)
                : System.Array.Empty<BossAttackPattern>();
            BossAttackAreaModule attack1Area = FindPattern(patterns, "Attack1")?
                .AttackInstance?.GetComponent<BossAttackAreaModule>();
            BossAttack4AttractionModule attack4Attraction = FindPattern(patterns,
                "Attack4")?.PrimaryAttackObject?.GetComponent<BossAttack4AttractionModule>();
            BossAttackAreaModule attack4Area = attack4Attraction != null
                ? attack4Attraction.GetComponent<BossAttackAreaModule>() : null;
            BossAttack5ContactModule attack5Contact = FindPattern(patterns,
                "Attack5")?.PrimaryAttackObject?.GetComponent<BossAttack5ContactModule>();
            BossAttackAreaModule attack5Area = attack5Contact != null
                ? attack5Contact.GetComponent<BossAttackAreaModule>() : null;
            BossAttackInstance attack5Instance = attack5Area != null
                ? attack5Area.GetComponent<BossAttackInstance>() : null;
            Rigidbody body = GetComponent<Rigidbody>();
            if (Health == null || ActionStateFlow == null || CombatFlow == null || CombatModule == null ||
                attack1Area == null ||
                attack4Area == null ||
                attack4Area.GetComponent<BossAttackInstance>() == null ||
                attack5Area == null || attack5Instance == null ||
                Locomotion == null || LocomotionStateFlow == null ||
                body == null ||
                Animation == null || !Animation.IsReady)
            {
                Debug.LogError($"{name}: Health, 보스 상태, Attack1/4/5 공격 객체, Locomotion 모듈, 애니메이션 또는 Rigidbody가 누락되었습니다.", this);
                enabled = false;
                return;
            }

            Health.Died += HandleDied;
            BossIdleState idle = new();
            BossAttackingState attackingState = new();
            BossAttackRecoveryState recoveryState = new(CombatModule);
            Transform groundBody = transform.Find("GroundBody");
            BoxCollider[] groundBodyColliders = groundBody != null
                ? groundBody.GetComponentsInChildren<BoxCollider>(true)
                : System.Array.Empty<BoxCollider>();
            _movement = new BossMovementModule(body, groundBodyColliders,
                Locomotion.ContactClearanceSpeed);
            Locomotion.Bind(_movement);
            LocomotionStateFlow.Bind(Locomotion);
            CombatModule.Bind(transform, _movement);
            attack1Area.Bind(transform);
            attack4Area.Bind(transform);
            attack4Attraction.Bind(transform, () => CombatFlow.CurrentTarget);
            attack5Area.Bind(transform);
            attack5Contact.Bind(transform, () => CombatFlow.CurrentTarget,
                groundBodyColliders);
            CombatFlow.Bind(ActionStateFlow, idle, attackingState, recoveryState,
                CombatModule, Locomotion, LocomotionStateFlow,
                transform.position);
            Animation.Bind(ActionStateFlow, CombatFlow, LocomotionStateFlow);
            Animation.AttackAnimationFinished += CombatFlow.CompleteAttackAnimation;
            Animation.MovementStarted += CombatFlow.BeginMovement;
            Animation.MovementProgressChanged += CombatFlow.UpdateMovement;
            Animation.MovementEnded += CombatFlow.EndMovement;
        }

        private void FixedUpdate() => _movement?.FixedTick();

        private void LateUpdate() => _movement?.LateTick();

        // 무기 피격은 보스 진입점에서 받고 체력 계산은 Health 자식에 맡긴다.
        public float TakeDamage(in AttackDamage damage) =>
            Health != null ? Health.ApplyDamage(damage) : 0f;

        private void HandleDied()
        {
            ActionStateFlow?.MarkDead();
            LocomotionStateFlow?.StopChase();
            _movement?.OnDisabled();
        }

        private void OnDisable() => _movement?.OnDisabled();

        private static BossAttackPattern FindPattern(BossAttackPattern[] patterns,
            string attackId)
        {
            foreach (BossAttackPattern pattern in patterns)
                if (pattern != null && pattern.AttackId == attackId) return pattern;
            return null;
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Died -= HandleDied;
            _movement?.Dispose();
            if (Animation != null && CombatFlow != null)
            {
                Animation.AttackAnimationFinished -= CombatFlow.CompleteAttackAnimation;
                Animation.MovementStarted -= CombatFlow.BeginMovement;
                Animation.MovementProgressChanged -= CombatFlow.UpdateMovement;
                Animation.MovementEnded -= CombatFlow.EndMovement;
            }
        }

    }
}
