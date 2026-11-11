using UnityEngine;

namespace alpha.ingame.item
{
    public enum EItemType
    {
        Weapon = 0,
        Consumable = 1,
        Material = 2,
        Quest = 3
    }

    [CreateAssetMenu(fileName = "ItemData", menuName = "AlphaZero/Item/Item Data")]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string ItemId;
        [SerializeField] private string ItemName;
        [SerializeField] private EItemType ItemType;
        [SerializeField] private Sprite ItemIcon;
        [SerializeField, Min(1)] private int MaxStack = 1;
        [SerializeField] private EAreteType[] AllowedAretes;

        public string Id => string.IsNullOrWhiteSpace(ItemId) ? name : ItemId;
        public string DisplayName => string.IsNullOrWhiteSpace(ItemName) ? name : ItemName;
        public EItemType Type => ItemType;
        public Sprite Icon => ItemIcon;
        public int StackLimit => Mathf.Max(1, MaxStack);

        public bool CanUseBy(EAreteType areteType)
        {
            if (AllowedAretes == null || AllowedAretes.Length == 0) return true;
            foreach (EAreteType allowed in AllowedAretes)
                if (allowed == areteType) return true;
            return false;
        }

        protected void SetItemType(EItemType itemType) => ItemType = itemType;

        protected virtual void OnValidate()
        {
            MaxStack = Mathf.Max(1, MaxStack);
        }
    }
}
