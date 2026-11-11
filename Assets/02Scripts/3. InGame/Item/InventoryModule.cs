using System;
using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.item
{
    [DisallowMultipleComponent]
    public class InventoryModule : MonoBehaviour
    {
        [Serializable]
        private struct StartingWeapons
        {
            [SerializeField] private EAreteType _areteType;
            [SerializeField] private GameObject _mainPrefab;
            [SerializeField] private GameObject _subPrefab;

            public EAreteType AreteType => _areteType;
            public GameObject MainPrefab => _mainPrefab;
            public GameObject SubPrefab => _subPrefab;
        }

        // 아레테별 기본 무기 프리팹은 인벤토리가 보관한다.
        [SerializeField] private StartingWeapons[] _startingWeapons;
        [SerializeField] private List<ItemInstance> _items = new();

        public IReadOnlyList<ItemInstance> Items => _items;
        public event Action Changed;

        public bool TryGetStartingWeapons(EAreteType areteType, out GameObject mainPrefab, out GameObject subPrefab)
        {
            if (_startingWeapons != null)
            {
                foreach (StartingWeapons entry in _startingWeapons)
                {
                    if (entry.AreteType != areteType) continue;
                    mainPrefab = entry.MainPrefab;
                    subPrefab = entry.SubPrefab;
                    return true;
                }
            }

            mainPrefab = null;
            subPrefab = null;
            return false;
        }

        public void InitializeStartingWeapons(EAreteType areteType)
        {
            if (!TryGetStartingWeapons(areteType, out GameObject mainPrefab, out GameObject subPrefab)) return;

            // 프리팹에 WeaponData가 연결된 경우에만 보유 아이템으로 등록한다.
            AddStartingWeapon(mainPrefab);
            AddStartingWeapon(subPrefab);
        }

        private void AddStartingWeapon(GameObject prefab)
        {
            WeaponData data = prefab != null ? prefab.GetComponent<EquippableWeapon>()?.Data : null;
            if (data != null && CountOf(data) == 0) TryAdd(data, 1);
        }

        public int CountOf(ItemData data)
        {
            if (data == null) return 0;
            int count = 0;
            foreach (ItemInstance item in _items)
                if (item != null && item.Data == data) count += item.Quantity;
            return count;
        }

        public bool TryAdd(ItemData data, int quantity)
        {
            if (data == null || quantity <= 0) return false;

            int remaining = quantity;
            foreach (ItemInstance item in _items)
            {
                if (item == null || item.Data != data) continue;
                int added = Mathf.Min(remaining, data.StackLimit - item.Quantity);
                if (added <= 0) continue;
                item.ChangeQuantity(added);
                remaining -= added;
                if (remaining == 0) break;
            }

            while (remaining > 0)
            {
                int added = Mathf.Min(remaining, data.StackLimit);
                _items.Add(new ItemInstance(data, added));
                remaining -= added;
            }

            Changed?.Invoke();
            return true;
        }

        public bool TryRemove(ItemData data, int quantity)
        {
            if (data == null || quantity <= 0 || CountOf(data) < quantity) return false;

            int remaining = quantity;
            for (int i = _items.Count - 1; i >= 0 && remaining > 0; i--)
            {
                ItemInstance item = _items[i];
                if (item == null || item.Data != data) continue;
                int removed = Mathf.Min(remaining, item.Quantity);
                item.ChangeQuantity(-removed);
                remaining -= removed;
                if (item.Quantity == 0) _items.RemoveAt(i);
            }

            Changed?.Invoke();
            return true;
        }

        public bool TryUse(ConsumableData data, GameObject user)
        {
            if (data == null || CountOf(data) == 0 || !data.TryUse(user)) return false;
            return TryRemove(data, 1);
        }
    }
}
