using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>MiniCrab의 이동 방향을 좌우 걷기 애니메이션으로 표현한다.</summary>
    [DisallowMultipleComponent]
    public sealed class MiniCrabMotionView : MonoBehaviour
    {
        private MiniCrabMotionModule _motion;
        private Animator _animator;
        private bool _started;
        private int _shownDirection = int.MinValue;

        private void Awake()
        {
            _motion = GetComponent<MiniCrabMotionModule>();
            _animator = GetComponentInChildren<Animator>(true);
        }

        private void OnEnable()
        {
            if (_motion != null) _motion.DirectionChanged += ShowDirection;
            if (_started && _motion != null) ShowDirection(_motion.Direction);
        }

        private void Start()
        {
            _started = true;
            _shownDirection = int.MinValue; // Animator 초기화 이후 첫 이동 상태를 확실히 재생한다.
            if (_motion != null) ShowDirection(_motion.Direction);
        }

        private void Update()
        {
            if (_shownDirection == 0 || _shownDirection == int.MinValue ||
                _animator == null || !_animator.isActiveAndEnabled) return;

            string stateName = _shownDirection > 0 ? "Walk_R" : "Walk_L";
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            // 좌우 걷기 클립은 Loop Time이 꺼져 있으므로 MiniCrab에서만 다시 재생한다.
            if (state.shortNameHash != Animator.StringToHash(stateName) ||
                state.normalizedTime >= 1f)
                _animator.Play(stateName, 0, 0f);
        }

        private void OnDisable()
        {
            if (_motion != null) _motion.DirectionChanged -= ShowDirection;
            _shownDirection = int.MinValue;
        }

        private void ShowDirection(int direction)
        {
            if (_animator == null || !_animator.isActiveAndEnabled ||
                direction == _shownDirection) return;

            _shownDirection = direction;
            _animator.Play(direction > 0 ? "Walk_R" : direction < 0 ? "Walk_L" : "Idle", 0, 0f);
        }
    }
}
