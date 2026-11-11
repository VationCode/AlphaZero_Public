using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.item
{
    /// <summary>명중 인스턴스를 미리 만들고 피해 처리와 이펙트 재생에 재사용한다.</summary>
    public sealed class ImpactPool
    {
        private readonly Stack<ImpactInstance> _available = new();
        private readonly ImpactData _data;
        private readonly Transform _root;
        private readonly Transform _inactiveRoot;

        public ImpactPool(ImpactData data, string ownerName)
        {
            _data = data;
            _root = new GameObject($"{ownerName} Impact Pool").transform;
            _inactiveRoot = new GameObject("Inactive Spawn Root").transform;
            _inactiveRoot.SetParent(_root, false);
            _inactiveRoot.gameObject.SetActive(false);
            for (int i = 0; i < data.InitialPoolSize; i++) _available.Push(Create());
        }

        public void Spawn(RaycastHit hit, Transform owner, float damage, Vector3 fallbackNormal,
            Vector3? contactPointOverride = null, IAttackDamageResolver damageResolver = null)
        {
            if (hit.collider == null) return;
            ImpactInstance instance = _available.Count > 0 ? _available.Pop() : Create();
            instance.Play(hit, owner, damage, fallbackNormal, contactPointOverride, damageResolver);
        }

        public void Return(ImpactInstance instance)
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

        private ImpactInstance Create()
        {
            GameObject obj = _data.EffectPrefab != null
                ? Object.Instantiate(_data.EffectPrefab, _inactiveRoot)
                : new GameObject("Impact");
            if (obj.transform.parent != _inactiveRoot) obj.transform.SetParent(_inactiveRoot, false);
            obj.SetActive(false);
            ImpactInstance instance = obj.GetComponent<ImpactInstance>();
            if (instance == null) instance = obj.AddComponent<ImpactInstance>();
            instance.Initialize(this, _data);
            // 위치를 정하기 전까지 OnEnable과 Play On Awake가 실행되지 않게 한다.
            obj.transform.SetParent(_root, false);
            return instance;
        }
    }
}
