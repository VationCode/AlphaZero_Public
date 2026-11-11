using System;
using alpha.ingame.item;
using alpha.ingame.skill;
using UnityEngine;

namespace alpha.ingame.player
{
    [RequireComponent(typeof(PlayerMotionModule))]
    public class PlayerRoot : MonoBehaviour, IPlayerHitReceiver, IPlayerAttractionReceiver
    {
        [Header("Model")]
        [SerializeField] private Transform _modelRoot;
        [SerializeField] private GameObject _hernaPrefab;
        [SerializeField] private GameObject _arinPrefab;
        [SerializeField] private GameObject _silyonPrefab;

        public GameObject CurrentModel { get; private set; }
        public Arete CurrentArete { get; private set; }
        public EAreteType CurrentAreteType { get; private set; }
        public AnimationView AnimationView { get; private set; }
        private PlayerAudioModule _audioModule;
        
        public LocomotionStateFlow LocomotionStateFlow { get; private set; }
        public CombatStateFlow CombatStateFlow { get; private set; }
        public CombatActionFlow CombatActionFlow => CombatStateFlow?.Action;
        public CombatStanceFlow CombatStanceFlow => CombatStateFlow?.Stance;
        public SkillStateFlow SkillStateFlow { get; private set; }
        public SkillModule SkillModule { get; private set; }
        public SkillDisplayChannel SkillDisplayChannel { get; } = new SkillDisplayChannel();
        public AmmoDisplayChannel AmmoDisplayChannel { get; } = new AmmoDisplayChannel();


        public LocomotionModule LocomotionModule { get; private set; }
        public PlayerMotionModule MotionModule { get; private set; }
        public CombatModule CombatModule { get; private set; }
        public EquipmentModule EquipmentModule { get; private set; }
        public InventoryModule InventoryModule { get; private set; }
        public PlayerHealthModule HealthModule { get; private set; }
        public PlayerHitModule HitModule { get; private set; }
        public PlayerActionStateFlow ActionStateFlow { get; private set; }
        private PlayerFootstepAudio _footstepAudio;

        // 공격 판정은 Player의 대표 진입점으로 피격 정보를 전달한다.
        public bool TryReceiveHit(in PlayerHitInfo hit) => ActionStateFlow != null &&
            ActionStateFlow.TryReceiveHit(hit);

        // 외부 인력은 대표 진입점에서 이동 Feature로 전달한다.
        public void SetAttraction(Transform source, Vector3 velocity)
        {
            if (HealthModule?.IsDead == true)
            {
                MotionModule?.ClearAttraction(source);
                return;
            }
            MotionModule?.SetAttraction(source, velocity);
        }

        public void ClearAttraction(Transform source) =>
            MotionModule?.ClearAttraction(source);

        // 접촉 판정자는 몸체만 전달하고, 실제 겹침과 이격 거리는 Player가 계산한다.
        public bool TryGetContactClearanceDistance(Collider[] bodies, Vector3 direction,
            float padding, out float distance)
        {
            distance = 0f;
            return MotionModule != null &&
                MotionModule.TryGetContactClearanceDistance(bodies, direction,
                    padding, out distance);
        }

        public bool TryClearContact(Collider[] bodies, Vector3 direction, float maxStep) =>
            MotionModule?.TryClearContact(bodies, direction, maxStep) == true;


        public event Action<EAreteType> OnPlayerSetup;

        private void Awake()
        {
            if (_modelRoot == null) _modelRoot = transform.Find("Model");

            LocomotionStateFlow = GetComponentInChildren<LocomotionStateFlow>(true);
            LocomotionModule = GetComponentInChildren<LocomotionModule>(true);
            MotionModule = GetComponent<PlayerMotionModule>();
            CombatStateFlow = GetComponentInChildren<CombatStateFlow>(true);
            CombatModule = GetComponentInChildren<CombatModule>(true);
            SkillStateFlow = GetComponentInChildren<SkillStateFlow>(true);
            SkillModule = GetComponentInChildren<SkillModule>(true);

            EquipmentModule = GetComponentInChildren<EquipmentModule>(true);
            InventoryModule = GetComponentInChildren<InventoryModule>(true);
            HealthModule = GetComponentInChildren<PlayerHealthModule>(true);
            HitModule = GetComponentInChildren<PlayerHitModule>(true);
            ActionStateFlow = GetComponentInChildren<PlayerActionStateFlow>(true);
            _footstepAudio = GetComponentInChildren<PlayerFootstepAudio>(true);

            AnimationView = GetComponentInChildren<AnimationView>(true);
        }


        private void Start()
        {
            if (GameManager.Instance == null) return;

            EAreteType areteType = GameManager.Instance.AreteType;
            CurrentAreteType = areteType;
            if (!CreateModel(areteType)) return;

            if (AnimationView != null)
            {
                // 모델 생성 후 Animator와 아레테 컨트롤러를 함께 설정한다.
                AnimationView.Initialize(CurrentModel.GetComponentInChildren<Animator>(true), areteType);
                if (ActionStateFlow != null)
                    ActionStateFlow.PhaseChanged += AnimationView.PlayHitPhase;
            }

            InventoryModule?.InitializeStartingWeapons(areteType);
            EquipmentModule?.Initialize(areteType, CurrentModel, InventoryModule);
            OnPlayerSetup?.Invoke(areteType);

            if (LocomotionModule != null && AnimationView != null)
            {
                LocomotionModule.OnMove += AnimationView.MoveAnim;
                LocomotionModule.OnJump += AnimationView.JumpAnim;
            }
            if (AnimationView != null && _footstepAudio != null)
                AnimationView.OnFootstep += _footstepAudio.PlayFootstep;
            if (CombatModule != null && AnimationView != null)
                CombatModule.ShotFired += AnimationView.PlayFireRecoil;
        }

        private void OnDestroy()
        {
            if (AnimationView != null && _audioModule != null)
                AnimationView.HitAnimationStarted -= _audioModule.PlayHit;
            if (CombatModule != null && _audioModule != null)
                CombatModule.CriticalHitApplied -= _audioModule.PlayCriticalHit;
            if (AnimationView != null && _footstepAudio != null)
                AnimationView.OnFootstep -= _footstepAudio.PlayFootstep;
            if (ActionStateFlow != null && AnimationView != null)
                ActionStateFlow.PhaseChanged -= AnimationView.PlayHitPhase;
            if (AnimationView == null) return;
            if (LocomotionModule != null)
            {
                LocomotionModule.OnMove -= AnimationView.MoveAnim;
                LocomotionModule.OnJump -= AnimationView.JumpAnim;
            }
            if (CombatModule != null)
                CombatModule.ShotFired -= AnimationView.PlayFireRecoil;
        }

        private bool CreateModel(EAreteType p_areteType)
        {
            // 선택한 아레테 모델 하나만 Player/Model 아래에 배치한다.
            GameObject prefab = p_areteType switch
            {
                EAreteType.Heruna => _hernaPrefab,
                EAreteType.Arin => _arinPrefab,
                EAreteType.Silyon => _silyonPrefab,
                _ => null
            };

            if (_modelRoot == null || prefab == null)
            {
                Debug.LogWarning($"{nameof(PlayerRoot)}: Model 또는 {p_areteType} 프리팹 참조가 없습니다.", this);
                return false;
            }

            // 씬의 편집용 모델이 있으면 런타임 모델과 겹치지 않게 제거한다.
            for (int i = _modelRoot.childCount - 1; i >= 0; i--)
            {
                GameObject existingModel = _modelRoot.GetChild(i).gameObject;
                existingModel.SetActive(false);
                Destroy(existingModel);
            }

            CurrentModel = Instantiate(prefab, _modelRoot);
            CurrentModel.transform.localPosition = Vector3.zero;
            CurrentModel.transform.localRotation = Quaternion.identity;
            CurrentModel.transform.localScale = Vector3.one;

            CurrentArete = CurrentModel.GetComponent<Arete>();
            if (CurrentArete == null || CurrentArete.AreteType != p_areteType)
            {
                Debug.LogError($"{nameof(PlayerRoot)}: {p_areteType} 프리팹 루트에 올바른 Arete 컴포넌트가 없습니다.", this);
                Destroy(CurrentModel);
                CurrentModel = null;
                CurrentArete = null;
                return false;
            }

            // 모델이 생성된 뒤 플레이어 Audios의 채널을 모델 이벤트에 전달한다.
            Transform audioRoot = transform.Find("Audios");
            if (audioRoot == null)
            {
                GameObject audioObject = new GameObject("Audios");
                audioObject.transform.SetParent(transform, false);
                audioRoot = audioObject.transform;
            }
            PlayerAudioModule audioModule = audioRoot.GetComponent<PlayerAudioModule>();
            if (audioModule == null)
                audioModule = audioRoot.gameObject.AddComponent<PlayerAudioModule>();
            audioModule.BindModel(CurrentModel);
            if (AnimationView != null && _audioModule != null)
                AnimationView.HitAnimationStarted -= _audioModule.PlayHit;
            if (CombatModule != null && _audioModule != null)
                CombatModule.CriticalHitApplied -= _audioModule.PlayCriticalHit;
            _audioModule = audioModule;
            if (AnimationView != null)
                AnimationView.HitAnimationStarted += _audioModule.PlayHit;
            if (CombatModule != null)
                CombatModule.CriticalHitApplied += _audioModule.PlayCriticalHit;

            CurrentArete.Initialize(this);
            return true;
        }

    }
}
