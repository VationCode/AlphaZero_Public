using System;
using UnityEngine;

namespace alpha.ingame.enemy
{
    /// <summary>적의 상태를 애니메이션으로 표현하고 공격 타입별 클립 설정을 제공한다.</summary>
    public sealed class EnemyAnimationView : MonoBehaviour
    {
        [Serializable]
        private sealed class AttackAnimationEntry
        {
            [SerializeField, Tooltip("인스펙터에서 공격 클립을 식별할 에셋입니다.")]
            private AnimationClip _clip;
            [SerializeField, Tooltip("이 클립을 사용할 공격 타입입니다. 같은 타입을 여러 클립에 지정할 수 있습니다.")]
            private EEnemyAttackStyle _attackType;
            [SerializeField, Tooltip("Animator의 전체 상태 경로입니다. 클립 이름과 다를 수 있습니다.")]
            private string _statePath;

            public AnimationClip Clip => _clip;
            public EEnemyAttackStyle AttackType => _attackType;
            public string StatePath => _statePath;
        }

        [Header("Locomotion States")]
        [SerializeField, Tooltip("Animator의 전체 Idle 상태 경로입니다.")]
        private string _idleStatePath = "Base Layer.Idle";
        [SerializeField, Tooltip("Animator의 전체 Idle2 상태 경로입니다.")]
        private string _idle2StatePath = "Base Layer.Idle2";
        [SerializeField, Tooltip("Animator의 전체 Walk 상태 경로입니다.")]
        private string _walkStatePath = "Base Layer.Walk";
        [SerializeField, Tooltip("Animator의 전체 Run 상태 경로입니다.")]
        private string _runStatePath = "Base Layer.Run";

        [Header("Attack Animations")]
        [SerializeField] private AttackAnimationEntry[] _attackAnimations = Array.Empty<AttackAnimationEntry>();

        [Header("Transition")]
        [SerializeField, Min(0f)] private float _walkSpeedThreshold = 0.05f;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.15f;

        private EnemyRoot _enemy;
        private Animator _animator;
        private int _currentState;
        private int _idleStateHash;
        private int _idle2StateHash;
        private int _walkStateHash;
        private int _runStateHash;
        private bool _hasIdle2State;
        private bool _hasRunState;

        private void Awake()
        {
            _enemy = GetComponentInParent<EnemyRoot>(true);
            _animator = GetComponentInChildren<Animator>(true);
            _idleStateHash = Animator.StringToHash(_idleStatePath);
            _idle2StateHash = Animator.StringToHash(_idle2StatePath);
            _walkStateHash = Animator.StringToHash(_walkStatePath);
            _runStateHash = Animator.StringToHash(_runStatePath);
            if (_enemy == null || _animator == null ||
                !_animator.HasState(0, _idleStateHash) || !_animator.HasState(0, _walkStateHash))
            {
                Debug.LogWarning($"{name}: 순찰 애니메이션에 EnemyRoot와 Idle/Walk 상태가 필요합니다.", this);
                enabled = false;
                return;
            }

            _hasIdle2State = _animator.HasState(0, _idle2StateHash);
            if (!_hasIdle2State)
                Debug.LogWarning($"{name}: Animator에 Idle2 상태가 없어 두 번째 대기는 Idle로 표시됩니다.", this);
            _hasRunState = _animator.HasState(0, _runStateHash);
            if (!_hasRunState)
                Debug.LogWarning($"{name}: Animator에 Run 상태가 없어 추적 이동은 Walk로 표시됩니다.", this);

            if (_attackAnimations == null) return;
            for (int i = 0; i < _attackAnimations.Length; i++)
            {
                if (IsValidAttackEntry(_attackAnimations[i])) continue;
                Debug.LogWarning($"{name}: Attack Animations의 {i}번 클립, 공격 타입 또는 Animator 상태 경로를 확인하세요.", this);
            }
        }

        // 공격 Flow가 같은 타입의 여러 클립을 순서대로 선택할 수 있도록 인덱스를 받는다.
        public bool TryGetAttackAnimation(EEnemyAttackStyle attackType, int sequenceIndex,
            out int stateHash, out AnimationClip clip)
        {
            stateHash = 0;
            clip = null;
            if (_animator == null || _attackAnimations == null ||
                attackType == EEnemyAttackStyle.None) return false;

            int count = 0;
            foreach (AttackAnimationEntry entry in _attackAnimations)
                if (IsValidAttackEntry(entry) && entry.AttackType == attackType) count++;
            if (count == 0) return false;

            int selected = Mathf.Max(0, sequenceIndex) % count;
            foreach (AttackAnimationEntry entry in _attackAnimations)
            {
                if (!IsValidAttackEntry(entry) || entry.AttackType != attackType) continue;
                if (selected-- > 0) continue;
                stateHash = Animator.StringToHash(entry.StatePath);
                clip = entry.Clip;
                return true;
            }
            return false;
        }

        public int GetAttackAnimationCount(EEnemyAttackStyle attackType)
        {
            if (_animator == null || _attackAnimations == null) return 0;
            int count = 0;
            foreach (AttackAnimationEntry entry in _attackAnimations)
                if (IsValidAttackEntry(entry) && entry.AttackType == attackType) count++;
            return count;
        }

        public bool TryPlayAttack(EEnemyAttackStyle attackType, int index, out float duration)
        {
            duration = 0f;
            if (!TryGetAttackAnimation(attackType, index, out int stateHash, out AnimationClip clip))
                return false;

            _animator.CrossFade(stateHash, _transitionDuration, 0, 0f);
            _currentState = stateHash;
            duration = clip.length;
            return true;
        }

        private bool IsValidAttackEntry(AttackAnimationEntry entry) =>
            entry != null && entry.Clip != null && entry.AttackType != EEnemyAttackStyle.None &&
            !string.IsNullOrWhiteSpace(entry.StatePath) &&
            _animator.HasState(0, Animator.StringToHash(entry.StatePath));

        private void Update()
        {
            if (_enemy.Combat != null && _enemy.Combat.IsMeleeAttacking) return;
            EEnemyState state = _enemy.StateFlow.CurrentState;
            int nextState;
            if (state == EEnemyState.Patrol)
            {
                nextState = _enemy.StateFlow.PatrolPhase switch
                {
                    EEnemyPatrolPhase.Idle2 => _hasIdle2State ? _idle2StateHash : _idleStateHash,
                    EEnemyPatrolPhase.Moving => _enemy.Locomotion != null &&
                                                _enemy.Locomotion.CurrentSpeed > _walkSpeedThreshold
                        ? (_enemy.StateFlow.IsReturningFromChase && _hasRunState
                            ? _runStateHash : _walkStateHash) : _idleStateHash,
                    _ => _idleStateHash
                };
            }
            else if (state == EEnemyState.Chase)
            {
                nextState = _enemy.Locomotion != null &&
                            _enemy.Locomotion.CurrentSpeed > _walkSpeedThreshold
                    ? (_hasRunState ? _runStateHash : _walkStateHash) : _idleStateHash;
            }
            else if (state == EEnemyState.Idle || _currentState == _walkStateHash ||
                     _currentState == _idle2StateHash || _currentState == _runStateHash)
                nextState = _idleStateHash;
            else
                return;

            if (nextState == _currentState) return;
            _animator.CrossFade(nextState, _transitionDuration, 0);
            _currentState = nextState;
        }
    }
}
