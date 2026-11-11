using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>월드에 생성된 빔포드 그룹의 포드와 추적·타겟팅을 시작한다.</summary>
    [RequireComponent(typeof(BeamPodFollow), typeof(BeamPodTargetModule))]
    public sealed class BeamPodSummon : MonoBehaviour
    {
        private SummonLifetime[] _pods;
        private RandomPointMove[] _randomMoves;
        private BeamPodTravel[] _travels;
        private BeamPodAttack[] _attacks;
        private Vector3[] _spawnLocalPositions;
        private Quaternion[] _spawnLocalRotations;
        private BeamPodFollow _follow;
        private BeamPodTargetModule _targetModule;
        private bool _started;

        public bool HasActivePods => _targetModule != null && _targetModule.IsActive;
        public bool IsAttacking => _targetModule != null && _targetModule.IsAttacking;
        public float ActiveProgress
        {
            get
            {
                if (!_started || _pods == null) return 0f;
                float progress = 0f;
                // 일부 포드가 파괴돼도 마지막 포드가 남은 시간까지 표시한다.
                foreach (SummonLifetime pod in _pods)
                    if (pod != null) progress = Mathf.Max(progress, pod.ActiveProgress);
                return progress;
            }
        }
        public EBeamPodCommandResult AdvanceCommand() => _targetModule != null
            ? _targetModule.AdvanceCommand() : EBeamPodCommandResult.None;

        private void Awake()
        {
            _pods = GetComponentsInChildren<SummonLifetime>(true);
            _randomMoves = new RandomPointMove[_pods.Length];
            _travels = new BeamPodTravel[_pods.Length];
            _attacks = new BeamPodAttack[_pods.Length];
            _spawnLocalPositions = new Vector3[_pods.Length];
            _spawnLocalRotations = new Quaternion[_pods.Length];

            // 배치된 포드의 시작 자세를 보관해 스킬을 다시 쓸 때 복원한다.
            for (int i = 0; i < _pods.Length; i++)
            {
                Transform podTransform = _pods[i].transform;
                _randomMoves[i] = _pods[i].GetComponent<RandomPointMove>();
                _travels[i] = _pods[i].GetComponent<BeamPodTravel>();
                _attacks[i] = _pods[i].GetComponent<BeamPodAttack>();
                _spawnLocalPositions[i] = podTransform.localPosition;
                _spawnLocalRotations[i] = podTransform.localRotation;
            }

            _follow = GetComponent<BeamPodFollow>();
            _targetModule = GetComponent<BeamPodTargetModule>();
        }

        public bool Activate(BeamPodSkillData skill, PlayerRoot player)
        {
            if (_targetModule == null || skill == null || player == null) return false;
            if (_pods == null || _pods.Length == 0)
            {
                Debug.LogError($"{nameof(BeamPodSummon)}: 빔포드에 {nameof(SummonLifetime)}을 연결하세요.", this);
                return false;
            }

            for (int i = 0; i < _pods.Length; i++)
            {
                if (_pods[i] != null && _randomMoves[i] != null &&
                    _travels[i] != null && _attacks[i] != null) continue;
                Debug.LogError($"{nameof(BeamPodSummon)}: 각 빔포드에 이동 및 공격 컴포넌트를 연결하세요.", this);
                return false;
            }

            // 모든 포드를 먼저 끄고 시작 자세로 되돌린다.
            foreach (SummonLifetime pod in _pods)
            {
                pod.gameObject.SetActive(false);
            }

            if (_follow == null || !_follow.Begin(player)) return false;

            // OnEnable이 각 포드의 생존 시간을 새로 시작한다.
            for (int i = 0; i < _pods.Length; i++)
            {
                SummonLifetime pod = _pods[i];
                pod.transform.SetLocalPositionAndRotation(_spawnLocalPositions[i], _spawnLocalRotations[i]);
                pod.gameObject.SetActive(true);
                _randomMoves[i].Begin(_spawnLocalPositions[i]);
                _travels[i].Begin(player.transform, _spawnLocalPositions[i]);
                if (_attacks[i].Begin(player.transform, skill.SightLayers, skill.TargetLayers,
                        player.CombatModule))
                    continue;
                foreach (SummonLifetime activePod in _pods) activePod.gameObject.SetActive(false);
                return false;
            }
            if (_targetModule.Begin(skill, player))
            {
                _started = true;
                return true;
            }
            foreach (SummonLifetime pod in _pods) pod.gameObject.SetActive(false);
            return false;
        }

        private void Update()
        {
            if (!_started) return;
            foreach (SummonLifetime pod in _pods)
                if (pod != null && pod.gameObject.activeInHierarchy && !pod.IsExpired) return;
            Destroy(gameObject);
        }

    }
}
