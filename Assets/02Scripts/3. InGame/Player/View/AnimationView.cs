using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace alpha.ingame.player
{
    public class AnimationView : MonoBehaviour
    {
        [Serializable]
        private struct AreteAnimatorController
        {
            [SerializeField] private EAreteType _areteType;
            [SerializeField] private RuntimeAnimatorController _baseController;

            public EAreteType AreteType => _areteType;
            public RuntimeAnimatorController BaseController => _baseController;
        }

        [Header("Ref")]
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private Animator _animator;
        [SerializeField] private AreteAnimatorController[] _areteControllers;

        [Header("Animator Layers")]
        [SerializeField] private string _baseLayerName = "Base Layer";
        [SerializeField] private string _reloadLayerName = "UpperArm";
        [SerializeField] private string _fireLayerName = "Upper";
        [SerializeField] private string _skillLayerName = "Full Body";

        [Header("Blend Time")]
        [SerializeField] private float _moveBlendTime = 0.12f;

        [Header("Footstep")]
        [SerializeField, Range(0f, 1f)] private float _firstFootstepPhase = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _secondFootstepPhase = 0.7f;

        [Header("Jump To Fall")]
        [SerializeField, Range(0f, 1f)] private float _jumpFallStart = 0.4f;
        [SerializeField, Range(0f, 1f)] private float _doubleJumpFallStart = 0.65f;

        [Header("Fall Crossfade")]
        [SerializeField, Min(0f)] private float _fallBlendTime = 0.1f;

        [Header("Silyon Aim Point")]
        [SerializeField, Min(1f)] private float _aimPointDistance = 30f;

        // 설정된 레이어 이름을 포함한 Animator 상태 해시
        private int _walk;
        private int _sprintTree;
        private int _boostTree;
        private int _boostCombatTree;
        private int _combatMoveTree;
        private int _jump;
        private int _doubleJump;
        private int _fall;
        private int _land;
        private int _staggeredHit;
        private int _knockBack;
        private int _knockDown;
        private int _knockDownLoop;
        private int _wakeUp;
        private int _reload;
        private int _reloadEmpty;
        private int _fire;
        private int _fireEmpty;
        private int _skillEmpty;
        private readonly int _skillCastSpeed = Animator.StringToHash("SkillCastSpeed");

        // Blend Tree 파라미터
        private readonly int _inputX = Animator.StringToHash("InputX");
        private readonly int _inputY = Animator.StringToHash("InputY");
        private readonly int _moveMagnitude = Animator.StringToHash("MoveMagnitude");

        private int _currentMoveState;
        private Rig[] _modelRigs = Array.Empty<Rig>();
        private CameraModule _cameraModule;
        private Transform _aimPoint;
        private bool _reloadAnimationActive;
        private bool _reloadAnimationEntered;
        private float _reloadFadeStartTime;
        private float _reloadFadeDuration;
        private bool _fireAnimationActive;
        private bool _fireAnimationEntered;
        private float _fireStartedAt;
        private bool _skillAnimationActive;
        private int _skillState;
        private bool _hitAnimationActive;
        private int _activeHitState;
        private bool _hitStateEntered;
        private EPlayerHitPhase? _deferredHitPhase;
        private int _groundMoveInputFrame = -1;
        private EMoveType _footstepMoveType;
        private bool _isFootstepCycleActive;
        private int _footstepStateHash;
        private float _previousFootstepPhase;

        public bool IsReloadActive => _reloadAnimationActive;
        public event Action<EMoveType> OnFootstep;
        public event Action<EPlayerHitType> HitAnimationStarted;

        private void OnEnable()
        {
            RefreshStateHashes();
            if (_animator != null) BindCameraView();
        }

#if UNITY_EDITOR
        private void OnValidate() => RefreshStateHashes();
#endif

        private void RefreshStateHashes()
        {
            _walk = Animator.StringToHash(_baseLayerName + ".Walk");
            _sprintTree = Animator.StringToHash(_baseLayerName + ".Sprint Tree");
            _boostTree = Animator.StringToHash(_baseLayerName + ".Boost Tree");
            _boostCombatTree = Animator.StringToHash(_baseLayerName + ".Boost Combat Tree");
            _combatMoveTree = Animator.StringToHash(_baseLayerName + ".CombatMove Tree");
            _jump = Animator.StringToHash(_baseLayerName + ".Jump");
            _doubleJump = Animator.StringToHash(_baseLayerName + ".Double Jump");
            _fall = Animator.StringToHash(_baseLayerName + ".Fall");
            _land = Animator.StringToHash(_baseLayerName + ".Land");
            _staggeredHit = Animator.StringToHash(_baseLayerName + ".StaggeredHit");
            _knockBack = Animator.StringToHash(_baseLayerName + ".KnockBack");
            _knockDown = Animator.StringToHash(_baseLayerName + ".KnockDown");
            _knockDownLoop = Animator.StringToHash(_baseLayerName + ".KnockDownLoop");
            _wakeUp = Animator.StringToHash(_baseLayerName + ".WakeUp");
            _reload = Animator.StringToHash(_reloadLayerName + ".Reload");
            _reloadEmpty = Animator.StringToHash(_reloadLayerName + ".Empty");
            _fire = Animator.StringToHash(_fireLayerName + ".R_Fire");
            _fireEmpty = Animator.StringToHash(_fireLayerName + ".Empty");
            _skillEmpty = Animator.StringToHash(_skillLayerName + ".Empty");
        }

        private int GetLayerIndex(string layerName) =>
            _animator != null && !string.IsNullOrWhiteSpace(layerName)
                ? _animator.GetLayerIndex(layerName) : -1;

        private void OnDisable()
        {
            ResetFootstepCycle();
            StopSkill();
            StopReload();
            StopFireRecoil();
            _hitAnimationActive = false;
            _activeHitState = 0;
            _hitStateEntered = false;
            _deferredHitPhase = null;
            if (_cameraModule != null)
                _cameraModule.OnViewChanged -= HandleCameraViewChanged;
            HandleCameraViewChanged(ECameraViewType.Default);
        }

        private void Start()
        {
            if (_characterRoot == null)
            {
                _characterRoot = GetComponentInParent<CharacterController>().transform;
            }
        }

        public void Initialize(Animator p_animator, EAreteType p_areteType)
        {
            if (p_animator == null)
            {
                Debug.LogWarning($"{nameof(AnimationView)}: {p_areteType} 모델에 Animator가 없습니다.", this);
                return;
            }

            // 생성된 모델의 Animator에 해당 아레테 컨트롤러를 적용한다.
            StopSkill();
            StopReload();
            StopFireRecoil();
            _hitAnimationActive = false;
            _activeHitState = 0;
            _hitStateEntered = false;
            _deferredHitPhase = null;
            _animator = p_animator;
            ResetFootstepCycle();
            RefreshStateHashes();
            PlayerRoot playerCore = GetComponentInParent<PlayerRoot>();
            _modelRigs = playerCore != null && playerCore.CurrentModel != null
                ? playerCore.CurrentModel.GetComponentsInChildren<Rig>(true)
                : Array.Empty<Rig>();
            _aimPoint = playerCore != null && playerCore.CurrentModel != null
                ? playerCore.CurrentModel.transform.Find("AimPoint")
                : null;
            BindCameraView();
            HandlePlayerSetup(p_areteType);
        }

        private void LateUpdate()
        {
            CompleteDeferredHitPhase();
            UpdateFootstepCycle();
            // 프리팹에 배치된 AimPoint만 카메라 조준 방향으로 옮긴다.
            if (_aimPoint == null || _cameraModule == null || _cameraModule.Cam == null) return;
            Transform cameraTransform = alpha.ingame.camera.CameraAim.StableTransform(_cameraModule.Cam);
            _aimPoint.position = cameraTransform.position + cameraTransform.forward * _aimPointDistance;
        }

        private void Update()
        {
            UpdateFireRecoil();
            if (_reloadFadeDuration <= 0f) return;
            if (_animator == null || !_animator.isActiveAndEnabled)
            {
                CompleteStopReload();
                return;
            }

            int layer = GetLayerIndex(_reloadLayerName);
            if (layer < 0)
            {
                CompleteStopReload();
                return;
            }

            float progress = Mathf.Clamp01((Time.time - _reloadFadeStartTime) / _reloadFadeDuration);
            float blend = Mathf.SmoothStep(0f, 1f, progress);
            _animator.SetLayerWeight(layer, 1f - blend);
            if (progress >= 1f)
                CompleteStopReload();
            else
                HandleCameraViewChanged(_cameraModule != null
                    ? _cameraModule.ActiveViewType : ECameraViewType.Default);
        }

        private void BindCameraView()
        {
            if (_cameraModule != null)
                _cameraModule.OnViewChanged -= HandleCameraViewChanged;

            _cameraModule = FindFirstObjectByType<CameraModule>();
            if (_cameraModule != null)
                _cameraModule.OnViewChanged += HandleCameraViewChanged;

            HandleCameraViewChanged(_cameraModule != null
                ? _cameraModule.ActiveViewType : ECameraViewType.Default);
        }

        private void HandleCameraViewChanged(ECameraViewType viewType)
        {
            // 재장전 레이어가 사라지는 만큼 조준 리깅을 함께 복귀시킨다.
            float weight = 0f;
            if (viewType == ECameraViewType.Aim && !_skillAnimationActive && !_hitAnimationActive)
            {
                if (!_reloadAnimationActive && !_fireAnimationActive)
                    weight = 1f;
                else if (_reloadFadeDuration > 0f)
                {
                    float progress = Mathf.Clamp01((Time.time - _reloadFadeStartTime) / _reloadFadeDuration);
                    weight = Mathf.SmoothStep(0f, 1f, progress);
                }
            }
            foreach (Rig rig in _modelRigs)
                if (rig != null) rig.weight = weight;
        }

        public bool PlayReload()
        {
            StopFireRecoil();
            if (_animator == null || !_animator.isActiveAndEnabled) return false;

            int layer = GetLayerIndex(_reloadLayerName);
            if (layer < 0 || !_animator.HasState(layer, _reload)) return false;

            // UpperArm 레이어의 Reload 상태를 처음부터 재생한다.
            _reloadFadeDuration = 0f;
            _reloadAnimationActive = true;
            _reloadAnimationEntered = false;
            _animator.SetLayerWeight(layer, 1f);
            _animator.CrossFadeInFixedTime(_reload, 0.05f, layer, 0f);
            HandleCameraViewChanged(_cameraModule != null
                ? _cameraModule.ActiveViewType : ECameraViewType.Default);
            return true;
        }

        // End 이벤트가 누락됐을 때 전체 클립 종료 여부만 확인한다.
        public bool IsReloadFinished()
        {
            if (!_reloadAnimationActive || _animator == null || !_animator.isActiveAndEnabled)
                return true;

            int layer = GetLayerIndex(_reloadLayerName);
            if (layer < 0) return true;

            if (_animator.IsInTransition(layer))
            {
                if (_animator.GetNextAnimatorStateInfo(layer).fullPathHash == _reload)
                    _reloadAnimationEntered = true;
                return false;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(layer);
            if (state.fullPathHash == _reload)
            {
                _reloadAnimationEntered = true;
                return state.normalizedTime >= 1f;
            }

            return _reloadAnimationEntered;
        }

        public void StopReload(float blendOutDuration = 0f)
        {
            if (!_reloadAnimationActive) return;
            if (blendOutDuration > 0f && _animator != null && _animator.isActiveAndEnabled &&
                GetLayerIndex(_reloadLayerName) >= 0)
            {
                // 완료 지점부터 UpperArm 레이어만 서서히 줄인다.
                _reloadFadeStartTime = Time.time;
                _reloadFadeDuration = blendOutDuration;
                return;
            }

            CompleteStopReload();
        }

        private void CompleteStopReload()
        {
            _reloadAnimationActive = false;
            _reloadAnimationEntered = false;
            _reloadFadeDuration = 0f;

            if (_animator != null)
            {
                int layer = GetLayerIndex(_reloadLayerName);
                if (layer >= 0)
                {
                    _animator.SetLayerWeight(layer, 0f);
                    if (_animator.isActiveAndEnabled && _animator.HasState(layer, _reloadEmpty))
                        _animator.Play(_reloadEmpty, layer, 0f);
                }
            }

            HandleCameraViewChanged(_cameraModule != null
                ? _cameraModule.ActiveViewType : ECameraViewType.Default);
        }

        public void PlayFireRecoil()
        {
            if (_animator == null || !_animator.isActiveAndEnabled ||
                _reloadAnimationActive || _skillAnimationActive) return;

            int layer = GetLayerIndex(_fireLayerName);
            if (layer < 0 || !_animator.HasState(layer, _fire)) return;

            // 연사 중에도 실제 발사마다 반동 클립을 첫 프레임부터 다시 재생한다.
            _fireAnimationActive = true;
            _fireAnimationEntered = false;
            _fireStartedAt = Time.time;
            _animator.SetLayerWeight(layer, 1f);
            _animator.Play(_fire, layer, 0f);
            HandleCameraViewChanged(_cameraModule != null
                ? _cameraModule.ActiveViewType : ECameraViewType.Default);
        }

        private void UpdateFireRecoil()
        {
            if (!_fireAnimationActive) return;
            if (_animator == null || !_animator.isActiveAndEnabled)
            {
                StopFireRecoil();
                return;
            }

            int layer = GetLayerIndex(_fireLayerName);
            if (layer < 0)
            {
                StopFireRecoil();
                return;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(layer);
            if (state.fullPathHash == _fire)
            {
                _fireAnimationEntered = true;
                if (state.normalizedTime < 1f) return;
            }
            else if (!_fireAnimationEntered && Time.time - _fireStartedAt < 1f)
                return;

            StopFireRecoil();
        }

        public void StopFireRecoil()
        {
            _fireAnimationActive = false;
            _fireAnimationEntered = false;

            if (_animator != null)
            {
                int layer = GetLayerIndex(_fireLayerName);
                if (layer >= 0)
                {
                    _animator.SetLayerWeight(layer, 0f);
                    if (_animator.isActiveAndEnabled && _animator.HasState(layer, _fireEmpty))
                        _animator.Play(_fireEmpty, layer, 0f);
                }
            }

            HandleCameraViewChanged(_cameraModule != null
                ? _cameraModule.ActiveViewType : ECameraViewType.Default);
        }

        public bool PlaySkill(string stateName, AnimationClip clip, float castDuration)
        {
            StopFireRecoil();
            if (string.IsNullOrWhiteSpace(stateName) || clip == null ||
                castDuration <= 0f || _animator == null ||
                !_animator.isActiveAndEnabled) return false;

            int layer = GetLayerIndex(_skillLayerName);
            int state = Animator.StringToHash(_skillLayerName + "." + stateName);
            if (layer < 0 || !_animator.HasState(layer, state)) return false;

            StopReload();
            _skillState = state;
            _skillAnimationActive = true;
            // 지정한 시전 시간에 클립이 한 번 재생되도록 이 상태의 속도만 맞춘다.
            _animator.SetFloat(_skillCastSpeed, clip.length / castDuration);
            _animator.SetLayerWeight(layer, 1f);
            _animator.CrossFadeInFixedTime(state, 0.05f, layer, 0f);
            HandleCameraViewChanged(_cameraModule != null
                ? _cameraModule.ActiveViewType : ECameraViewType.Default);
            return true;
        }

        public bool TryGetSkillProgress(out float normalizedTime)
        {
            normalizedTime = 0f;
            if (!_skillAnimationActive || _animator == null || !_animator.isActiveAndEnabled)
                return false;

            int layer = GetLayerIndex(_skillLayerName);
            if (layer < 0 || _animator.IsInTransition(layer)) return false;

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(layer);
            if (state.fullPathHash != _skillState) return false;

            normalizedTime = state.normalizedTime;
            return true;
        }

        public void StopSkill()
        {
            if (!_skillAnimationActive) return;
            _skillAnimationActive = false;
            _skillState = 0;

            if (_animator != null)
            {
                _animator.SetFloat(_skillCastSpeed, 1f);
                int layer = GetLayerIndex(_skillLayerName);
                if (layer >= 0)
                {
                    _animator.SetLayerWeight(layer, 0f);
                    if (_animator.isActiveAndEnabled && _animator.HasState(layer, _skillEmpty))
                        _animator.Play(_skillEmpty, layer, 0f);
                }
            }

            HandleCameraViewChanged(_cameraModule != null
                ? _cameraModule.ActiveViewType : ECameraViewType.Default);
        }

        public void HandlePlayerSetup(EAreteType p_type)
        {
            if (_animator == null || _areteControllers == null)
                return;

            // 아레테 타입으로 기본 컨트롤러를 찾아 생성된 모델의 Animator에 적용한다.
            foreach (AreteAnimatorController entry in _areteControllers)
            {
                if (entry.AreteType != p_type)
                    continue;

                if (entry.BaseController == null)
                    break;

                _animator.runtimeAnimatorController = entry.BaseController;
                _currentMoveState = 0;
                return;
            }

            Debug.LogWarning($"{nameof(AnimationView)}: {p_type} 기본 Animator Controller가 없습니다.", this);
        }

        // ActionStateFlow의 피격 단계 변화만 받아 Animator 상태를 표현한다.
        public void PlayHitPhase(EPlayerHitPhase phase)
        {
            // 일반피격은 체력·무적 판정만 적용하고 Animator는 건드리지 않는다.
            if (phase == EPlayerHitPhase.Normal ||
                (phase == EPlayerHitPhase.None && !_hitAnimationActive)) return;

            if (_animator == null || !_animator.isActiveAndEnabled ||
                _animator.runtimeAnimatorController == null) return;

            int layer = GetLayerIndex(_baseLayerName);
            if (layer < 0) return;

            // 액션 타이머가 먼저 끝나도 일회성 피격 클립은 마지막 프레임까지 재생한다.
            if (_activeHitState != 0 &&
                (phase == EPlayerHitPhase.None || phase == EPlayerHitPhase.Lying ||
                 phase == EPlayerHitPhase.Standing) &&
                !IsActiveHitClipFinished(layer))
            {
                _deferredHitPhase = phase;
                return;
            }
            _deferredHitPhase = null;

            if (phase == EPlayerHitPhase.None)
            {
                _hitAnimationActive = false;
                _activeHitState = 0;
                _hitStateEntered = false;
                _currentMoveState = 0;
                // 공중 피격 후에는 Fall 진입 이벤트가 다시 오지 않으므로 낙하 클립으로 복귀한다.
                bool airborne = GetComponentInParent<PlayerRoot>()?.LocomotionModule?.IsGrounded == false;
                if (airborne && _animator.HasState(layer, _fall))
                    _animator.CrossFadeInFixedTime(_fall, _moveBlendTime, layer, 0f);
                // 지상 이동 클립은 다음 Locomotion 갱신의 입력으로 선택한다.
                HandleCameraViewChanged(_cameraModule != null
                    ? _cameraModule.ActiveViewType : ECameraViewType.Default);
                return;
            }

            if (phase == EPlayerHitPhase.Dead)
            {
                _hitAnimationActive = true;
                _activeHitState = 0;
                StopSkill();
                StopReload();
                StopFireRecoil();
                return;
            }

            int state = phase switch
            {
                EPlayerHitPhase.Stagger => _staggeredHit,
                EPlayerHitPhase.Knockback => _knockBack,
                EPlayerHitPhase.Knockdown => _knockDown,
                EPlayerHitPhase.Lying => _knockDownLoop,
                EPlayerHitPhase.Standing => _wakeUp,
                _ => 0
            };
            if (state == 0 || !_animator.HasState(layer, state)) return;

            _hitAnimationActive = true;
            _activeHitState = phase == EPlayerHitPhase.Lying ? 0 : state;
            _hitStateEntered = false;
            _currentMoveState = 0;
            StopSkill();
            StopReload();
            StopFireRecoil();
            _animator.CrossFadeInFixedTime(state, 0.05f, layer, 0f);
            // 실제 피격 클립으로 전환할 때만 해당 타입의 소리를 요청한다.
            if (phase is EPlayerHitPhase.Stagger or EPlayerHitPhase.Knockback or
                EPlayerHitPhase.Knockdown)
                HitAnimationStarted?.Invoke(phase switch
                {
                    EPlayerHitPhase.Stagger => EPlayerHitType.Stagger,
                    EPlayerHitPhase.Knockback => EPlayerHitType.Knockback,
                    _ => EPlayerHitType.Knockdown
                });
            HandleCameraViewChanged(_cameraModule != null
                ? _cameraModule.ActiveViewType : ECameraViewType.Default);
        }

        private void CompleteDeferredHitPhase()
        {
            if (!_deferredHitPhase.HasValue || _animator == null ||
                !_animator.isActiveAndEnabled) return;
            int layer = GetLayerIndex(_baseLayerName);
            if (layer < 0 || !IsActiveHitClipFinished(layer)) return;
            EPlayerHitPhase phase = _deferredHitPhase.Value;
            _deferredHitPhase = null;
            PlayHitPhase(phase);
        }

        private bool IsActiveHitClipFinished(int layer)
        {
            if (_activeHitState == 0) return true;
            if (_animator.IsInTransition(layer))
            {
                if (_animator.GetNextAnimatorStateInfo(layer).fullPathHash == _activeHitState)
                    _hitStateEntered = true;
                return false;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(layer);
            if (state.fullPathHash == _activeHitState)
            {
                _hitStateEntered = true;
                return state.normalizedTime >= 1f;
            }
            return _hitStateEntered;
        }

        public void MoveAnim(Vector3 p_moveDirection, EMoveType p_moveType, float p_inputStrength)
        {
            if (_hitAnimationActive || _animator == null || !_animator.isActiveAndEnabled ||
                _animator.runtimeAnimatorController == null)
                return;

            int layer = GetLayerIndex(_baseLayerName);
            if (layer < 0) return;

            float strength = Mathf.Clamp01(p_inputStrength);
            _groundMoveInputFrame = strength > 0.1f &&
                p_moveType is EMoveType.Walk or EMoveType.Run or EMoveType.Combat
                ? Time.frameCount : -1;
            _footstepMoveType = p_moveType;
            // 걷기 전환은 이동 중에만 적용하고 전투 이동을 우선한다.
            int nextState = p_moveType switch
            {
                EMoveType.Combat => _combatMoveTree,
                EMoveType.Boost => _boostTree,
                EMoveType.BoostCombat => _boostCombatTree,
                EMoveType.Walk when strength > 0f => _walk,
                _ => _sprintTree
            };

            // 월드 방향을 캐릭터 기준으로 바꾸고 입력 세기를 반영한다.
            Vector3 localDirection =
                _characterRoot != null ? _characterRoot.InverseTransformDirection(p_moveDirection) : p_moveDirection;
            localDirection.y = 0f;
            localDirection.Normalize();

            _animator.SetFloat(_inputX, localDirection.x * strength, 0.1f, Time.deltaTime);
            _animator.SetFloat(_inputY, localDirection.z * strength, 0.1f, Time.deltaTime);
            _animator.SetFloat(_moveMagnitude, strength, 0.1f, Time.deltaTime);

            if (_currentMoveState == nextState)
                return;

            _currentMoveState = nextState;
            _animator.CrossFadeInFixedTime(nextState, _moveBlendTime, layer, 0f);
        }

        // 입력 중인 지상 이동 애니메이션의 양발 접촉 시점을 알린다.
        private void UpdateFootstepCycle()
        {
            if (_groundMoveInputFrame != Time.frameCount || _hitAnimationActive ||
                _animator == null || !_animator.isActiveAndEnabled ||
                _animator.runtimeAnimatorController == null)
            {
                ResetFootstepCycle();
                return;
            }

            int layer = GetLayerIndex(_baseLayerName);
            if (layer < 0)
            {
                ResetFootstepCycle();
                return;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(layer);
            int stateHash = state.fullPathHash;
            if (stateHash != _walk && stateHash != _sprintTree &&
                stateHash != _combatMoveTree)
            {
                ResetFootstepCycle();
                return;
            }

            float phase = Mathf.Repeat(state.normalizedTime, 1f);
            if (!_isFootstepCycleActive || _footstepStateHash != stateHash)
            {
                _isFootstepCycleActive = true;
                _footstepStateHash = stateHash;
                _previousFootstepPhase = phase;
                return;
            }

            if (HasCrossedFootstepPhase(_previousFootstepPhase, phase,
                    _firstFootstepPhase))
                OnFootstep?.Invoke(_footstepMoveType);
            if (HasCrossedFootstepPhase(_previousFootstepPhase, phase,
                    _secondFootstepPhase))
                OnFootstep?.Invoke(_footstepMoveType);
            _previousFootstepPhase = phase;
        }

        private static bool HasCrossedFootstepPhase(float previous, float current,
            float target) => current >= previous
            ? target > previous && target <= current
            : target > previous || target <= current;

        private void ResetFootstepCycle()
        {
            _groundMoveInputFrame = -1;
            _isFootstepCycleActive = false;
            _footstepStateHash = 0;
            _previousFootstepPhase = 0f;
        }

        public void JumpAnim(int p_jumpCount)
        {
            if (_hitAnimationActive || _animator == null || !_animator.isActiveAndEnabled) return;

            int layer = GetLayerIndex(_baseLayerName);
            int state = p_jumpCount == 2 ? _doubleJump : _jump;
            if (layer < 0 || !_animator.HasState(layer, state)) return;

            _currentMoveState = 0;
            _animator.CrossFadeInFixedTime(state, 0.08f, layer, 0f); // 점프 시작부터 재생
        }

        public bool CanEnterFallFromJump()
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return true;
            int layer = GetLayerIndex(_baseLayerName);
            if (layer < 0) return true;
            if (_animator.IsInTransition(layer)) return false;

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(layer);
            if (state.fullPathHash == _jump)
                return state.normalizedTime >= _jumpFallStart;
            if (state.fullPathHash == _doubleJump)
                return state.normalizedTime >= _doubleJumpFallStart;

            return true;
        }


        public void FallAnim()
        {
            if (_hitAnimationActive || _animator == null || !_animator.isActiveAndEnabled) return;
            int layer = GetLayerIndex(_baseLayerName);
            if (layer < 0 || !_animator.HasState(layer, _fall)) return;

            _currentMoveState = 0;

            _animator.CrossFadeInFixedTime(_fall, _fallBlendTime, layer, 0f); // 별도 Fall 클립을 처음부터 짧게 섞는다.
        }

        public void LandAnim()
        {
            if (_hitAnimationActive || _animator == null || !_animator.isActiveAndEnabled) return;
            int layer = GetLayerIndex(_baseLayerName);
            if (layer < 0 || !_animator.HasState(layer, _land)) return;

            _currentMoveState = 0;
            _animator.CrossFadeInFixedTime(_land, 0.174f, layer, 0.1416163f);
        }

        public bool IsLandAnimFinished()
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return true;
            int layer = GetLayerIndex(_baseLayerName);
            if (layer < 0 || !_animator.HasState(layer, _land)) return true;

            if (_animator.IsInTransition(layer))
                return false;

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(layer);
            return state.fullPathHash == _land && state.normalizedTime >= 1f;
        }

        private void OnAnimatorMove()
        {
            // 이동과 회전은 LocomotionModule이 처리한다.
        }

    }
}
