using System.Collections;
using _Scripts.Core.Managers;
using UnityEngine;

namespace _Scripts.Systems.Player
{
    /// <summary>
    /// Handles player movement including walking, sprinting, crouching, jumping, and gravity.
    /// Manages the CharacterController and calculates movement based on input and player settings.
    /// Exposes movement state properties for other systems (camera effects, animations, etc.).
    /// Integrates with Neutronic Boots for ceiling walking physics override.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MovementController : MonoBehaviour
    {
        #region Private Fields
        
        private CharacterController _characterController;
        private PlayerSettings _settings;

        private Vector2 _moveInput;
        private Vector3 _velocity;
        private bool _isGrounded;
        private bool _isSprinting;
        private bool _isCrouching;
        private bool _isJumping;
        private float _originalHeight;
        private Vector3 _originalCenter;

        private float _currentSpeed;
        private float _currentTargetSpeed;

        private Coroutine _crouchRoutine;
        
        // Neutronic Boots integration
        private float _gravityMultiplier = 1f;
        private Liquid.Player.Equipment.NeutronicBoots _neutronicBoots;

        #endregion

        #region Serialized Fields

        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _sprintSpeed = 8f;
        [SerializeField] private float _crouchSpeed = 2.5f;
        [SerializeField] private float _walkToggleSpeed = 2.5f;
        
        [Header("Jump Settings")]
        [SerializeField] private float _jumpForce = 5f;
        [SerializeField] private float _gravity = -9.81f;

        [Header("Crouch Settings")]
        [SerializeField] private float _crouchHeightMultiplier = 0.5f;
        [SerializeField] private float _crouchTransitionDuration = 0.25f;

        [Header("Ground Check")]
        [SerializeField] private Transform _groundCheck;
        [SerializeField] private float _groundDistance = 0.4f;
        [SerializeField] private LayerMask _groundMask;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets whether the player is currently touching the ground.
        /// </summary>
        public bool IsGrounded => _isGrounded;
        
        /// <summary>
        /// Gets whether the player is currently sprinting.
        /// </summary>
        public bool IsSprinting => _isSprinting;
        
        /// <summary>
        /// Gets whether the player is currently crouching.
        /// </summary>
        public bool IsCrouching => _isCrouching;
        
        /// <summary>
        /// Gets whether the player is currently jumping/airborne.
        /// </summary>
        public bool IsJumping => _isJumping;
        
        /// <summary>
        /// Gets the current velocity of the player.
        /// </summary>
        public Vector3 Velocity => _velocity;
        
        /// <summary>
        /// Gets the current movement speed magnitude (horizontal only).
        /// </summary>
        public float CurrentSpeed => _currentSpeed;
        
        /// <summary>
        /// Gets the maximum speed the player is currently targeting based on movement state.
        /// </summary>
        public float MaxSpeed => _currentTargetSpeed;
        
        /// <summary>
        /// Gets whether the player has walk toggle enabled.
        /// </summary>
        public bool IsWalkingToggled { get; private set; }

        /// <summary>
        /// Gets the base walk speed for use by other systems (like Neutronic Boots).
        /// </summary>
        public float WalkSpeed => _walkSpeed;
        
        /// <summary>
        /// **FIX**: Public getter for the gravity value.
        /// </summary>
        public float Gravity => _gravity;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the movement controller with player settings and caches references.
        /// Should be called once during player setup.
        /// </summary>
        /// <param name="settings">The player settings configuration.</param>
        public void Initialize(PlayerSettings settings)
        {
            _settings = settings;
            _characterController = GetComponent<CharacterController>();

            if (_characterController == null)
            {
                Debug.LogError("[MovementController] CharacterController component not found!");
                return;
            }

            _originalHeight = _characterController.height;
            _originalCenter = _characterController.center;

            // Get reference to Neutronic Boots if present
            _neutronicBoots = GetComponent<Liquid.Player.Equipment.NeutronicBoots>();
        }

        #endregion

        #region Movement Handling

        /// <summary>
        /// Updates player movement for the current frame.
        /// Handles ground detection, input reading, speed calculation, jumping, and gravity.
        /// Skips normal movement when Neutronic Boots are handling ceiling physics.
        /// Should be called every frame from PlayerController.Update().
        /// </summary>
        public void HandleMovement()
        {
            if (_settings == null || InputManager.Instance == null) return;

            // CEILING PHYSICS OVERRIDE - Skip normal movement if boots are handling ceiling
            if (_neutronicBoots != null && _neutronicBoots.ShouldOverrideMovement)
            {
                // This remains correct: it stops velocity accumulation and cedes control.
                _velocity.y = 0f;
                return;
            }

            // NORMAL GROUND MOVEMENT
            bool wasGrounded = _isGrounded;
            _isGrounded = Physics.CheckSphere(_groundCheck.position, _groundDistance, _groundMask);

            if (!wasGrounded && _isGrounded)
            {
                _isJumping = false;
            }

            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }
            
            _moveInput = InputManager.Instance.MoveInput;
            _isSprinting = InputManager.Instance.IsSprinting && !_isCrouching;

            if (InputManager.Instance.CrouchPressed)
            {
                HandleCrouchToggle();
            }

            if (InputManager.Instance.WalkTogglePressed)
            {
                IsWalkingToggled = !IsWalkingToggled;
            }

            Vector3 move = transform.right * _moveInput.x + transform.forward * _moveInput.y;

            if (_isCrouching)
            {
                _currentTargetSpeed = _crouchSpeed;
            }
            else if (_isSprinting)
            {
                _currentTargetSpeed = _sprintSpeed;
            }
            else if (IsWalkingToggled)
            {
                _currentTargetSpeed = _walkToggleSpeed;
            }
            else
            {
                _currentTargetSpeed = _walkSpeed;
            }
            
            _characterController.Move(move * _currentTargetSpeed * Time.deltaTime);

            Vector3 horizontalVelocity = _characterController.velocity;
            horizontalVelocity.y = 0f;
            _currentSpeed = horizontalVelocity.magnitude;

            // Check if boots should prevent jumping
            bool bootsPreventJump = _neutronicBoots != null && _neutronicBoots.ShouldPreventJump;

            if (InputManager.Instance.JumpPressed && _isGrounded && !_isCrouching && !bootsPreventJump)
            {
                _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
                _isJumping = true;
            }

            _velocity.y += _gravity * _gravityMultiplier * Time.deltaTime;
            _characterController.Move(_velocity * Time.deltaTime);
        }

        #endregion
        
        #region Neutronic Boots Integration

        /// <summary>
        /// Sets the gravity multiplier for special movement states like ceiling walking.
        /// Called by Neutronic Boots component to reverse gravity.
        /// </summary>
        /// <param name="multiplier">The gravity multiplier (-1 for ceiling, 1 for normal).</param>
        public void SetGravityMultiplier(float multiplier)
        {
            _gravityMultiplier = multiplier;
            // This is no longer the primary mechanism for sticking, but is kept for state consistency.
            Debug.Log($"[MovementController] Gravity multiplier set to: {_gravityMultiplier}");
        }

        #endregion

        #region Crouch Handling

        private void HandleCrouchToggle()
        {
            if (_crouchRoutine != null)
            {
                StopCoroutine(_crouchRoutine);
            }

            _isCrouching = !_isCrouching;
            _crouchRoutine = StartCoroutine(TransitionCrouch(_isCrouching));
        }

        private IEnumerator TransitionCrouch(bool crouch)
        {
            float targetHeight = crouch ? _originalHeight * _crouchHeightMultiplier : _originalHeight;
            Vector3 targetCenter = crouch ? new Vector3(_originalCenter.x, _originalCenter.y * _crouchHeightMultiplier, _originalCenter.z) : _originalCenter;
            
            float startHeight = _characterController.height;
            Vector3 startCenter = _characterController.center;
            
            float elapsed = 0f;

            while (elapsed < _crouchTransitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _crouchTransitionDuration;

                _characterController.height = Mathf.Lerp(startHeight, targetHeight, t);
                _characterController.center = Vector3.Lerp(startCenter, targetCenter, t);

                yield return null;
            }

            _characterController.height = targetHeight;
            _characterController.center = targetCenter;
        }

        #endregion
    }
}