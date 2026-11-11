using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>설치형 쉴드의 지속시간과 공통 설치 조건을 보관한다.</summary>
    [CreateAssetMenu(fileName = "ShieldPlacement", menuName = "AlphaZero/Skill/Shield")]
    public sealed class ShieldSkillData : DeployableSkillData
    {
        [Header("Shield")]
        [SerializeField, Min(0.1f)] private float _duration = 8f;
        [Tooltip("Excute 이벤트 후 이동 입력을 다시 받을 때까지의 시간(초)")]
        [SerializeField, Min(0f)] private float _moveDelayAfterExecute = 0.2f;

        public float Duration => Mathf.Max(0.1f, _duration);
        public float MoveDelayAfterExecute => Mathf.Max(0f, _moveDelayAfterExecute);

        public override bool TryActivateInstance(GameObject instance)
        {
            if (instance != null && instance.TryGetComponent(out ShieldInstance shield))
            {
                shield.Activate(Duration);
                return true;
            }

            Debug.LogError($"{nameof(ShieldSkillData)}: 쉴드 프리팹에 {nameof(ShieldInstance)}가 없습니다.", instance);
            return false;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _duration = Mathf.Max(0.1f, _duration);
            _moveDelayAfterExecute = Mathf.Max(0f, _moveDelayAfterExecute);
        }
    }
}
