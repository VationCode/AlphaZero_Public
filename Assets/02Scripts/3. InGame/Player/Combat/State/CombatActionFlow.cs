using alpha.ingame.input;
using alpha.ingame.item;

namespace alpha.ingame.player
{
    public enum ECombatAction
    {
        Idle,
        Attack,
        Reload,
        Skill
    }

    /// <summary>공격·재장전·스킬이 동시에 실행되지 않도록 행동을 전환한다.</summary>
    public sealed class CombatActionFlow
    {
        private readonly PlayerRoot _playerCore;
        private GunWeapon _activeGun;
        private GunWeapon _knownGun;

        public ECombatAction CurrentState { get; private set; } = ECombatAction.Idle;
        public bool IsAttacking => CurrentState == ECombatAction.Attack;
        public bool IsReloading => CurrentState == ECombatAction.Reload;

        public CombatActionFlow(PlayerRoot playerCore)
        {
            _playerCore = playerCore;
            _knownGun = playerCore.CombatModule.CurrentGun;
        }

        public void Tick()
        {
            AlphaInput input = AlphaInput.Instance;
            if (input == null)
            {
                CancelAll();
                return;
            }

            GunWeapon gun = _playerCore.CombatModule.CurrentGun;
            if (gun != _knownGun || input.IsQuickSlotOpen ||
                (input.IsQuickSlotPressed && !IsAttacking))
            {
                CancelAll();
                _knownGun = gun;
                return;
            }

            // 스킬의 내부 단계는 SkillStateFlow가 담당한다.
            if (CurrentState == ECombatAction.Skill) return;

            if (input.IsReload && _playerCore.CombatModule.CanReload)
            {
                BeginReload(gun);
                return;
            }

            switch (CurrentState)
            {
                case ECombatAction.Attack:
                    UpdateAttack(input, gun);
                    break;
                case ECombatAction.Reload:
                    UpdateReload(gun);
                    break;
                case ECombatAction.Idle:
                    if (input.IsPrimaryPressed && _playerCore.CombatModule.CanAttack)
                        BeginAttack(gun);
                    break;
            }

            // 마지막 탄환을 발사한 프레임에 자동으로 재장전한다.
            if (CurrentState != ECombatAction.Reload && gun != null && gun.CurrentAmmo == 0 &&
                _playerCore.CombatModule.CanReload)
                BeginReload(gun);
        }

        public bool TryBeginSkill()
        {
            if (CurrentState == ECombatAction.Skill) return false;

            CancelCurrent();
            _playerCore.CombatStanceFlow?.ClearReady();
            _playerCore.CombatModule?.CancelSecondary();
            CurrentState = ECombatAction.Skill;
            return true;
        }

        public void EndSkill()
        {
            if (CurrentState == ECombatAction.Skill)
                CurrentState = ECombatAction.Idle;
        }

        public void CancelAll()
        {
            if (CurrentState == ECombatAction.Skill)
                _playerCore.SkillStateFlow?.CancelExecute();
            CancelCurrent();
            _playerCore.CombatStanceFlow?.ClearReady();
        }

        private void BeginAttack(GunWeapon gun)
        {
            if (gun == null) return;
            _activeGun = gun;
            CurrentState = ECombatAction.Attack;
            gun.BeginAttack();
        }

        private void UpdateAttack(AlphaInput input, GunWeapon gun)
        {
            if (_activeGun == null || _activeGun != gun || !_playerCore.CombatModule.CanContinueAttack)
            {
                CancelCurrent();
                return;
            }

            if (!input.IsPrimaryHeld)
            {
                // 차징 무기는 키를 놓을 때 발사한다.
                _activeGun.EndAttack();
                _activeGun = null;
                CurrentState = ECombatAction.Idle;
                return;
            }

            _activeGun.UpdateAttack();
        }

        private void BeginReload(GunWeapon gun)
        {
            if (gun == null) return;
            CancelCurrent();
            if (!gun.TryReload()) return;

            _activeGun = gun;
            CurrentState = ECombatAction.Reload;
        }

        private void UpdateReload(GunWeapon gun)
        {
            if (_activeGun == null || _activeGun != gun)
            {
                CancelCurrent();
                return;
            }
            if (_activeGun.IsReloading) return;

            _activeGun = null;
            CurrentState = ECombatAction.Idle;
        }

        private void CancelCurrent()
        {
            if (CurrentState == ECombatAction.Attack) _activeGun?.CancelAttack();
            if (CurrentState == ECombatAction.Reload) _activeGun?.CancelReload();
            _activeGun = null;
            CurrentState = ECombatAction.Idle;
        }
    }
}
