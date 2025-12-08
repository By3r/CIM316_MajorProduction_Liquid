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
        
        private Liquid.Player.Equipment.NeutronicBoots _neutronicBoots;
        private float _gravityMultiplier = 1f;

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

        public bool IsGrounded => _isGrounded;
        public bool IsSprinting => _isSprinting;
        public bool IsCrouching => _isCrouching;
        public bool IsJumping => _isJumping;
        public Vector3 Velocity => _velocity;
        public float CurrentSpeed => _currentSpeed;
        public float MaxSpeed => _currentTargetSpeed;
        public bool IsWalkingToggled { get; private set; }
        public float WalkSpeed => _walkSpeed;
        public float Gravity => _gravity;

        #endregion

        #region Initialization

        public void Initialize(PlayerSettings settings)
        {
            _settings = settings;
            _characterController = GetComponent<CharacterController>();
            _neutronicBoots = GetComponent<Liquid.Player.Equipment.NeutronicBoots>();
            _originalHeight = _characterController.height;
            _originalCenter = _characterController.center;
        }

        #endregion

        #region Movement Handling

        public void HandleMovement()
        {
            if (_settings == null || InputManager.Instance == null) return;

            if (_neutronicBoots != null && _neutronicBoots.ShouldOverrideMovement)
            {
                _velocity.y = 0f;
                return;
            }

            bool wasGrounded = _isGrounded;
            _isGrounded = Physics.CheckSphere(_groundCheck.position, _groundDistance, _groundMask);

            if (!wasGrounded && _isGrounded) _isJumping = false;
            if (_isGrounded && _velocity.y < 0) _velocity.y = -2f;
            
            _moveInput = InputManager.Instance.MoveInput;
            _isSprinting = InputManager.Instance.IsSprinting && !_isCrouching;

            if (InputManager.Instance.CrouchPressed) HandleCrouchToggle();
            if (InputManager.Instance.WalkTogglePressed) IsWalkingToggled = !IsWalkingToggled;

            Vector3 move = transform.right * _moveInput.x + transform.forward * _moveInput.y;

            if (_isCrouching) _currentTargetSpeed = _crouchSpeed;
            else if (_isSprinting) _currentTargetSpeed = _sprintSpeed;
            else if (IsWalkingToggled) _currentTargetSpeed = _walkToggleSpeed;
            else _currentTargetSpeed = _walkSpeed;
            
            _characterController.Move(move * _currentTargetSpeed * Time.deltaTime);

            Vector3 horizontalVelocity = _characterController.velocity;
            horizontalVelocity.y = 0f;
            _currentSpeed = horizontalVelocity.magnitude;

            // This jump logic now only handles "normal" jumps when the boots aren't interfering.
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
        /// Called by NeutronicBoots when a "tap jump" is detected.
        /// Bypasses the normal jump conditions to execute a jump.
        /// </summary>
        public void ForceJump()
        {
            if (_isGrounded && !_isCrouching)
            {
                _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
                _isJumping = true;
                Debug.Log("[MovementController] ForceJump executed by NeutronicBoots.");
            }
        }
        
        public void SetGravityMultiplier(float multiplier)
        {
            _gravityMultiplier = multiplier;
        }

        #endregion

        #region Crouch Handling

        private void HandleCrouchToggle()
        {
            if (_crouchRoutine != null) StopCoroutine(_crouchRoutine);
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