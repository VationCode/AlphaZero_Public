using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.item
{
    /// <summary>총알을 미리 만들고 비활성화된 인스턴스를 재사용한다.</summary>
    public sealed class GunProjectilePool
    {
        private readonly Stack<GunProjectile> _available = new();
        private readonly GunProjectile _prefab;
        private readonly Transform _root;

        public GunProjectilePool(GunProjectile prefab, int initialSize, string name)
        {
            _prefab = prefab;
            _root = new GameObject($"{name} Projectile Pool").transform;
            for (int i = 0; i < initialSize; i++) _available.Push(Create());
        }

        public GunProjectile Rent() => _available.Count > 0 ? _available.Pop() : Create();

        public void Return(GunProjectile projectile)
        {
            projectile.gameObject.SetActive(false);
            _available.Push(projectile);
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _available.Clear();
        }

        private GunProjectile Create()
        {
            GunProjectile projectile;
            if (_prefab != null)
            {
                projectile = Object.Instantiate(_prefab, _root);
            }
            else
            {
                // 투사체 프리팹을 지정하기 전에도 발사를 확인할 수 있는 기본 탄환.
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Gun Projectile";
                sphere.transform.SetParent(_root);
                sphere.transform.localScale = Vector3.one * 0.05f;
                projectile = sphere.AddComponent<GunProjectile>();
            }

            // 이동 구간을 SphereCast로 검사하므로 콜라이더는 크기 설정에만 사용한다.
            foreach (Collider collider in projectile.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in projectile.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            projectile.gameObject.SetActive(false);
            return projectile;
        }
    }
}
