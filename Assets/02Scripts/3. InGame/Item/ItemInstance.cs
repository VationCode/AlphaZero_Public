using System;
using UnityEngine;

namespace alpha.ingame.item
{
    [Serializable]
    public sealed class ItemInstance
    {
        [SerializeField] private ItemData _data;
        [SerializeField, Min(1)] private int _quantity = 1;

        public ItemData Data => _data;
        public int Quantity => Mathf.Max(0, _quantity);

        public ItemInstance(ItemData data, int quantity)
        {
            _data = data;
            _quantity = Mathf.Max(1, quantity);
        }

        internal void ChangeQuantity(int difference)
        {
            _quantity = Mathf.Max(0, _quantity + difference);
        }
    }
}
