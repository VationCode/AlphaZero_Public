using UnityEngine;

namespace alpha.ingame.player
{
    public enum ECombatStance
    {
        Normal,
        Combat
    }

    /// <summary>행동과 AimView를 바탕으로 기본 자세와 유지 시간을 결정한다.</summary>
    public sealed class CombatStanceFlow
    {
        private readonly PlayerRoot _playerCore;
        private float _readyUntil;

        public CombatStanceFlow(PlayerRoot playerCore) => _playerCore = playerCore;

        public ECombatStance CurrentState
        {
            get
            {
                ECombatAction action = _playerCore.CombatActionFlow?.CurrentState ?? ECombatAction.Idle;
                // 재장전은 Ready 시간이나 조준 상태를 변경하지 않는다.
                bool combat = action == ECombatAction.Attack ||
                              ((action == ECombatAction.Idle || action == ECombatAction.Reload) &&
                               (Time.time < _readyUntil || _playerCore.CombatModule?.IsAimOrScopeActive == true));
                return combat ? ECombatStance.Combat : ECombatStance.Normal;
            }
        }

        public bool IsCombat => CurrentState == ECombatStance.Combat;

        public void RefreshReady()
        {
            float duration = _playerCore.CombatModule?.ReadyDuration ?? 0f;
            _readyUntil = Time.time + duration;
        }

        public void ClearReady() => _readyUntil = 0f;
    }
}
