using UnityEngine;

namespace alpha.ingame.enemy
{
    /// <summary>추적 취소와 근접 공격 진입을 판단하고 이동을 조정한다.</summary>
    public sealed class EnemyChaseState : EnemyState
    {
        private bool _moveRequestFailed;

        public EnemyChaseState(EnemyRoot enemy, EnemyStateFlow flow) : base(enemy, flow) { }

        public override void Enter()
        {
            _moveRequestFailed = false;
            Enemy.Chase?.Begin(Enemy.Combat?.Target, Enemy.ActivityOrigin,
                Enemy.transform.position);
        }

        public override void Tick()
        {
            EnemyChaseModule chase = Enemy.Chase;
            if (chase == null)
            {
                if (Enemy.Combat != null)
                    Enemy.Combat.SuppressTargetDuringReturn(Enemy.Combat.Target);
                Flow.ReturnToPatrolOrigin();
                return;
            }

            Transform target = chase.Target;
            EnemyCombatModule combat = Enemy.Combat;
            if (combat != null && combat.IsMeleeAttacking)
            {
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    combat.CancelMeleeAttack();
                    chase.ReportCancellation(EEnemyChaseCancelReason.TargetLost);
                    Flow.ReturnToPatrolOrigin();
                    return;
                }

                // 한 번 시작한 공격은 클립이 끝난 뒤 추적 취소 조건을 다시 평가한다.
                Enemy.Locomotion?.Face(target.position);
                combat.TickMeleeAttack();
                if (!combat.IsMeleeAttacking)
                    chase.ResetCancellationProgress(Enemy.transform.position);
                return;
            }

            bool visible = target != null && combat != null && combat.CanSee(target);
            Vector3 destination = visible && target != null
                ? target.position : chase.LastSeenPosition;
            bool atTarget = chase.HasReachedTarget(destination);
            bool meleeInRange = visible && Enemy.Info != null &&
                                Enemy.Info.AttackStyle == EEnemyAttackStyle.Melee &&
                                combat.IsTargetInRange(target);
            bool pathFailed = _moveRequestFailed || !chase.CanMove || chase.PathFailed;
            EEnemyChaseCancelReason reason = chase.Evaluate(Enemy.transform.position,
                visible, atTarget || meleeInRange, pathFailed);
            if (reason != EEnemyChaseCancelReason.None)
            {
                combat?.CancelMeleeAttack();
                chase.ReportCancellation(reason);
                // 복귀 중에는 같은 대상을 다시 추적하지 않는다.
                if (reason != EEnemyChaseCancelReason.SightLost &&
                    reason != EEnemyChaseCancelReason.TargetLost)
                    Enemy.Combat?.SuppressTargetDuringReturn(target);
                Flow.ReturnToPatrolOrigin();
                return;
            }

            destination = visible ? target.position : chase.LastSeenPosition;
            if (meleeInRange && TryStartMeleeAttack(combat, chase, destination))
                return;

            if (atTarget)
            {
                chase.StopAtBoundary(destination);
                Enemy.Locomotion?.Face(destination);
                _moveRequestFailed = false;
                return;
            }

            if (!chase.CanMove)
            {
                _moveRequestFailed = true;
                return;
            }

            _moveRequestFailed = !chase.MoveTowards(destination);
        }

        private bool TryStartMeleeAttack(EnemyCombatModule combat, EnemyChaseModule chase,
            Vector3 destination)
        {
            EnemyAnimationView animation = Enemy.AnimationView;
            if (animation == null || !animation.isActiveAndEnabled) return false;

            int count = animation.GetAttackAnimationCount(EEnemyAttackStyle.Melee);
            if (!combat.TryChooseMeleeAnimation(count, out int index) ||
                !animation.TryPlayAttack(EEnemyAttackStyle.Melee, index, out float duration))
                return false;

            chase.StopAtBoundary(destination);
            combat.BeginMeleeAttack(duration, chase.Target);
            Enemy.Locomotion?.Face(destination);
            _moveRequestFailed = false;
            return true;
        }

        public override void Exit()
        {
            Enemy.Combat?.CancelMeleeAttack();
            if (Enemy.Chase != null) Enemy.Chase.End();
            else Enemy.Locomotion?.Stop();
        }
    }
}
