using alpha.ingame.player;
using UnityEngine;

namespace alpha.ingame.item
{
    /// <summary>
    /// 장착 가능한 무기 프리팹의 공통 정보: 무기 데이터와 소유자를 연결하고 장착 상태를 보관한다.
    /// 장비 모듈이 생성 및 퀵슬롯 전환 시 Initialize/SetEquipped를 호출한다.
    /// </summary>
    public abstract class EquippableWeapon : MonoBehaviour
    {
        [SerializeField] private WeaponData _weaponData;
        [SerializeField] private WeaponSecondaryAction _secondaryAction;

        public WeaponData Data => _weaponData;
        public PlayerRoot Owner { get; private set; }
        public bool IsEquipped { get; private set; }
        public WeaponSecondaryAction SecondaryAction => _secondaryAction;

        public virtual void Initialize(PlayerRoot owner)
        {
            Owner = owner;
            IsEquipped = false;
        }

        public void SetEquipped(bool equipped)
        {
            if (!equipped) _secondaryAction?.Cancel();
            IsEquipped = equipped;
        }
    }
}
