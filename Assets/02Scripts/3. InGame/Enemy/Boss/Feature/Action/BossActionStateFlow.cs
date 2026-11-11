using System;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    public enum EBossActionState
    {
        Dormant,
        Intro,
        Combat,
        PhaseTransition,
        Dead
    }

    /// <summary>보스의 전체 행동 상태인 대기, 인트로, 전투, 페이즈 전환, 사망을 관리한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BossActionStateFlow : MonoBehaviour
    {
        [SerializeField] private bool _startInCombat;
        [SerializeField, Min(1)] private int _phaseCount = 2;

        public EBossActionState CurrentState { get; private set; } = EBossActionState.Dormant;
        public int CurrentPhase { get; private set; } = 1;
        public int PhaseCount => Mathf.Max(1, _phaseCount);

        public event Action<EBossActionState> StateChanged;

        private void Start()
        {
            // 진입 연출을 연결하기 전에는 씬 설정에 따라 바로 전투를 시작한다.
            if (_startInCombat) TryStartCombat();
        }

        public bool TryBeginIntro()
        {
            if (CurrentState != EBossActionState.Dormant) return false;
            ChangeState(EBossActionState.Intro);
            return true;
        }

        public bool TryStartCombat()
        {
            if (CurrentState != EBossActionState.Dormant &&
                CurrentState != EBossActionState.Intro) return false;
            ChangeState(EBossActionState.Combat);
            return true;
        }

        public bool TryBeginPhaseTransition()
        {
            if (CurrentState != EBossActionState.Combat ||
                CurrentPhase >= PhaseCount) return false;
            ChangeState(EBossActionState.PhaseTransition);
            return true;
        }

        public bool TryCompletePhaseTransition()
        {
            if (CurrentState != EBossActionState.PhaseTransition) return false;
            CurrentPhase++;
            ChangeState(EBossActionState.Combat);
            return true;
        }

        public void MarkDead()
        {
            if (CurrentState != EBossActionState.Dead)
                ChangeState(EBossActionState.Dead);
        }

        private void ChangeState(EBossActionState next)
        {
            CurrentState = next;
            StateChanged?.Invoke(next);
        }
    }
}
