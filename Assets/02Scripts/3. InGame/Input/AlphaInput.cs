using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace alpha.ingame.input
{
    public class AlphaInput : MonoBehaviour
    {
        public static AlphaInput Instance { get; private set; }

        private AlphaInputAction _action;

        // Locomotion
        public Vector2 MoveInput => _moveInput;
        Vector2 _moveInput;

        public bool IsWalk => _isWalk;
        bool _isWalk;

        public bool IsJump => _jumpFrame == Time.frameCount;  // 한 프레임 단위만 True로 이후 False
        private int _jumpFrame = -1;

        public bool IsDodge => _dodgeFrame == Time.frameCount;
        private int _dodgeFrame = -1;

        // 기동: 눌림과 유지 입력은 아레테별로 다르게 해석한다.
        public bool IsMobilityPressed => _mobilityFrame == Time.frameCount;
        public bool IsMobilityHeld => _isMobilityHeld;
        private bool _isMobilityHeld;
        private int _mobilityFrame = -1;

        // 발사 방식에 따라 누름/유지/해제를 각각 사용한다.
        public bool IsPrimaryPressed => !_isQuickSlotOpen && _primaryPressedFrame == Time.frameCount;
        public bool IsPrimaryHeld => !_isQuickSlotOpen && _isPrimaryHeld;
        public bool IsPrimaryReleased => !_isQuickSlotOpen && _primaryReleasedFrame == Time.frameCount;
        private bool _isPrimaryHeld;
        private int _primaryPressedFrame = -1;
        private int _primaryReleasedFrame = -1;

        // Interaction
        public bool IsQuickSlotPressed => _quickSlotFrame == Time.frameCount;
        public bool IsQuickSlotOpen => _isQuickSlotOpen;
        private int _quickSlotFrame = -1;
        private bool _isQuickSlotOpen;

        // Camera
        public Vector2 LookInput => (_isQuickSlotOpen || _isMouseLock) ? Vector2.zero : _lookInput;
        private Vector2 _lookInput;

        public Vector2 ZoomInput => _zoomInput;
        private Vector2 _zoomInput;

        // Combat
        public bool IsSecondaryPressed => !_isQuickSlotOpen && _secondaryPressedFrame == Time.frameCount;
        public bool IsSecondaryHeld => !_isQuickSlotOpen && _isSecondaryHeld;
        public bool IsSecondaryReleased => !_isQuickSlotOpen && _secondaryReleasedFrame == Time.frameCount;
        private bool _isSecondaryHeld;
        private int _secondaryPressedFrame = -1;
        private int _secondaryReleasedFrame = -1;

        public int SkillNum => _skillNum;
        public int SkillPressedSlotThisFrame => _skillPressedFrame == Time.frameCount ? _skillPressedSlot : 0;
        private int _skillNum;
        private int _skillPressedSlot;
        private int _skillPressedFrame = -1;

        public bool IsReload => !_isQuickSlotOpen && _reloadFrame == Time.frameCount;
        private int _reloadFrame = -1;

        public bool IsAssistance => _assistanceFrame == Time.frameCount;
        private int _assistanceFrame = -1;

        // Mouse
        // 누르고 있는 동안 커서를 표시하고 잠금을 해제한다.
        public bool IsMouseLock => _isMouseLock;
        private bool _isMouseLock;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            _action = new AlphaInputAction();
            // 생성된 입력 래퍼가 갱신되기 전에도 Tab 바인딩을 적용한다.
            _action.PlayerInteraction.QuickSlot.ApplyBindingOverride("<Keyboard>/tab");

            // Locomotion
            _action.PlayerLocomotion.Move.performed += i => _moveInput = i.ReadValue<Vector2>();
            _action.PlayerLocomotion.Move.canceled += i => _moveInput = Vector2.zero;

            _action.PlayerLocomotion.Walk.performed += _ => _isWalk = !_isWalk;

            _action.PlayerLocomotion.Jump.performed += i => _jumpFrame = Time.frameCount;
            _action.PlayerLocomotion.Mobility.performed += OnMobilityPerformed;
            _action.PlayerLocomotion.Mobility.canceled += OnMobilityCanceled;
            _action.PlayerLocomotion.Dodge.performed += i => _dodgeFrame = Time.frameCount;

            // Combat
            _action.PlayerCombat.Primary.performed += OnPrimaryPerformed;
            _action.PlayerCombat.Primary.canceled += OnPrimaryCanceled;

            _action.PlayerCombat.Secondary.performed += OnSecondaryPerformed;
            _action.PlayerCombat.Secondary.canceled += OnSecondaryCanceled;

            _action.PlayerCombat.SkillNum.performed += OnSkillNumPerformed;
            _action.PlayerCombat.SkillNum.canceled += OnSkillNumCanceled;

            _action.PlayerCombat.Reload.performed += OnReloadPerformed;

            _action.PlayerCombat.Assistance.performed += i => _assistanceFrame = Time.frameCount;

            // Interaction
            _action.PlayerInteraction.QuickSlot.performed += OnQuickSlotPerformed;

            // Camera
            _action.Camera.Look.performed += i => _lookInput = i.ReadValue<Vector2>();
            _action.Camera.Look.canceled += i => _lookInput = Vector2.zero;

            _action.Camera.Zoom.performed += i => _zoomInput = i.ReadValue<Vector2>();
            _action.Camera.Zoom.canceled += i => _zoomInput = Vector2.zero;

            // Mouse
            _action.Mouse.Lock.performed += OnMouseLockPerformed;
            _action.Mouse.Lock.canceled += OnMouseLockCanceled;

            _action.Enable();
        }

        private void OnDisable()
        {
            _action.PlayerLocomotion.Move.performed -= i => _moveInput = i.ReadValue<Vector2>();
            _action.PlayerLocomotion.Move.canceled -= i => _moveInput = Vector2.zero;

            _action.PlayerLocomotion.Walk.performed -= i => _isWalk = !_isWalk;

            _action.PlayerLocomotion.Jump.performed -= i => _jumpFrame = Time.frameCount;

            _action.PlayerLocomotion.Mobility.performed -= OnMobilityPerformed;
            _action.PlayerLocomotion.Mobility.canceled -= OnMobilityCanceled;

            _action.PlayerLocomotion.Dodge.performed -= i => _dodgeFrame = Time.frameCount;

            // Interaction
            _action.PlayerInteraction.QuickSlot.performed -= OnQuickSlotPerformed;

            // Combat
            _action.PlayerCombat.Primary.performed -= OnPrimaryPerformed;
            _action.PlayerCombat.Primary.canceled -= OnPrimaryCanceled;

            _action.PlayerCombat.Secondary.performed -= OnSecondaryPerformed;
            _action.PlayerCombat.Secondary.canceled -= OnSecondaryCanceled;

            _action.PlayerCombat.SkillNum.performed -= OnSkillNumPerformed;
            _action.PlayerCombat.SkillNum.canceled -= OnSkillNumCanceled;

            _action.PlayerCombat.Reload.performed -= OnReloadPerformed;

            _action.PlayerCombat.Assistance.performed -= i => _assistanceFrame = Time.frameCount;

            // Camera
            _action.Camera.Look.performed -= i => _lookInput = i.ReadValue<Vector2>();
            _action.Camera.Look.canceled -= i => _lookInput = Vector2.zero;

            _action.Camera.Zoom.performed -= i => _zoomInput = i.ReadValue<Vector2>();
            _action.Camera.Zoom.canceled -= i => _zoomInput = Vector2.zero;


            // Mouse
            _action.Mouse.Lock.performed -= OnMouseLockPerformed;
            _action.Mouse.Lock.canceled -= OnMouseLockCanceled;

            _action.Disable();

            _action = null;
            _moveInput = Vector2.zero;
            _jumpFrame = -1;
            _dodgeFrame = -1;
            _isMobilityHeld = false;

            _isPrimaryHeld = false;
            _primaryPressedFrame = -1;
            _primaryReleasedFrame = -1;
            _isSecondaryHeld = false;
            _secondaryPressedFrame = -1;
            _secondaryReleasedFrame = -1;
            _skillNum = 0;
            _skillPressedSlot = 0;
            _skillPressedFrame = -1;
            _reloadFrame = -1;
            _assistanceFrame = -1;

            _isQuickSlotOpen = false;
            _quickSlotFrame = -1;
            _zoomInput = Vector2.zero;

            _isMouseLock = false;
        }

        private void OnMobilityPerformed(InputAction.CallbackContext context)
        {
            _mobilityFrame = Time.frameCount;
            _isMobilityHeld = true;
        }

        private void OnMobilityCanceled(InputAction.CallbackContext context)
        {
            _isMobilityHeld = false;
        }

        private void OnPrimaryPerformed(InputAction.CallbackContext context)
        {
            if (_isQuickSlotOpen) return;
            _isPrimaryHeld = true;
            _primaryPressedFrame = Time.frameCount;
        }

        private void OnPrimaryCanceled(InputAction.CallbackContext context)
        {
            if (_isPrimaryHeld && !_isQuickSlotOpen) _primaryReleasedFrame = Time.frameCount;
            _isPrimaryHeld = false;
        }

        private void OnSecondaryPerformed(InputAction.CallbackContext context)
        {
            if (_isQuickSlotOpen) return;
            _isSecondaryHeld = true;
            _secondaryPressedFrame = Time.frameCount;
        }

        private void OnSecondaryCanceled(InputAction.CallbackContext context)
        {
            if (_isSecondaryHeld && !_isQuickSlotOpen) _secondaryReleasedFrame = Time.frameCount;
            _isSecondaryHeld = false;
        }

        private void OnSkillNumPerformed(InputAction.CallbackContext context)
        {
            if (_isQuickSlotOpen || context.control is not KeyControl key) return;

            // Button 값은 모든 숫자 키에서 동일하므로 실제 눌린 키로 슬롯을 구분한다.
            _skillNum = key.keyCode switch
            {
                Key.Digit1 => 1,
                Key.Digit2 => 2,
                Key.Digit3 => 3,
                Key.Digit4 => 4,
                _ => 0
            };
            // 같은 프레임에 키를 떼어도 스킬 요청을 읽을 수 있게 누름을 보관한다.
            _skillPressedSlot = _skillNum;
            _skillPressedFrame = Time.frameCount;
        }

        private void OnSkillNumCanceled(InputAction.CallbackContext context)
        {
            _skillNum = 0;
        }

        private void OnReloadPerformed(InputAction.CallbackContext context)
        {
            if (!_isQuickSlotOpen) _reloadFrame = Time.frameCount;
        }

        private void OnQuickSlotPerformed(InputAction.CallbackContext context)
        {
            _quickSlotFrame = Time.frameCount;
        }

        private void OnMouseLockPerformed(InputAction.CallbackContext context)
        {
            _isMouseLock = true;
            _lookInput = Vector2.zero;
            MouseManger.Instance?.RefreshLockState();
        }

        private void OnMouseLockCanceled(InputAction.CallbackContext context)
        {
            _isMouseLock = false;
            MouseManger.Instance?.RefreshLockState();
        }

        public void SetQuickSlotOpen(bool isOpen)
        {
            _isQuickSlotOpen = isOpen;
            _lookInput = Vector2.zero;
            if (!isOpen) return;

            // 열리는 순간 이미 누르고 있던 공격과 마우스 회전량도 지운다.
            _isPrimaryHeld = false;
            _primaryPressedFrame = -1;
            _primaryReleasedFrame = -1;
            _isSecondaryHeld = false;
            _secondaryPressedFrame = -1;
            _secondaryReleasedFrame = -1;
            _skillNum = 0;
            _reloadFrame = -1;
        }

    }
}
