using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>공격 후 회복 시간과 회복 중 회전 제한을 관리한다.</summary>
    public sealed class BossAttackRecoveryState
    {
        private readonly BossCombatModule _combat;
        private float _endsAt;
        private bool _active;

        public BossAttackRecoveryState(BossCombatModule combat)
        {
            _combat = combat;
        }

        public void Enter(float duration)
        {
            _endsAt = Time.time + Mathf.Max(0f, duration);
            _active = true;
            _combat?.LockRecoveryRotation();
        }

        public bool Tick() => _active && Time.time >= _endsAt;

        public void Exit()
        {
            if (!_active) return;
            _active = false;
            _endsAt = 0f;
            _combat?.UnlockRecoveryRotation();
        }
    }
}
