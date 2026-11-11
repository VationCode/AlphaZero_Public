using alpha.ingame.enemy;
using alpha.system.combat;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>소환된 MiniCrab의 플레이어 접촉을 폭발 표현으로 전환한다.</summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class MiniCrabContactView : MonoBehaviour
    {
        [SerializeField] private LayerMask _playerLayers = 1 << 7;
        [SerializeField] private Transform _model;
        [SerializeField] private Transform _explosion;

        private SphereCollider _trigger;
        private MiniCrabMotionModule _motion;
        private BossAttackInstance _attack;
        private bool _exploded;

        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
            _motion = GetComponentInParent<MiniCrabMotionModule>();
            _attack = GetComponentInParent<BossAttackInstance>();
            if (_model != null && _explosion != null) return;
            Debug.LogError($"{name}: MiniCrab 모델 또는 폭발 이펙트가 연결되지 않았습니다.", this);
            enabled = false;
        }

        private void Start()
        {
            if (!_exploded && _explosion != null)
                _explosion.gameObject.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_exploded || other == null || _motion == null ||
                _attack?.IsHitWindowActive != true) return;
            if (TryExplodeOnBarrier(other)) return;
            CharacterController player = other as CharacterController;
            if (player == null || !player.enabled ||
                (_playerLayers.value & (1 << player.gameObject.layer)) == 0) return;
            if (_motion.IsBeingPushed) return;

            // 접촉 이벤트 순서와 관계없이 쉴드 바깥의 MiniCrab은 내부 플레이어를 치지 못한다.
            if (ShieldBarrierRegistry.TryIntercept(_motion.ContactCenter,
                    player.bounds.center, 0f, out _)) return;

            Vector3 contactPoint = _trigger.ClosestPoint(player.bounds.center);
            Vector3 direction = EnemyHitDirection.FromContact(player,
                contactPoint, _trigger.bounds.center);
            _attack.TryHit(player, direction);
            Explode();
        }

        private void OnTriggerStay(Collider other)
        {
            if (_exploded || other == null || _motion == null ||
                _attack?.IsHitWindowActive != true) return;
            TryExplodeOnBarrier(other);
        }

        private bool TryExplodeOnBarrier(Collider other)
        {
            if (other.GetComponent(typeof(IShieldBarrier)) is not IShieldBarrier barrier)
                return false;

            // 접촉 자체를 판정한다. 내부에서 밀려 나가는 동안에는 폭발하지 않는다.
            float contactDistance = barrier.Radius + _motion.ContactRadius + 0.05f;
            if (barrier.IsActive && !_motion.IsBeingPushed &&
                (_motion.ContactCenter - barrier.Center).sqrMagnitude <=
                contactDistance * contactDistance)
                Explode();
            return true;
        }

        private void Explode()
        {
            _exploded = true;
            _motion?.Stop();
            _trigger.enabled = false; // 중복 폭발을 막는다.
            _model.gameObject.SetActive(false);
            _explosion.gameObject.SetActive(true);
            foreach (ParticleSystem particle in _explosion.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!particle.gameObject.activeInHierarchy) continue;
                particle.Clear();
                particle.Play();
            }
        }
    }
}
