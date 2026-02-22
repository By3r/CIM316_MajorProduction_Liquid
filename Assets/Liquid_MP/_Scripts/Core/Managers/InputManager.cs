using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Core.Managers
{
    /// <summary>
    /// Manages all player input and UI navigation through the new Unity Input System.
    /// Provides properties to read current input states and methods to enable/disable input maps or lock the cursor.
    /// Automatically pauses/resumes the game when the Pause action is triggered.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InputManager : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of the InputManager.
        /// </summary>
        public static InputManager Instance { get; private set; }

        /// <summary>
        /// The Input Action Asset that defines all player and UI input mappings.
        /// Must be assigned in the inspector for the InputManager to function.
        /// </summary>
        [SerializeField] public InputActionAsset InputActions;

        private InputActionMap _playerActionMap;
        private InputActionMap _uiActionMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;
        private InputAction _jumpAction;
        private InputAction _interactAction;
        private InputAction _fireAction;
        private InputAction _reloadAction;
        private InputAction _switchWeaponAction;
        private InputAction _pauseAction;
        private InputAction _navigateAction;
        private InputAction _submitAction;
        private InputAction _cancelAction;
        private InputAction _walkToggleAction;
        private InputAction _inventoryToggleAction;
        private InputAction _aimAction;

        private bool _isInitialized = false;

        public Vector2 MoveInput => _isInitialized && _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 LookInput => _isInitialized && _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
        public bool IsSprinting => _isInitialized && _sprintAction != null && _sprintAction.IsPressed();
        public bool CrouchPressed => _isInitialized && _crouchAction != null && _crouchAction.WasPressedThisFrame();
        public bool JumpPressed => _isInitialized && _jumpAction != null && _jumpAction.WasPressedThisFrame();
        public bool IsJumpHeld => _isInitialized && _jumpAction != null && _jumpAction.IsPressed();
        public bool JumpReleased => _isInitialized && _jumpAction != null && _jumpAction.WasReleasedThisFrame();
        public bool InteractPressed => _isInitialized && _interactAction != null && _interactAction.WasPressedThisFrame();
        public bool FirePressed => _isInitialized && _fireAction != null && _fireAction.IsPressed();
        public bool FireJustPressed => _isInitialized && _fireAction != null && _fireAction.WasPressedThisFrame();
        public bool ReloadPressed => _isInitialized && _reloadAction != null && _reloadAction.WasPressedThisFrame();
        public float SwitchWeaponInput => _isInitialized && _switchWeaponAction != null ? _switchWeaponAction.ReadValue<float>() : 0f;
        public bool PausePressed => _isInitialized && _pauseAction != null && _pauseAction.WasPressedThisFrame();
        public Vector2 NavigateInput => _isInitialized && _navigateAction != null ? _navigateAction.ReadValue<Vector2>() : Vector2.zero;
        public bool SubmitPressed => _isInitialized && _submitAction != null && _submitAction.WasPressedThisFrame();
        public bool CancelPressed => _isInitialized && _cancelAction != null && _cancelAction.WasPressedThisFrame();
        public bool WalkTogglePressed => _isInitialized && _walkToggleAction != null && _walkToggleAction.WasPressedThisFrame();
        public bool InventoryTogglePressed => _isInitialized && _inventoryToggleAction != null && _inventoryToggleAction.WasPressedThisFrame();
        public bool AimPressed => _isInitialized && _aimAction != null && _aimAction.IsPressed();
        public bool AimJustPressed => _isInitialized && _aimAction != null && _aimAction.WasPressedThisFrame();
        public bool AimJustReleased => _isInitialized && _aimAction != null && _aimAction.WasReleasedThisFrame();

        private void Awake()
        {
            InitializeSingleton();
        }

        private void Start()
        {
            InitializeInputActions();
        }



        private void InitializeSingleton()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void InitializeInputActions()
        {
            if (InputActions == null)
            {
                Debug.LogError("[InputManager] InputActionAsset is not assigned!");
                return;
            }

            _playerActionMap = InputActions.FindActionMap("Player");
            _uiActionMap = InputActions.FindActionMap("UI");

            _moveAction = _playerActionMap.FindAction("Move");
            _lookAction = _playerActionMap.FindAction("Look");
            _sprintAction = _playerActionMap.FindAction("Sprint");
            _crouchAction = _playerActionMap.FindAction("Crouch");
            _jumpAction = _playerActionMap.FindAction("Jump");
            _interactAction = _playerActionMap.FindAction("Interact");
            _fireAction = _playerActionMap.FindAction("Fire");
            _reloadAction = _playerActionMap.FindAction("Reload");
            _switchWeaponAction = _playerActionMap.FindAction("SwitchWeapon");
            _walkToggleAction = _playerActionMap.FindAction("WalkToggle");
            _inventoryToggleAction = _playerActionMap.FindAction("InventoryToggle");
            _aimAction = _playerActionMap.FindAction("Aim");

            _pauseAction = _uiActionMap.FindAction("Pause");
            _navigateAction = _uiActionMap.FindAction("Navigate");
            _submitAction = _uiActionMap.FindAction("Submit");
            _cancelAction = _uiActionMap.FindAction("Cancel");

            if (_pauseAction != null)
            {
                _pauseAction.performed += OnPausePerformed;
            }

            _isInitialized = true;
            EnablePlayerInput(true);
            EnableUIInput(true);
        }

        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            if (GameManager.Instance == null) return;

            // If debug console is open, close it instead of toggling pause
            var console = Systems.DebugConsole.DebugConsole.Instance;
            if (console != null && console.IsOpen)
            {
                console.Close();
                return;
            }

            // If inventory is open, let InventoryUI handle ESC (it closes inventory)
            // Don't toggle pause while inventory is open
            var inventoryUI = _Scripts.Systems.Inventory.UI.InventoryUI.Instance;
            if (inventoryUI != null && inventoryUI.IsOpen)
            {
                return;
            }

            if (GameManager.Instance.CurrentState == GameState.Gameplay)
            {
                GameManager.Instance.SetGameState(GameState.Paused);
            }
            else if (GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.SetGameState(GameState.Gameplay);
            }
        }

        public void EnablePlayerInput(bool enabled)
        {
            if (!_isInitialized) return;

            if (enabled) _playerActionMap?.Enable();
            else _playerActionMap?.Disable();
        }

        public void EnableUIInput(bool enabled)
        {
            if (!_isInitialized) return;

            if (enabled) _uiActionMap?.Enable();
            else _uiActionMap?.Disable();
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnDestroy()
        {
            if (_pauseAction != null)
            {
                _pauseAction.performed -= OnPausePerformed;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Gameplay)
            {
                LockCursor(true);
            }
        }
    }
}