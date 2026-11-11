using UnityEngine;
namespace alpha.ingame.player
{
    public class AnimatorRootMotion : MonoBehaviour
    {
        [SerializeField]
        private LocomotionModule _locomotionModule;

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            // 추출은 항상 활성화하고 적용 여부를 상태로 구분
            _animator.applyRootMotion = true;
            _animator.updateMode = AnimatorUpdateMode.Normal;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (_locomotionModule == null)
            {
                PlayerRoot playerCore = GetComponentInParent<PlayerRoot>(true);

                if (playerCore != null)
                {
                    _locomotionModule = playerCore.GetComponentInChildren<LocomotionModule>(true);
                }
            }
        }

        private void OnAnimatorMove()
        {

        }
    }
}
