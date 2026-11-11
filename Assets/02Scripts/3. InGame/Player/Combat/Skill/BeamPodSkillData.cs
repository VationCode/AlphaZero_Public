using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>미리 배치된 빔포드를 소환하는 스킬 데이터.</summary>
    [CreateAssetMenu(fileName = "BeamPodSkill", menuName = "AlphaZero/Skill/Beam Pod")]
    public sealed class BeamPodSkillData : SkillData
    {
        [Header("Summon")]
        [SerializeField] private BeamPodSummon _beamPodGroupPrefab;

        [Header("Targeting")]
        [SerializeField, Min(0.1f)] private float _maxTargetDistance = 20f;
        [SerializeField] private LayerMask _targetLayers = 1 << 9;
        [SerializeField] private LayerMask _sightLayers = Physics.DefaultRaycastLayers;

        public BeamPodSummon BeamPodGroupPrefab => _beamPodGroupPrefab;
        public float MaxTargetDistance => Mathf.Max(0.1f, _maxTargetDistance);
        public LayerMask TargetLayers => _targetLayers;
        public LayerMask SightLayers => _sightLayers;
    }
}
