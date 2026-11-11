using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.item
{
    /// <summary>무기별 총구 이펙트를 미리 만들고 발사마다 재사용한다.</summary>
    public sealed class MuzzleEffectPool
    {
        private readonly Stack<MuzzleEffectInstance> _available = new();
        private readonly GameObject _prefab;
        private readonly Transform _root;
        private readonly float _maxLifetime;

        public MuzzleEffectPool(GameObject prefab, int initialSize, float maxLifetime, string ownerName)
        {
            _prefab = prefab;
            _maxLifetime = Mathf.Max(0.01f, maxLifetime);
            _root = new GameObject($"{ownerName} Muzzle Effect Pool").transform;
            for (int i = 0; i < initialSize; i++) _available.Push(Create());
        }

        public void Spawn(Transform muzzlePoint)
        {
            if (_root == null || muzzlePoint == null) return;
            MuzzleEffectInstance instance = _available.Count > 0 ? _available.Pop() : Create();
            instance.Play(muzzlePoint, _maxLifetime);
        }

        public void Return(MuzzleEffectInstance instance)
        {
            if (_root == null) return;
            instance.gameObject.SetActive(false);
            _available.Push(instance);
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _available.Clear();
        }

        private MuzzleEffectInstance Create()
        {
            GameObject obj = Object.Instantiate(_prefab, _root);
            MuzzleEffectInstance instance = obj.GetComponent<MuzzleEffectInstance>();
            if (instance == null) instance = obj.AddComponent<MuzzleEffectInstance>();
            instance.Initialize(this);
            obj.SetActive(false);
            return instance;
        }
    }
}
