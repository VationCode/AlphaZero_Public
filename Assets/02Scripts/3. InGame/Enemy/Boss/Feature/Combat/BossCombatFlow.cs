using System;
using System.Collections.Generic;
using alpha.ingame.player;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    public enum EBossCombatState
    {
        Idle,
        Attacking,
        AttackRecovery
    }

    /// <summary>전투 행동의 전환과 공격 착지 지점을 판단한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BossCombatFlow : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _attackRecoveryDuration = 0.75f;
        private BossActionStateFlow _actionState;
        private BossIdleState _idle;
        private BossAttackingState _attacking;
        private BossAttackRecoveryState _recovery;
        private BossCombatModule _combat;
        private BossLocomotionModule _locomotion;
        private BossAttackPattern[] _patterns = Array.Empty<BossAttackPattern>();
        private readonly Dictionary<string, BossAttackPattern> _patternsById = new();
        private readonly Dictionary<string, Vector3> _jumpDestinations = new();
        private BossAttackInstance _attack3LandingInstance;
        private BossLocomotionStateFlow _locomotionState;
        private BossAttackPattern _chaseAttack;
        private Vector3 _attack3LandingTarget;
        private BossAttackMovementSettings _activeMovement;
        private Vector3 _initialBossPosition;
        private EAttack7Phase _attack7Phase;
        private readonly Dictionary<string, ChaseFailure> _chaseFailures = new();
        private bool _attackAnimationFinished;
        private int _remainingCasts;

        private enum EAttack7Phase { None, Jump, FaceTarget, Attack }

        private readonly struct ChaseFailure
        {
            public readonly Transform Target;
            public readonly float Distance;
            public readonly float RetryImprovement;

            public ChaseFailure(Transform target, float distance, float retryImprovement)
            {
                Target = target;
                Distance = distance;
                RetryImprovement = retryImprovement;
            }
        }

        public EBossCombatState CurrentState { get; private set; } = EBossCombatState.Idle;
        public Transform CurrentTarget => _idle?.CurrentTarget;
        public IReadOnlyList<BossAttackPattern> AttackPatterns => _patterns;
        public BossAttackPattern CurrentPattern { get; private set; }
        public string CurrentAttackId => CurrentPattern?.AttackId;
        public float CurrentAnimatedJumpHeight =>
            _combat?.CurrentAnimatedJumpHeight ?? 0f;
        // Attack7의 선행 점프에서는 Attack3 클립을 재생해도 공격 ID는 Attack7이다.
        public string CurrentAnimationAttackId => _attack7Phase switch
        {
            EAttack7Phase.Jump => "Attack3",
            EAttack7Phase.FaceTarget => null,
            _ => CurrentAttackId
        };
        public event Action<EBossCombatState> StateChanged;
        public event Action AttackStageChanged;
        public event Action<string, string> EffectSpawnRequested;

        public void Bind(BossActionStateFlow actionState, BossIdleState idle,
            BossAttackingState attacking, BossAttackRecoveryState recovery,
            BossCombatModule combatModule,
            BossLocomotionModule locomotion, BossLocomotionStateFlow locomotionState,
            Vector3 initialBossPosition)
        {
            if (_actionState != null)
                _actionState.StateChanged -= HandleActionStateChanged;
            if (_locomotionState != null)
                _locomotionState.ChaseFinished -= HandleChaseFinished;

            _actionState = actionState;
            _idle = idle;
            _attacking = attacking;
            _recovery = recovery;
            _combat = combatModule;
            _locomotion = locomotion;
            RegisterPatterns();
            _locomotionState = locomotionState;
            _initialBossPosition = initialBossPosition;
            _idle?.Bind(this, combatModule);
            if (_locomotionState != null)
                _locomotionState.ChaseFinished += HandleChaseFinished;
            if (_actionState == null) return;

            _actionState.StateChanged += HandleActionStateChanged;
            HandleActionStateChanged(_actionState.CurrentState);
        }

        private void RegisterPatterns()
        {
            _patterns = GetComponentsInChildren<BossAttackPattern>(true);
            _patternsById.Clear();
            foreach (BossAttackPattern pattern in _patterns)
            {
                if (pattern == null || pattern.Conditions == null ||
                    string.IsNullOrWhiteSpace(pattern.AttackId) ||
                    _patternsById.ContainsKey(pattern.AttackId))
                {
                    Debug.LogError($"{name}: BossAttackPattern의 Attack ID가 비었거나 중복되었습니다: " +
                        $"{pattern?.AttackId}", this);
                    continue;
                }
                _patternsById.Add(pattern.AttackId, pattern);
            }
        }

        public bool TryGetPattern(string attackId, out BossAttackPattern pattern)
        {
            pattern = null;
            return !string.IsNullOrEmpty(attackId) &&
                _patternsById.TryGetValue(attackId, out pattern);
        }

        // Attack7의 첫 단계도 재생 중인 Attack3 패턴의 이동 설정을 사용한다.
        public bool TryGetAnimationMovement(string movementId,
            out BossAttackMovementSettings movement)
        {
            movement = null;
            return CurrentState == EBossCombatState.Attacking &&
                TryGetPattern(CurrentAnimationAttackId, out BossAttackPattern pattern) &&
                pattern.TryGetMovement(movementId, out movement);
        }

        private bool IsConfiguredPattern(BossAttackPattern pattern) =>
            pattern != null && TryGetPattern(pattern.AttackId,
                out BossAttackPattern configured) && ReferenceEquals(pattern, configured);

        private void Update()
        {
            if (_actionState == null ||
                _actionState.CurrentState != EBossActionState.Combat) return;

            if (CurrentState == EBossCombatState.Attacking)
            {
                if (_attackAnimationFinished) FinishAttack();
            }

            if (CurrentState == EBossCombatState.AttackRecovery)
            {
                if (_recovery?.Tick() == true)
                    SetState(EBossCombatState.Idle);
                return;
            }

            if (CurrentState == EBossCombatState.Idle && _chaseAttack == null &&
                _locomotionState?.CurrentState == EBossLocomotionState.Idle && _idle != null)
                _idle.Tick();
        }

        private void FixedUpdate()
        {
            if (_actionState == null ||
                _actionState.CurrentState != EBossActionState.Combat) return;

            if (CurrentState == EBossCombatState.Idle && _chaseAttack != null)
            {
                Transform target = _locomotionState?.Target;
                float distance = _locomotionState?.CurrentDistance ?? float.PositiveInfinity;
                if (!_chaseAttack.IsEnabled || target == null ||
                    distance < _chaseAttack.Conditions.MinSelectionDistance ||
                    distance > _chaseAttack.Conditions.MaxSelectionDistance)
                    CancelPendingChase();
            }
            else if (CurrentState == EBossCombatState.Attacking &&
                _attack7Phase == EAttack7Phase.FaceTarget)
            {
                if (_combat?.HasTargetingTarget != true || _locomotion == null)
                    SetState(EBossCombatState.Idle);
                else if (_locomotion.FaceTarget(_combat.TargetingTarget.position))
                {
                    _attack7Phase = EAttack7Phase.Attack;
                    AttackStageChanged?.Invoke();
                }
            }
        }

        public bool TryBeginChasing(Transform target, BossAttackPattern attack)
        {
            if (_actionState == null ||
                _actionState.CurrentState != EBossActionState.Combat ||
                CurrentState != EBossCombatState.Idle ||
                attack == null || !attack.IsEnabled ||
                !IsConfiguredPattern(attack) ||
                _chaseAttack != null || _locomotionState == null ||
                !_locomotionState.TryBeginChase(target,
                    attack.Conditions.CastDistance)) return false;

            _chaseAttack = attack;
            return true;
        }

        public bool TryBeginAttack(Transform target, BossAttackPattern attack)
        {
            if (_actionState == null || _actionState.CurrentState != EBossActionState.Combat ||
                CurrentState != EBossCombatState.Idle ||
                attack == null || !attack.IsEnabled || !IsSupportedAttackId(attack.AttackId) ||
                _attacking == null || _combat == null || _locomotion == null ||
                !IsConfiguredPattern(attack) ||
                _locomotionState == null ||
                _locomotionState.CurrentState != EBossLocomotionState.Idle ||
                !_combat.CanMove || _combat.IsAttackMovementActive ||
                target == null || !_locomotion.IsFacingTarget(target.position) ||
                !_attacking.CanStart(attack, _combat.Owner, target)) return false;

            if (!TryPrepareAttackMovement(attack, target)) return false;

            if (!_attacking.Enter(attack, _combat.Owner, target)) return false;
            // 대상 위치의 고정은 TargetingStart 이벤트에서 Module에 요청한다.
            _combat.BeginTargeting(target);
            _remainingCasts = attack.Conditions.CastCount;
            _attackAnimationFinished = false;
            EndHitWindow();
            // Attack7은 같은 점프 클립을 재사용한 뒤 타깃을 바라본다.
            _attack7Phase = attack.AttackId == "Attack7" ? EAttack7Phase.Jump : EAttack7Phase.None;
            CurrentPattern = attack;
            _idle?.RecordAttackStarted(attack.AttackId);
            SetState(EBossCombatState.Attacking);
            return true;
        }

        private bool TryPrepareAttackMovement(BossAttackPattern pattern, Transform target)
        {
            _jumpDestinations.Clear();
            if (pattern.AttackId == "Attack5")
            {
                return pattern.TryGetMovement("Butt", out BossAttackMovementSettings dash) &&
                    dash.Type == EBossAttackMovementType.Dash;
            }

            if (pattern.AttackId is not ("Attack3" or "Attack7")) return true;
            // Attack7의 복귀 점프는 Attack3의 이동 설정을 그대로 사용한다.
            BossAttackPattern jumpPattern = pattern;
            if (pattern.AttackId == "Attack7" &&
                !TryGetPattern("Attack3", out jumpPattern)) return false;
            if (!jumpPattern.TryGetMovement("Jump", out BossAttackMovementSettings jump) ||
                jump.Type != EBossAttackMovementType.Jump) return false;

            // Attack7은 Attack3 점프 클립을 재사용해도 최초 보스 위치로 돌아간다.
            Vector3 destination;
            if (pattern.AttackId == "Attack3")
            {
                if (!_combat.TryProjectToGround(target.position, out destination)) return false;
            }
            else destination = _initialBossPosition;

            _attack3LandingTarget = destination;
            _jumpDestinations.Add(jump.MovementId, destination);
            return true;
        }

        public void CaptureTargetingPosition()
        {
            if (CurrentState == EBossCombatState.Attacking)
                _combat?.CaptureTargetingPosition();
        }

        // 클립 이벤트 한 번을 이펙트 생성 요청 한 번으로 전달한다.
        public void BeginEffectSpawn(string eventKey)
        {
            string attackId = CurrentAnimationAttackId;
            if (CurrentState != EBossCombatState.Attacking ||
                _attackAnimationFinished || string.IsNullOrEmpty(attackId) ||
                string.IsNullOrEmpty(eventKey)) return;

            EffectSpawnRequested?.Invoke(attackId, eventKey);
        }

        public void BeginHitWindow()
        {
            if (CurrentState != EBossCombatState.Attacking ||
                CurrentAnimationAttackId != "Attack3" ||
                !TryGetPattern("Attack3", out BossAttackPattern landingPattern)) return;

            // Attack7의 선행 점프도 Attack3 착지 판정을 복귀 지점에서 실행한다.
            EndHitWindow();
            _attack3LandingInstance = _combat?.SpawnAttack(
                landingPattern.AttackInstance, _attack3LandingTarget);
        }

        public void EndHitWindow()
        {
            if (_attack3LandingInstance != null)
                _attack3LandingInstance.End();
            _attack3LandingInstance = null;
        }

        public bool TryGetTargetingPosition(out Vector3 position)
        {
            position = default;
            return CurrentState == EBossCombatState.Attacking &&
                _combat != null && _combat.TryGetTargetingPosition(out position);
        }

        public bool TryGetAttack7SpawnPositions(float minSpacing, List<Vector3> positions)
        {
            return CurrentState == EBossCombatState.Attacking &&
                CurrentAttackId == "Attack7" &&
                _combat?.TryGetAttack7SpawnPositions(CurrentPattern.SpawnSettings,
                    minSpacing, positions) == true;
        }

        private static bool IsSupportedAttackId(string attackId) =>
            attackId is "Attack1" or "Attack2" or "Attack3" or "Attack4" or
                "Attack5" or "Attack6" or "Attack7" or "Attack8";

        public bool IsAttackReady(BossAttackPattern attack) =>
            attack != null && attack.IsEnabled &&
            IsConfiguredPattern(attack) && IsSupportedAttackId(attack.AttackId) &&
            _attacking != null && _attacking.IsReady(attack);

        public bool IsDisruptiveAttack(string attackId)
        {
            if (!TryGetPattern(attackId, out BossAttackPattern pattern)) return false;
            // Attack7은 Attack3의 넉백 착지를 포함하는 복합 패턴이다.
            if (attackId == "Attack7" &&
                TryGetPattern("Attack3", out BossAttackPattern landingPattern) &&
                landingPattern.AttackInstance != null &&
                landingPattern.AttackInstance.HitType is
                    EPlayerHitType.Knockback or EPlayerHitType.Knockdown)
                return true;
            BossAttackInstance instance = pattern.AttackInstance;
            return instance != null &&
                instance.HitType is EPlayerHitType.Knockback or EPlayerHitType.Knockdown;
        }

        // 현재 패턴의 ID로 이동을 찾아 애니메이션 이벤트의 실행 시점에 시작한다.
        public void BeginMovement(string movementId)
        {
            if (CurrentState != EBossCombatState.Attacking ||
                _attackAnimationFinished || _activeMovement != null || _combat == null ||
                !TryGetAnimationMovement(movementId,
                    out BossAttackMovementSettings movement)) return;

            bool started = movement.Type switch
            {
                EBossAttackMovementType.Jump =>
                    _jumpDestinations.TryGetValue(movementId, out Vector3 destination) &&
                    _combat.BeginJump(destination, _combat.TargetingTarget),
                EBossAttackMovementType.Dash => _combat.BeginDash(_combat.TargetingTarget),
                _ => false
            };
            if (started) _activeMovement = movement;
        }

        public void UpdateMovement(string movementId, float progress,
            float animatedHeight, float animatedForwardDistance)
        {
            if (CurrentState != EBossCombatState.Attacking ||
                _activeMovement?.MovementId != movementId) return;
            progress = Mathf.Clamp01(progress);
            if (_activeMovement.Type == EBossAttackMovementType.Jump)
                _combat?.SetJumpPose(progress, animatedHeight,
                    _activeMovement.AdditionalHeight);
            else if (_activeMovement.Type == EBossAttackMovementType.Dash)
                _combat?.SetDashDistance(animatedForwardDistance +
                    _activeMovement.AdditionalDistance * progress);
        }

        public void EndMovement(string movementId)
        {
            if (_activeMovement?.MovementId == movementId)
                CompleteActiveMovement();
        }

        private void CompleteActiveMovement()
        {
            if (_activeMovement == null) return;
            if (_activeMovement.Type == EBossAttackMovementType.Jump)
                _combat?.CompleteJump();
            else if (_activeMovement.Type == EBossAttackMovementType.Dash)
                _combat?.EndDash();
            _activeMovement = null;
        }

        private void CancelActiveMovement()
        {
            if (_activeMovement == null) return;
            _combat?.StopAttackMovement();
            _activeMovement = null;
        }

        // View의 재생 완료 신호를 받아 상태만 전환한다.
        public void CompleteAttackAnimation()
        {
            if (CurrentState != EBossCombatState.Attacking || _attacking == null) return;

            // Attack7은 Attack3 착지 후 현재 플레이어를 바라본 다음 시작한다.
            if (_attack7Phase == EAttack7Phase.Jump)
            {
                EndHitWindow();
                CompleteActiveMovement();
                _attack7Phase = EAttack7Phase.FaceTarget;
                AttackStageChanged?.Invoke();
                return;
            }
            if (_attack7Phase == EAttack7Phase.FaceTarget) return;
            _attackAnimationFinished = true;
            FinishAttack();
        }

        private void FinishAttack()
        {
            if (CurrentState != EBossCombatState.Attacking ||
                !_attackAnimationFinished) return;

            // 한 캐스팅이 끝나면 타깃을 다시 고정하고 같은 공격을 재생한다.
            if (_remainingCasts > 1 && _combat?.HasTargetingTarget == true &&
                TryPrepareAttackMovement(CurrentPattern, _combat.TargetingTarget))
            {
                _remainingCasts--;
                CompleteActiveMovement();
                EndHitWindow();
                _combat.ClearTargetingPosition();
                _attackAnimationFinished = false;
                if (CurrentAttackId == "Attack7")
                    _attack7Phase = EAttack7Phase.Jump;
                AttackStageChanged?.Invoke();
                return;
            }

            if (!_attacking.Complete()) return;

            _remainingCasts = 0;
            CompleteActiveMovement();
            SetState(EBossCombatState.AttackRecovery);
        }

        public void RecordChaseFailure(BossAttackPattern attack, Transform target,
            float distance, float retryImprovement)
        {
            if (attack == null || string.IsNullOrEmpty(attack.AttackId)) return;
            _chaseFailures[attack.AttackId] = new ChaseFailure(target, distance,
                Mathf.Max(0.01f, retryImprovement));
        }

        // Locomotion은 추적 결과만 알리고, 공격 시전 여부는 Combat이 판단한다.
        private void HandleChaseFinished(EBossChaseResult result, Transform target,
            float distance, float progressDistance)
        {
            BossAttackPattern attack = _chaseAttack;
            _chaseAttack = null;
            if (attack == null || !attack.IsEnabled || _actionState == null ||
                _actionState.CurrentState != EBossActionState.Combat ||
                CurrentState != EBossCombatState.Idle || target == null ||
                distance < attack.Conditions.MinSelectionDistance ||
                distance > attack.Conditions.MaxSelectionDistance) return;

            if (result == EBossChaseResult.Reached &&
                TryBeginAttack(target, attack)) return;
            if (result == EBossChaseResult.NoProgress ||
                result == EBossChaseResult.MovementUnavailable)
                RecordChaseFailure(attack, target, distance, progressDistance);
        }

        private void CancelPendingChase()
        {
            _chaseAttack = null;
            _locomotionState?.StopChase();
        }

        public bool IsChaseBlocked(BossAttackPattern attack, Transform target,
            float distance)
        {
            if (attack == null || string.IsNullOrEmpty(attack.AttackId) ||
                !_chaseFailures.TryGetValue(attack.AttackId, out ChaseFailure failure))
                return false;

            // 상황이 바뀌면 실패한 공격을 다시 후보로 허용한다.
            if (failure.Target != target || distance <= attack.Conditions.CastDistance ||
                distance <= failure.Distance - failure.RetryImprovement)
            {
                _chaseFailures.Remove(attack.AttackId);
                return false;
            }
            return true;
        }

        private void HandleActionStateChanged(EBossActionState actionState)
        {
            // 인트로·페이즈 전환·사망 중에는 전투 행동을 진행하지 않는다.
            if (actionState != EBossActionState.Combat)
            {
                CancelPendingChase();
                _chaseFailures.Clear();
                _idle?.ResetSelectionHistory();
                SetState(EBossCombatState.Idle);
            }
        }

        private void SetState(EBossCombatState next)
        {
            if (CurrentState == next) return;
            CancelPendingChase();
            if (CurrentState == EBossCombatState.Attacking)
            {
                _attackAnimationFinished = false;
                _remainingCasts = 0;
                EndHitWindow();
                CancelActiveMovement();
                _jumpDestinations.Clear();
                _combat?.EndTargeting();
                _attack7Phase = EAttack7Phase.None;
                _attacking?.Exit();
                CurrentPattern = null;
            }
            if (CurrentState == EBossCombatState.AttackRecovery)
                _recovery?.Exit();
            CurrentState = next;
            if (next == EBossCombatState.AttackRecovery)
                _recovery?.Enter(_attackRecoveryDuration);
            StateChanged?.Invoke(next);
        }

        private void OnDestroy()
        {
            EndHitWindow();
            if (_actionState != null)
                _actionState.StateChanged -= HandleActionStateChanged;
            if (_locomotionState != null)
                _locomotionState.ChaseFinished -= HandleChaseFinished;
            _idle?.Bind(null, null);
            CancelPendingChase();
            _attacking?.Exit();
            CancelActiveMovement();
            _jumpDestinations.Clear();
            _combat?.EndTargeting();
            _recovery?.Exit();
        }

        private void OnDisable()
        {
            _attackAnimationFinished = false;
            _remainingCasts = 0;
            EndHitWindow();
            _combat?.EndTargeting();
            CancelPendingChase();
            if (CurrentState == EBossCombatState.Attacking)
            {
                CancelActiveMovement();
                _jumpDestinations.Clear();
                _attack7Phase = EAttack7Phase.None;
                _attacking?.Exit();
                CurrentPattern = null;
                CurrentState = EBossCombatState.Idle;
            }
            else if (CurrentState == EBossCombatState.AttackRecovery)
            {
                _recovery?.Exit();
                CurrentState = EBossCombatState.Idle;
            }
        }

        private void OnValidate()
        {
            _attackRecoveryDuration = Mathf.Max(0f, _attackRecoveryDuration);
        }
    }
}
