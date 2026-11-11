using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>설치형 스킬이 공유하는 프리팹과 설치 조건.</summary>
    public abstract class DeployableSkillData : SkillData
    {
        [Header("Placement")]
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Vector3 _placementOffset;
        [SerializeField, Min(0.1f)] private float _maxDistance = 12f;
        [SerializeField, Range(0f, 89f)] private float _maxSlopeAngle = 30f;
        [SerializeField, Min(0.01f)] private float _clearanceRadius = 0.5f;
        [SerializeField, Min(0.02f)] private float _clearanceHeight = 1f;
        [SerializeField] private LayerMask _surfaceMask = ~0;
        [SerializeField] private LayerMask _obstacleMask = ~0;

        [Header("Cast")]
        [SerializeField] private string _animationState;
        [SerializeField] private AnimationClip _castAnimationClip;
        [SerializeField, Min(0.1f)] private float _castDuration = 1f;

        public GameObject Prefab => _prefab;
        public Vector3 PlacementOffset => _placementOffset;
        public float MaxDistance => _maxDistance;
        public float MaxSlopeAngle => _maxSlopeAngle;
        public float ClearanceRadius => _clearanceRadius;
        public float ClearanceHeight => _clearanceHeight;
        public LayerMask SurfaceMask => _surfaceMask;
        public LayerMask ObstacleMask => _obstacleMask;
        public string AnimationState => _animationState;
        public AnimationClip CastAnimationClip => _castAnimationClip;
        public float CastDuration => _castDuration;

        // 설치된 객체별 초기 설정은 데이터 타입이 담당한다.
        public virtual bool TryActivateInstance(GameObject instance) => instance != null;

        protected override void OnValidate()
        {
            base.OnValidate();
            _maxDistance = Mathf.Max(0.1f, _maxDistance);
            _clearanceRadius = Mathf.Max(0.01f, _clearanceRadius);
            _clearanceHeight = Mathf.Max(_clearanceRadius * 2f, _clearanceHeight);
            _castDuration = Mathf.Max(0.1f, _castDuration);
        }
    }
}
