using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>Animator 오브젝트의 공격 이벤트를 부모 AnimationView에 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class BossAttackAnimationEventRelay : MonoBehaviour
    {
        private BossAnimationView _view;

        private void Awake() => _view = GetComponentInParent<BossAnimationView>();

        // 클립의 String Parameter는 현재 공격 패턴의 Movement ID와 일치시킨다.
        public void MovementStart(AnimationEvent animationEvent) =>
            _view?.NotifyMovementStart(animationEvent);
        public void MovementEnd(AnimationEvent animationEvent) =>
            _view?.NotifyMovementEnd(animationEvent);

        // 클립의 String Parameter에 인스펙터 Event Key(예: ClawTrail)를 지정한다.
        public void EffectStart(string eventKey) => _view?.NotifyEffect(eventKey, true);
        public void EffectEnd(string eventKey) => _view?.NotifyEffect(eventKey, false);
        public void EffectSpawn(string eventKey) => _view?.NotifyEffectSpawn(eventKey);
        public void Footstep() => _view?.NotifyFootstep();
        public void TargetingStart() => _view?.NotifyTargetingStart();

        public void HitStart() => _view?.NotifyHitStart();
        public void HitEnd() => _view?.NotifyHitEnd();

        // Attack6의 발사 처리는 추후 연결한다. 현재는 이벤트만 수신한다.
        public void Fire() { }
    }
}
