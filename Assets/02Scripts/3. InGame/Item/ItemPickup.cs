using UnityEngine;

namespace alpha.ingame.item
{
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField] private ItemData _data;
        [SerializeField, Min(1)] private int _quantity = 1;

        private bool _collected;

        private void OnTriggerEnter(Collider other)
        {
            TryCollect(other.GetComponentInParent<InventoryModule>());
        }

        public bool TryCollect(InventoryModule inventory)
        {
            if (_collected || inventory == null || !inventory.TryAdd(_data, _quantity)) return false;
            _collected = true;
            Destroy(gameObject);
            return true;
        }
    }
}
