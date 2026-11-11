using UnityEngine;

namespace alpha.ingame.item
{
    public enum EImpactTargetMode { Single = 0, Area = 1 }
    public enum EImpactTiming { Instant = 0, Duration = 1 }

    /// <summary>명중 후 피해 범위·시간과 시각 효과를 정의한다. 발사 방식과 독립적으로 재사용한다.</summary>
    [CreateAssetMenu(fileName = "ImpactData", menuName = "AlphaZero/Item/Impact Data")]
    public sealed class ImpactData : ScriptableObject
    {
        [SerializeField] private EImpactTargetMode _targetMode = EImpactTargetMode.Single;
        [SerializeField] private EImpactTiming _timing = EImpactTiming.Instant;
        [SerializeField, Min(0f)] private float _radius = 2f;
        [SerializeField, Min(0.01f)] private float _duration = 3f;
        [SerializeField, Min(0.01f)] private float _tickInterval = 0.5f;
        [SerializeField] private LayerMask _targetLayers = ~0;
        [SerializeField] private GameObject _effectPrefab;
        [SerializeField, Min(0)] private int _initialPoolSize = 8;
        [SerializeField, Min(0.01f)] private float _effectFallbackLifetime = 1f;

        public EImpactTargetMode TargetMode => _targetMode;
        public EImpactTiming Timing => _timing;
        public float Radius => Mathf.Max(0f, _radius);
        public float Duration => Mathf.Max(0.01f, _duration);
        public float TickInterval => Mathf.Max(0.01f, _tickInterval);
        public LayerMask TargetLayers => _targetLayers;
        public GameObject EffectPrefab => _effectPrefab;
        public int InitialPoolSize => Mathf.Max(0, _initialPoolSize);
        public float EffectFallbackLifetime => Mathf.Max(0.01f, _effectFallbackLifetime);
    }
}
