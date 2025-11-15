using System.Collections;
using _Scripts.Core;
using UnityEngine;

namespace _Scripts.Player
{
    /// <summary>
    /// Handles player movement including walking, sprinting, crouching, jumping, and gravity.
    /// Manages the CharacterController and calculates movement based on input and player settings.
    /// Exposes movement state properties for other systems (camera effects, animations, etc.).
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
        /// Gets whether the player has toggled walk mode (reduced speed mode).
        /// </summary>
        public bool IsWalkingToggled { get; private set; }

        #endregion

        #region Initialization

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _originalHeight = _characterController.height;
            _originalCenter = _characterController.center;
            
            if (_groundCheck == null)
            {
                GameObject groundCheckObj = new GameObject("GroundCheck");
                groundCheckObj.transform.SetParent(transform);
                groundCheckObj.transform.localPosition = new Vector3(0, -_characterController.height / 2, 0);
                _groundCheck = groundCheckObj.transform;
            }
        }

        /// <summary>
        /// Initializes the movement controller with player settings.
        /// Must be called before HandleMovement() is used.
        /// </summary>
        /// <param name="settings">The player settings containing movement speeds and jump parameters.</param>
        public void Initialize(PlayerSettings settings)
        {
            _settings = settings;
        }

        #endregion

        #region Movement

        /// <summary>
        /// Updates player movement for the current frame.
        /// Handles ground detection, input reading, speed calculation, jumping, and gravity.
        /// Should be called every frame from PlayerController.Update().
        /// </summary>
        public void HandleMovement()
        {
            if (_settings == null || InputManager.Instance == null) return;

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

            if (InputManager.Instance.JumpPressed && _isGrounded && !_isCrouching)
            {
                _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
                _isJumping = true;
            }

            _velocity.y += _gravity * Time.deltaTime;
            _characterController.Move(_velocity * Time.deltaTime);
        }

        #endregion

        #region Crouch Mechanics

        private void HandleCrouchToggle()
        {
            if (_isCrouching)
            {
                if (CanStandUp())
                {
                    _isCrouching = false;
                    if (_crouchRoutine != null) StopCoroutine(_crouchRoutine);
                    _crouchRoutine = StartCoroutine(DoCrouchTransition(_originalHeight, _originalCenter));
                }
            }
            else
            {
                _isCrouching = true;
                if (_crouchRoutine != null) StopCoroutine(_crouchRoutine);
                float crouchHeight = _originalHeight * _crouchHeightMultiplier;
                Vector3 crouchCenter = _originalCenter * _crouchHeightMultiplier;
                _crouchRoutine = StartCoroutine(DoCrouchTransition(crouchHeight, crouchCenter));
            }
        }

        private IEnumerator DoCrouchTransition(float targetHeight, Vector3 targetCenter)
        {
            float currentHeight = _characterController.height;
            Vector3 currentCenter = _characterController.center;
            float elapsedTime = 0f;

            while (elapsedTime < _crouchTransitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / _crouchTransitionDuration);

                _characterController.height = Mathf.Lerp(currentHeight, targetHeight, t);
                _characterController.center = Vector3.Lerp(currentCenter, targetCenter, t);

                yield return null;
            }

            _characterController.height = targetHeight;
            _characterController.center = targetCenter;
        }

        private bool CanStandUp()
        {
            Vector3 start = transform.position + _originalCenter * _crouchHeightMultiplier;
            Vector3 end = transform.position + new Vector3(_originalCenter.x, _originalHeight - _characterController.radius, _originalCenter.z);
            
            return !Physics.CheckCapsule(start, end, _characterController.radius, _groundMask, QueryTriggerInteraction.Ignore);
        }

        #endregion

        #region Debug

        private void OnDrawGizmosSelected()
        {
            if (_groundCheck != null)
            {
                Gizmos.color = _isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(_groundCheck.position, _groundDistance);
            }
        }

        #endregion
    }
}