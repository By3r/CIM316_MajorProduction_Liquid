using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Core
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

        private bool _isInitialized = false;

        /// <summary>
        /// Gets the current movement input as a Vector2 (typically WASD or analog stick).
        /// Returns Vector2.zero if input is not initialized.
        /// </summary>
        public Vector2 MoveInput => _isInitialized && _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        
        /// <summary>
        /// Gets the current camera look/aim input as a Vector2 (typically mouse delta or analog stick).
        /// Returns Vector2.zero if input is not initialized.
        /// </summary>
        public Vector2 LookInput => _isInitialized && _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
        
        /// <summary>
        /// Returns true while the sprint key/button is held down.
        /// </summary>
        public bool IsSprinting => _isInitialized && _sprintAction != null && _sprintAction.IsPressed();
        
        /// <summary>
        /// Returns true only on the frame the crouch key/button is pressed.
        /// </summary>
        public bool CrouchPressed => _isInitialized && _crouchAction != null && _crouchAction.WasPressedThisFrame();
        
        /// <summary>
        /// Returns true only on the frame the jump key/button is pressed.
        /// </summary>
        public bool JumpPressed => _isInitialized && _jumpAction != null && _jumpAction.WasPressedThisFrame();
        
        /// <summary>
        /// Returns true only on the frame the interact key/button is pressed.
        /// </summary>
        public bool InteractPressed => _isInitialized && _interactAction != null && _interactAction.WasPressedThisFrame();
        
        /// <summary>
        /// Returns true while the fire key/button is held down (for continuous fire).
        /// </summary>
        public bool FirePressed => _isInitialized && _fireAction != null && _fireAction.IsPressed();
        
        /// <summary>
        /// Returns true only on the frame the fire key/button is pressed (for single shot).
        /// </summary>
        public bool FireJustPressed => _isInitialized && _fireAction != null && _fireAction.WasPressedThisFrame();
        
        /// <summary>
        /// Returns true only on the frame the reload key/button is pressed.
        /// </summary>
        public bool ReloadPressed => _isInitialized && _reloadAction != null && _reloadAction.WasPressedThisFrame();
        
        /// <summary>
        /// Gets the weapon switch input as a float value (typically mouse scroll wheel).
        /// Returns 0f if input is not initialized.
        /// </summary>
        public float SwitchWeaponInput => _isInitialized && _switchWeaponAction != null ? _switchWeaponAction.ReadValue<float>() : 0f;
        
        /// <summary>
        /// Returns true only on the frame the pause key/button is pressed.
        /// </summary>
        public bool PausePressed => _isInitialized && _pauseAction != null && _pauseAction.WasPressedThisFrame();
        
        /// <summary>
        /// Gets the menu navigation input as a Vector2 (typically arrow keys or D-pad).
        /// Returns Vector2.zero if input is not initialized.
        /// </summary>
        public Vector2 NavigateInput => _isInitialized && _navigateAction != null ? _navigateAction.ReadValue<Vector2>() : Vector2.zero;
        
        /// <summary>
        /// Returns true only on the frame the submit/confirm key/button is pressed.
        /// </summary>
        public bool SubmitPressed => _isInitialized && _submitAction != null && _submitAction.WasPressedThisFrame();
        
        /// <summary>
        /// Returns true only on the frame the cancel/back key/button is pressed.
        /// </summary>
        public bool CancelPressed => _isInitialized && _cancelAction != null && _cancelAction.WasPressedThisFrame();
        
        /// <summary>
        /// Returns true only on the frame the walk toggle key/button is pressed.
        /// </summary>
        public bool WalkTogglePressed => _isInitialized && _walkToggleAction != null && _walkToggleAction.WasPressedThisFrame();

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

            if (GameManager.Instance.CurrentState == GameState.Gameplay)
            {
                GameManager.Instance.SetGameState(GameState.Paused);
            }
            else if (GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.SetGameState(GameState.Gameplay);
            }
        }

        /// <summary>
        /// Enables or disables the player action map, allowing or preventing player input.
        /// </summary>
        /// <param name="enabled">True to enable player input, false to disable.</param>
        public void EnablePlayerInput(bool enabled)
        {
            if (!_isInitialized) return;

            if (enabled) _playerActionMap?.Enable();
            else _playerActionMap?.Disable();
        }

        /// <summary>
        /// Enables or disables the UI action map, allowing or preventing menu navigation.
        /// </summary>
        /// <param name="enabled">True to enable UI input, false to disable.</param>
        public void EnableUIInput(bool enabled)
        {
            if (!_isInitialized) return;

            if (enabled) _uiActionMap?.Enable();
            else _uiActionMap?.Disable();
        }

        /// <summary>
        /// Locks or unlocks the cursor, and shows or hides it accordingly.
        /// When locked, the cursor is confined to the game window and hidden.
        /// </summary>
        /// <param name="locked">True to lock and hide the cursor, false to unlock and show.</param>
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