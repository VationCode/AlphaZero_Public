using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>캐논 모델의 공격 애니메이션을 재생한다.</summary>
    public sealed class CanonAnimator : MonoBehaviour
    {
        private static readonly int ShootHash = Animator.StringToHash("Shoot");

        [SerializeField] private Animator _animator;

        public void PlayShoot()
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return;
            _animator.SetTrigger(ShootHash);
        }
    }
}
