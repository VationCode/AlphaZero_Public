using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.player
{
    public enum EBeamPodCommandResult { None, Attack, Cancel }

    /// <summary>타겟을 포드에 배정하고 3번 키의 접근·복귀 명령을 전달한다.</summary>
    [DefaultExecutionOrder(157)]
    public sealed class BeamPodTargetModule : MonoBehaviour, IPlayerTargetSession
    {
        private enum PodCommandState { Targeting, Approaching }

        private readonly List<Collider> _targets = new();
        private readonly List<int> _activePods = new();
        private readonly List<int> _freePods = new();
        private SummonLifetime[] _pods;
        private BeamPodTravel[] _travels;
        private Collider[] _assignments;
        private PlayerTargetingModule _playerTargeting;
        private BeamPodFollow _follow;
        private bool _isActive;
        private PodCommandState _commandState;
        private int _lastActiveCount;

        private void Awake()
        {
            _pods = GetComponentsInChildren<SummonLifetime>(true);
            _travels = new BeamPodTravel[_pods.Length];
            _assignments = new Collider[_pods.Length];
            _follow = GetComponent<BeamPodFollow>();
            for (int i = 0; i < _pods.Length; i++)
                _travels[i] = _pods[i].GetComponent<BeamPodTravel>();
        }

        public bool IsActive => _isActive && CountActivePods() > 0;
        public bool IsAttacking => _isActive && _commandState == PodCommandState.Approaching;

        public bool Begin(BeamPodSkillData skill, PlayerRoot player)
        {
            if (skill == null || player == null || _follow == null ||
                _pods == null || _pods.Length == 0) return false;
            _playerTargeting = player != null
                ? player.GetComponentInChildren<PlayerTargetingModule>(true) : null;
            if (_playerTargeting == null)
            {
                Debug.LogError($"{nameof(BeamPodTargetModule)}: 플레이어 타겟팅 모듈이 없습니다.", this);
                return false;
            }

            _isActive = true;
            _commandState = PodCommandState.Targeting;
            _lastActiveCount = -1;
            if (_playerTargeting.Begin(this, skill.MaxTargetDistance,
                    Mathf.Min(4, _pods.Length), skill.TargetLayers, skill.SightLayers))
                return true;

            _isActive = false;
            Debug.LogError($"{nameof(BeamPodTargetModule)}: 타겟 표시 UI 또는 카메라 참조가 없습니다.", this);
            return false;
        }

        public Collider GetTarget(SummonLifetime pod)
        {
            if (pod == null || _pods == null) return null;
            for (int i = 0; i < _pods.Length; i++)
                if (_pods[i] == pod) return _assignments[i];
            return null;
        }

        public void OnTargetsChanged(IReadOnlyList<Collider> targets)
        {
            _targets.Clear();
            if (targets != null)
                for (int i = 0; i < targets.Count; i++)
                    if (targets[i] != null) _targets.Add(targets[i]);
            if (_commandState == PodCommandState.Approaching) RefreshAttackAssignments();
            else AssignPods();
        }

        public EBeamPodCommandResult AdvanceCommand()
        {
            if (!IsActive) return EBeamPodCommandResult.None;
            if (_commandState == PodCommandState.Targeting)
            {
                if (_targets.Count == 0) return EBeamPodCommandResult.None;
                _playerTargeting.FreezeTargets(this);
                _commandState = PodCommandState.Approaching;
                _follow.Pause();
                ApplyTravelCommands();
                return EBeamPodCommandResult.Attack;
            }
            CancelAttack();
            return EBeamPodCommandResult.Cancel;
        }

        private void Update()
        {
            if (!_isActive) return;
            int activeCount = CountActivePods();
            if (activeCount == 0)
            {
                End();
                return;
            }

            if (activeCount != _lastActiveCount)
            {
                if (_commandState == PodCommandState.Approaching) RefreshAttackAssignments();
                else AssignPods();
            }
        }

        private void LateUpdate()
        {
            if (!_isActive || _commandState != PodCommandState.Approaching) return;
            // 한 포드라도 플레이어와의 한계를 넘으면 전체 공격을 취소한다.
            for (int i = 0; i < _pods.Length; i++)
                if (IsAvailable(i) && _travels[i].IsBeyondPlayerLimit)
                {
                    CancelAttack();
                    return;
                }
        }

        private void CancelAttack()
        {
            _commandState = PodCommandState.Targeting;
            // 복귀를 시작하는 프레임에 현재 화면의 타겟 표시도 다시 켠다.
            _playerTargeting.Retarget(this);
            ApplyTravelCommands();
            _follow.Resume();
        }

        private void AssignPods()
        {
            if (_pods == null) return;
            for (int i = _targets.Count - 1; i >= 0; i--)
                if (_targets[i] == null) _targets.RemoveAt(i);
            _activePods.Clear();
            for (int i = 0; i < _pods.Length; i++)
            {
                _assignments[i] = null;
                if (IsAvailable(i)) _activePods.Add(i);
            }
            _lastActiveCount = _activePods.Count;

            int targetCount = Mathf.Min(_targets.Count, _activePods.Count);
            if (targetCount > 0)
            {
                int[] remaining = new int[targetCount];
                for (int i = 0; i < targetCount; i++)
                    remaining[i] = _activePods.Count / targetCount +
                                   (i < _activePods.Count % targetCount ? 1 : 0);

                int[] current = new int[_activePods.Count];
                int[] best = new int[_activePods.Count];
                float bestCohesion = float.PositiveInfinity;
                float bestDistance = float.PositiveInfinity;
                SearchAssignments(0, remaining, current, best,
                    ref bestCohesion, ref bestDistance);
                for (int i = 0; i < _activePods.Count; i++)
                    _assignments[_activePods[i]] = _targets[best[i]];
            }

            ApplyTravelCommands();
        }

        // 공격 중에는 살아 있는 타겟을 맡은 포드를 유지하고 빈 포드만 다시 배정한다.
        private void RefreshAttackAssignments()
        {
            if (_pods == null) return;
            for (int i = _targets.Count - 1; i >= 0; i--)
                if (!IsTargetAvailable(_targets[i])) _targets.RemoveAt(i);
            if (_targets.Count == 0)
            {
                CancelAttack();
                return;
            }

            int[] assignedCounts = new int[_targets.Count];
            _activePods.Clear();
            _freePods.Clear();
            for (int i = 0; i < _pods.Length; i++)
            {
                if (!IsAvailable(i))
                {
                    _assignments[i] = null;
                    continue;
                }

                _activePods.Add(i);
                int targetIndex = TargetIndex(_assignments[i]);
                if (targetIndex >= 0)
                {
                    assignedCounts[targetIndex]++;
                    continue;
                }

                _assignments[i] = null;
                _freePods.Add(i);
            }
            _lastActiveCount = _activePods.Count;

            // 빈 포드만 남은 타겟에 고르게 붙이고, 동률이면 가까운 쪽을 택한다.
            foreach (int podIndex in _freePods)
            {
                int bestTarget = -1;
                int fewestPods = int.MaxValue;
                float nearestDistance = float.PositiveInfinity;
                for (int i = 0; i < _targets.Count; i++)
                {
                    float distance = (_pods[podIndex].transform.position -
                                      _targets[i].bounds.center).sqrMagnitude;
                    if (assignedCounts[i] > fewestPods ||
                        assignedCounts[i] == fewestPods && distance >= nearestDistance)
                        continue;
                    bestTarget = i;
                    fewestPods = assignedCounts[i];
                    nearestDistance = distance;
                }

                if (bestTarget < 0) continue;
                Collider target = _targets[bestTarget];
                _assignments[podIndex] = target;
                assignedCounts[bestTarget]++;
                _travels[podIndex].MoveToTarget(target);
            }
        }

        private int TargetIndex(Collider target)
        {
            if (!IsTargetAvailable(target)) return -1;
            for (int i = 0; i < _targets.Count; i++)
                if (_targets[i] == target) return i;
            return -1;
        }

        private static bool IsTargetAvailable(Collider target) =>
            target != null && target.enabled && target.gameObject.activeInHierarchy;

        private void ApplyTravelCommands()
        {
            for (int i = 0; i < _pods.Length; i++)
            {
                if (!IsAvailable(i)) continue;
                if (_commandState == PodCommandState.Approaching && _assignments[i] != null)
                    _travels[i].MoveToTarget(_assignments[i]);
                else _travels[i].ReturnToFormation();
            }
        }

        // 먼저 같은 적을 맡는 포드 사이의 거리를 최소화하고, 동률이면 적과 가까운 배정을 택한다.
        private void SearchAssignments(int podIndex, int[] remaining, int[] current, int[] best,
            ref float bestCohesion, ref float bestDistance)
        {
            if (podIndex == _activePods.Count)
            {
                float cohesion = 0f;
                float distance = 0f;
                for (int i = 0; i < current.Length; i++)
                {
                    Vector3 position = _pods[_activePods[i]].transform.position;
                    distance += (position - _targets[current[i]].bounds.center).sqrMagnitude;
                    for (int j = 0; j < i; j++)
                        if (current[j] == current[i])
                            cohesion += (position - _pods[_activePods[j]].transform.position)
                                .sqrMagnitude;
                }

                if (cohesion > bestCohesion + 0.0001f ||
                    Mathf.Abs(cohesion - bestCohesion) <= 0.0001f &&
                    distance >= bestDistance) return;
                bestCohesion = cohesion;
                bestDistance = distance;
                System.Array.Copy(current, best, current.Length);
                return;
            }

            for (int targetIndex = 0; targetIndex < remaining.Length; targetIndex++)
            {
                if (remaining[targetIndex] == 0) continue;
                current[podIndex] = targetIndex;
                remaining[targetIndex]--;
                SearchAssignments(podIndex + 1, remaining, current, best,
                    ref bestCohesion, ref bestDistance);
                remaining[targetIndex]++;
            }
        }

        private int CountActivePods()
        {
            int count = 0;
            for (int i = 0; i < _pods.Length; i++)
                if (IsAvailable(i)) count++;
            return count;
        }

        private bool IsAvailable(int index) =>
            _pods[index] != null && _pods[index].gameObject.activeInHierarchy &&
            !_pods[index].IsExpired && _travels[index] != null;

        private void OnDisable() => End();

        private void End()
        {
            _playerTargeting?.End(this);
            _isActive = false;
            _commandState = PodCommandState.Targeting;
            _targets.Clear();
            if (_assignments != null) System.Array.Clear(_assignments, 0, _assignments.Length);
        }
    }
}
