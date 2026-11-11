using alpha.ingame.item;
using UnityEngine;

namespace alpha.ingame.player
{
    public enum EEquipmentSlot
    {
        Main = 0,
        Sub = 1
    }

    public class EquipmentModule : MonoBehaviour
    {
        private PlayerRoot _playerCore;
        private GunWeapon _observedGun;

        public GameObject MainWeapon { get; private set; }
        public GameObject SubWeapon { get; private set; }
        public GameObject CurrentWeapon { get; private set; }
        public EEquipmentSlot ActiveSlot { get; private set; } = EEquipmentSlot.Main;
        public WeaponData CurrentWeaponData => CurrentWeapon != null
            ? CurrentWeapon.GetComponent<EquippableWeapon>()?.Data
            : null;
        public EWeaponKind? CurrentWeaponKind => CurrentWeaponData?.Kind;

        private void Awake() => _playerCore = GetComponentInParent<PlayerRoot>(true);

        public void Initialize(EAreteType areteType, GameObject model, InventoryModule inventory)
        {
            UnsubscribeAmmo();
            _playerCore?.AmmoDisplayChannel.Clear();
            MainWeapon = null;
            SubWeapon = null;
            CurrentWeapon = null;
            ActiveSlot = EEquipmentSlot.Main;
            if (model == null || inventory == null) return;
            if (!inventory.TryGetStartingWeapons(areteType, out GameObject mainPrefab, out GameObject subPrefab)) return;

            Transform holder = FindHolder(model.transform);
            if (holder == null)
            {
                Debug.LogWarning($"{nameof(EquipmentModule)}: {areteType} 모델에 WeaponHolder가 없습니다.", this);
                return;
            }

            // 인벤토리의 프리팹으로 두 무기를 생성하고 Main만 표시한다.
            bool holderWasActive = holder.gameObject.activeSelf;
            holder.gameObject.SetActive(false);

            PlayerRoot owner = GetComponentInParent<PlayerRoot>();
            MainWeapon = CreateWeapon(mainPrefab, holder, owner);
            SubWeapon = CreateWeapon(subPrefab, holder, owner);

            if (SubWeapon != null)
            {
                SubWeapon.GetComponent<EquippableWeapon>()?.SetEquipped(false);
                SubWeapon.SetActive(false);
            }
            if (MainWeapon != null)
            {
                MainWeapon.GetComponent<EquippableWeapon>()?.SetEquipped(true);
                MainWeapon.SetActive(true);
            }
            holder.gameObject.SetActive(holderWasActive);

            ActiveSlot = EEquipmentSlot.Main;
            CurrentWeapon = MainWeapon;
            ObserveCurrentAmmo();
        }

        public bool SelectSlot(EEquipmentSlot slot)
        {
            if (slot != EEquipmentSlot.Main && slot != EEquipmentSlot.Sub) return false;
            GameObject selected = slot == EEquipmentSlot.Main ? MainWeapon : SubWeapon;
            if (selected == null) return false;

            // 퀵슬롯 UI는 이 메서드만 호출하면 된다.
            if (MainWeapon != null)
            {
                MainWeapon.GetComponent<EquippableWeapon>()?.SetEquipped(slot == EEquipmentSlot.Main);
                MainWeapon.SetActive(slot == EEquipmentSlot.Main);
            }
            if (SubWeapon != null)
            {
                SubWeapon.GetComponent<EquippableWeapon>()?.SetEquipped(slot == EEquipmentSlot.Sub);
                SubWeapon.SetActive(slot == EEquipmentSlot.Sub);
            }
            ActiveSlot = slot;
            CurrentWeapon = selected;
            ObserveCurrentAmmo();
            return true;
        }

        private void ObserveCurrentAmmo()
        {
            UnsubscribeAmmo();
            _observedGun = CurrentWeapon != null ? CurrentWeapon.GetComponent<GunWeapon>() : null;
            if (_observedGun == null)
            {
                _playerCore?.AmmoDisplayChannel.Clear();
                return;
            }

            _observedGun.AmmoChanged += HandleAmmoChanged;
            HandleAmmoChanged(_observedGun.CurrentAmmo, _observedGun.MagazineCapacity);
        }

        private void HandleAmmoChanged(int currentAmmo, int magazineCapacity)
        {
            _playerCore?.AmmoDisplayChannel.Publish(new AmmoDisplayState(currentAmmo, magazineCapacity));
        }

        private void UnsubscribeAmmo()
        {
            if (_observedGun != null) _observedGun.AmmoChanged -= HandleAmmoChanged;
            _observedGun = null;
        }

        private void OnDestroy() => UnsubscribeAmmo();

        private static GameObject CreateWeapon(GameObject prefab, Transform holder, PlayerRoot owner)
        {
            if (prefab == null) return null;
            GameObject weapon = Instantiate(prefab, holder, false);
            weapon.transform.localPosition = prefab.transform.localPosition;
            weapon.transform.localRotation = prefab.transform.localRotation;
            weapon.transform.localScale = prefab.transform.localScale;
            weapon.GetComponent<EquippableWeapon>()?.Initialize(owner);
            return weapon;
        }

        private static Transform FindHolder(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "WeaponHolder") return child;
            return null;
        }
    }
}
