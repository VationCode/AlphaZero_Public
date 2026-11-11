using UnityEngine;

namespace alpha.ingame.item
{
    // 회복·버프 등 실제 효과는 이 데이터의 하위 에셋에서 구현한다.
    public abstract class ItemUseEffectData : ScriptableObject
    {
        public abstract void Apply(GameObject user);
    }

    [CreateAssetMenu(fileName = "ConsumableData", menuName = "AlphaZero/Item/Consumable Data")]
    public class ConsumableData : ItemData
    {
        [SerializeField] private ItemUseEffectData _effect;

        public bool TryUse(GameObject user)
        {
            if (user == null || _effect == null) return false;
            _effect.Apply(user);
            return true;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetItemType(EItemType.Consumable);
        }
    }
}
