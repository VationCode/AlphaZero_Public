using System;
using System.Collections.Generic;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>거리와 최근 사용 순서를 보고 공격 또는 추적을 요청한다.</summary>
    public sealed class BossIdleState
    {
        private BossCombatFlow _flow;
        private BossCombatModule _combat;
        private readonly List<Transform> _combatTargets = new();
        private readonly List<BossAttackPattern> _castable = new();
        private readonly List<BossAttackPattern> _chaseable = new();
        private readonly Dictionary<string, int> _lastStartedOrder = new();
        private readonly Comparison<BossAttackPattern> _compareCastable;
        private readonly Comparison<BossAttackPattern> _compareChaseable;
        private int _selectionOrder;
        private bool _lastAttackWasDisruptive;

        public Transform CurrentTarget { get; private set; }

        public BossIdleState()
        {
            _compareCastable = CompareCastable;
            _compareChaseable = CompareChaseable;
        }

        public void Bind(BossCombatFlow flow, BossCombatModule combat)
        {
            _flow = flow;
            _combat = combat;
            ResetSelectionHistory();
        }

        public void ResetSelectionHistory()
        {
            CurrentTarget = null;
            _combatTargets.Clear();
            _castable.Clear();
            _chaseable.Clear();
            _lastStartedOrder.Clear();
            _selectionOrder = 0;
            _lastAttackWasDisruptive = false;
        }

        public void RecordAttackStarted(string attackId)
        {
            if (string.IsNullOrEmpty(attackId)) return;
            _lastStartedOrder[attackId] = ++_selectionOrder;
            _lastAttackWasDisruptive = _flow?.IsDisruptiveAttack(attackId) == true;
        }

        public void Tick()
        {
            if (_flow == null || _combat == null || _combat.Owner == null) return;

            float maxDistance = 0f;
            foreach (BossAttackPattern attack in _flow.AttackPatterns)
                if (attack != null && attack.IsEnabled && attack.Conditions != null)
                    maxDistance = Mathf.Max(maxDistance,
                        attack.Conditions.MaxSelectionDistance);
            if (maxDistance <= 0f)
            {
                CurrentTarget = null;
                return;
            }

            Transform target = CurrentTarget;
            if (!_combat.IsCombatTargetValid(target) ||
                HorizontalSqrDistance(_combat.Owner.position, target.position) >
                maxDistance * maxDistance)
            {
                _combat.FindCombatTargets(maxDistance, _combatTargets);
                target = SelectNearestCombatTarget(_combat.Owner.position, maxDistance);
            }
            CurrentTarget = target;
            if (target == null) return;

            Vector3 offset = target.position - _combat.Owner.position;
            offset.y = 0f;
            float distance = offset.magnitude;

            _castable.Clear();
            _chaseable.Clear();
            foreach (BossAttackPattern attack in _flow.AttackPatterns)
            {
                if (attack == null || !attack.IsEnabled || !_flow.IsAttackReady(attack) ||
                    distance < attack.Conditions.MinSelectionDistance ||
                    distance > attack.Conditions.MaxSelectionDistance) continue;

                if (distance <= attack.Conditions.CastDistance + BossAttackingState.CastTolerance)
                    _castable.Add(attack);
                else if (!_flow.IsChaseBlocked(attack, target, distance))
                    _chaseable.Add(attack);
            }

            // 가능한 공격을 먼저 시도하고, 오래 사용하지 않은 공격을 우선한다.
            _castable.Sort(_compareCastable);
            foreach (BossAttackPattern attack in _castable)
                if (_flow.TryBeginAttack(target, attack)) return;

            // 방향 조정이 필요하면 시전 가능한 후보를 추적하고, 이후 먼 후보를 고려한다.
            foreach (BossAttackPattern attack in _castable)
                if (_flow.TryBeginChasing(target, attack)) return;
            _chaseable.Sort(_compareChaseable);
            foreach (BossAttackPattern attack in _chaseable)
                if (_flow.TryBeginChasing(target, attack)) return;
        }

        private Transform SelectNearestCombatTarget(Vector3 origin, float maxDistance)
        {
            Transform nearest = null;
            float nearestSqrDistance = maxDistance * maxDistance;
            foreach (Transform candidate in _combatTargets)
            {
                if (candidate == null) continue;
                float sqrDistance = HorizontalSqrDistance(origin, candidate.position);
                if (sqrDistance > nearestSqrDistance) continue;
                nearestSqrDistance = sqrDistance;
                nearest = candidate;
            }
            return nearest;
        }

        private static float HorizontalSqrDistance(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }

        private int CompareCastable(BossAttackPattern first,
            BossAttackPattern second)
        {
            int firstOrder = _lastStartedOrder.TryGetValue(first.AttackId,
                out int firstStartedOrder) ? firstStartedOrder : 0;
            int secondOrder = _lastStartedOrder.TryGetValue(second.AttackId,
                out int secondStartedOrder) ? secondStartedOrder : 0;
            int comparison = firstOrder.CompareTo(secondOrder);
            if (comparison != 0) return comparison;

            // 같은 사용 순서라면 연속 넉백·넉다운 대신 다른 반응을 끼운다.
            if (_lastAttackWasDisruptive)
            {
                comparison = _flow.IsDisruptiveAttack(first.AttackId)
                    .CompareTo(_flow.IsDisruptiveAttack(second.AttackId));
                if (comparison != 0) return comparison;
            }

            // 사용 이력이 같으면 짧은 사거리의 전용 공격을 먼저 사용한다.
            comparison = first.Conditions.MaxSelectionDistance.CompareTo(
                second.Conditions.MaxSelectionDistance);
            return comparison != 0 ? comparison :
                string.CompareOrdinal(first.AttackId, second.AttackId);
        }

        private int CompareChaseable(BossAttackPattern first,
            BossAttackPattern second)
        {
            // 시전 거리까지 가장 적게 이동할 후보를 선택한다.
            int comparison = second.Conditions.CastDistance.CompareTo(
                first.Conditions.CastDistance);
            return comparison != 0 ? comparison : CompareCastable(first, second);
        }
    }
}
