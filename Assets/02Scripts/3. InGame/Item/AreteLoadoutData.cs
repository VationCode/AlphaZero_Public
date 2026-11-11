using UnityEngine;

namespace alpha.ingame.item
{
    [CreateAssetMenu(fileName = "AreteLoadoutData", menuName = "AlphaZero/Item/Arete Loadout Data")]
    public class AreteLoadoutData : ScriptableObject
    {
        [SerializeField] private EAreteType _areteType;
        [SerializeField] private WeaponData _mainWeapon;
        [SerializeField] private WeaponData _subWeapon;

        public EAreteType AreteType => _areteType;
        public WeaponData MainWeapon => _mainWeapon;
        public WeaponData SubWeapon => _subWeapon;
    }
}
