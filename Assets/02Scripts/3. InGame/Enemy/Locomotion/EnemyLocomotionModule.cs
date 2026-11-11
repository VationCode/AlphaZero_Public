using UnityEngine;
using UnityEngine.AI;

namespace alpha.ingame.enemy
{
    /// <summary>NavMeshAgent의 이동·정지·회전을 한곳에서 실행한다.</summary>
    public sealed class EnemyLocomotionModule : MonoBehaviour
    {
        private EnemyRoot _enemy;
        private NavMeshAgent _agent;
        private Vector3 _destination;
        private float _destinationRequestedAt;
        private bool _hasDestination;
        public bool CanMove => _agent != null && _agent.enabled && _agent.isOnNavMesh;
        public float CurrentSpeed => CanMove && !_agent.isStopped ? _agent.velocity.magnitude : 0f;

        private float HorizontalDistanceToDestination
        {
            get
            {
                Vector3 offset = _destination - _agent.transform.position;
                offset.y = 0f;
                return offset.magnitude;
            }
        }

        private void Awake()
        {
            _enemy = GetComponentInParent<EnemyRoot>(true);
            if (_enemy != null) _agent = _enemy.GetComponent<NavMeshAgent>();
            if (_agent == null)
                Debug.LogError($"{name}: 적 루트에 NavMeshAgent가 필요합니다.", this);
        }

        public bool MoveTo(Vector3 destination, float speed)
        {
            if (_enemy == null || _enemy.IsDead || !CanMove) return false;
            _agent.speed = Mathf.Max(0.01f, speed);
            _agent.stoppingDistance = 0f;
            _agent.isStopped = false;
            // 움직이는 추적 대상은 충분히 위치가 달라졌을 때 경로를 다시 요청한다.
            if (_hasDestination && (_agent.hasPath || _agent.pathPending) &&
                (destination - _destination).sqrMagnitude < 0.01f)
            {
                _destination = destination;
                return true;
            }
            if (!_agent.SetDestination(destination))
            {
                Stop();
                return false;
            }
            _destination = destination;
            _destinationRequestedAt = Time.time;
            _hasDestination = true;
            return true;
        }

        public void Face(Vector3 position)
        {
            if (_enemy == null || _enemy.IsDead) return;
            Transform root = _enemy.transform;
            Vector3 direction = position - root.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float turnSpeed = _agent != null ? _agent.angularSpeed : 540f;
            root.rotation = Quaternion.RotateTowards(root.rotation, targetRotation,
                turnSpeed * Time.deltaTime);
        }

        public bool HasReachedDestination(float tolerance)
        {
            if (!CanMove || !_hasDestination || _agent.pathPending ||
                HorizontalDistanceToDestination > tolerance)
                return false;

            return !_agent.hasPath ||
                   (!float.IsInfinity(_agent.remainingDistance) &&
                    _agent.remainingDistance <= tolerance);
        }

        public bool HasPathFailed(float arrivalTolerance, float requestDelay)
        {
            return CanMove && _hasDestination && !_agent.pathPending &&
                   Time.time - _destinationRequestedAt > requestDelay &&
                   (_agent.pathStatus != NavMeshPathStatus.PathComplete ||
                    (!_agent.hasPath && HorizontalDistanceToDestination > arrivalTolerance));
        }

        public bool WarpTo(Vector3 position) => CanMove && _agent.Warp(position);

        public void Stop()
        {
            _hasDestination = false;
            if (!CanMove) return;
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }
    }
}
