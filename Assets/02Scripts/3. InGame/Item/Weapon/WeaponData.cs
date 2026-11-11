using System;
using UnityEngine;

namespace alpha.ingame.item
{
    public enum EWeaponKind
    {
        DualBlade = 0,
        Rifle = 1,
        Sniper = 2
    }

    public enum EWeaponHandType
    {
        None = 0,
        Single = 1,
        Dual = 2
    }

    public enum EWeaponHolderSlot
    {
        Primary = 0,
        Secondary = 1
    }

    [Serializable]
    public struct WeaponVisualData
    {
        [SerializeField] private GameObject itemObj;
        [SerializeField] private EWeaponHolderSlot WeaponHolderType;

        public GameObject Prefab => itemObj;
        public EWeaponHolderSlot HolderSlot => WeaponHolderType;
    }

    [CreateAssetMenu(fileName = "WeaponData", menuName = "AlphaZero/Item/Weapon Data")]
    public class WeaponData : ItemData
    {
        // 기존 무기 에셋의 직렬화 필드명을 유지한다.
        [SerializeField] private EWeaponKind WeaponType;
        [SerializeField] private EWeaponHandType WeaponHandType;
        [SerializeField] private WeaponVisualData[] WeaponDatas;

        public EWeaponKind Kind => WeaponType;
        public EWeaponHandType HandType => WeaponHandType;
        public WeaponVisualData[] Visuals => WeaponDatas;

        protected override void OnValidate()
        {
            base.OnValidate();
            SetItemType(EItemType.Weapon);
        }
    }
}
