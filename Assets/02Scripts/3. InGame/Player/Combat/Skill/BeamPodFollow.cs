using UnityEngine;
using alpha.ingame;

namespace alpha.ingame.player
{
    /// <summary>빔포드 대형이 플레이어의 위치와 방향을 부드럽게 따라가게 한다.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class BeamPodFollow : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float _followSmoothTime = 0.18f;
        [SerializeField, Min(0f)] private float _turnSpeed = 180f;

        private readonly SmoothPositionFollower _positionFollower = new();
        private SummonLifetime[] _pods;
        private Transform _player;
        private Vector3 _formationOffset;
        private Vector3 _currentPosition;
        private Quaternion _currentRotation;
        private bool _hasFormationOffset;
        private bool _isFollowing;

        private void Awake()
        {
            _pods = GetComponentsInChildren<SummonLifetime>(true);
        }

        public bool Begin(PlayerRoot player)
        {
            _player = player != null ? player.transform : null;
            if (_player == null)
            {
                Debug.LogError($"{nameof(BeamPodFollow)}: 플레이어가 없습니다.", this);
                return false;
            }

            _formationOffset = _player.InverseTransformPoint(transform.position);
            _hasFormationOffset = true;

            // 소환 직전 대형을 플레이어 방향으로 배치하고 이전 추적 속도를 지운다.
            _positionFollower.Reset();
            FollowPlayer(true);
            _isFollowing = true;
            return true;
        }

        // 공격 중에는 그룹을 월드에 고정해 포드가 플레이어 움직임을 상속받지 않게 한다.
        public void Pause()
        {
            _isFollowing = false;
            _positionFollower.Reset();
        }

        public void Resume()
        {
            if (!_hasFormationOffset || _player == null) return;
            _currentPosition = transform.position;
            _currentRotation = transform.rotation;
            _positionFollower.Reset();
            _isFollowing = true;
        }

        private void LateUpdate()
        {
            if (!_isFollowing) return;

            foreach (SummonLifetime pod in _pods)
            {
                if (pod == null || !pod.gameObject.activeInHierarchy) continue;
                FollowPlayer(false);
                return;
            }

            Stop();
        }

        private void OnDisable() => Stop();

        private void Stop()
        {
            _isFollowing = false;
            _positionFollower.Reset();
        }

        private void FollowPlayer(bool snap)
        {
            Vector3 forward = Vector3.ProjectOnPlane(_player.forward, Vector3.up);
            Quaternion targetRotation = forward.sqrMagnitude >= 0.0001f
                ? Quaternion.LookRotation(forward.normalized, Vector3.up)
                : snap ? transform.rotation : _currentRotation;

            // 월드 자세를 기준으로 회전과 위치를 부드럽게 보간한다.
            Quaternion nextRotation = snap
                ? targetRotation
                : Quaternion.RotateTowards(_currentRotation, targetRotation,
                    _turnSpeed * Time.deltaTime);
            Vector3 targetPosition = _player.position + nextRotation * _formationOffset;
            Vector3 nextPosition = snap
                ? targetPosition
                : _positionFollower.Step(_currentPosition, targetPosition, _followSmoothTime);
            _currentPosition = nextPosition;
            _currentRotation = nextRotation;
            transform.SetPositionAndRotation(nextPosition, nextRotation);
        }
    }
}
