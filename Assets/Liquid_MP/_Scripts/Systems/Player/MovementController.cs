using System.Collections;
using _Scripts.Core.Managers;
using UnityEngine;
using Liquid.Audio;

namespace _Scripts.Systems.Player
{
    /// <summary>
    /// Handles player movement including walking, sprinting, crouching, jumping, and gravity.
    /// Manages the CharacterController and calculates movement based on input from InputManager.
    /// Exposes movement state properties for TacticalShooterPlayer (animation gait), noise, etc.
    /// Integrates with Neutronic Boots for ceiling walking physics override.
    /// Self-initializes in Awake() — no external initialization needed.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MovementController : MonoBehaviour
    {
        #region Private Fields

        private CharacterController _characterController;

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

        private float _footstepTimer;

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

        [Header("Noise Settings")]
        [SerializeField] private bool _enableMovementNoise = true;
        [Tooltip("Base time between footsteps at normal walk speed.")] // TODO: Match it with footstep audio.
        [SerializeField] private float _baseFootstepInterval = 0.5f;
        [Tooltip("Minimum horizontal movement speed before we start emitting footsteps.")]
        [SerializeField] private float _minSpeedForSteps = 0.1f;
        [Tooltip("References the room the player is in (For noise multiplier purposes)")]
        [SerializeField] private RoomNoisePreset _currentRoomNoise;

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

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _neutronicBoots = GetComponent<Liquid.Player.Equipment.NeutronicBoots>();
            _originalHeight = _characterController.height;
            _originalCenter = _characterController.center;

            // Auto-find or create GroundCheck if not assigned in Inspector.
            if (_groundCheck == null)
            {
                Transform existing = transform.Find("GroundCheck");
                if (existing != null)
                {
                    _groundCheck = existing;
                }
                else
                {
                    var go = new GameObject("GroundCheck");
                    go.transform.SetParent(transform);
                    go.transform.localPosition = new Vector3(0f, -_characterController.height / 2f + _characterController.center.y, 0f);
                    _groundCheck = go.transform;
                    Debug.Log($"[MovementController] Created GroundCheck at local Y={go.transform.localPosition.y:F2}");
                }
            }

            // Auto-set ground mask if nothing assigned (default to everything except Ignore Raycast).
            if (_groundMask == 0)
            {
                _groundMask = ~LayerMask.GetMask("Ignore Raycast");
                Debug.Log("[MovementController] Ground mask was empty — set to ~IgnoreRaycast.");
            }
        }

        #endregion

        #region Movement Handling

        /// <summary>
        /// Self-driven movement update. Reads input from InputManager, applies CharacterController physics.
        /// TacticalShooterPlayer reads our state properties (IsSprinting, IsGrounded, etc.) for animation gait.
        /// </summary>
        private void Update()
        {
            if (InputManager.Instance == null) return;
            HandleMovement();
        }

        private void HandleMovement()
        {
            if (InputManager.Instance == null) return;

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
            bool hasMovementInput = _moveInput.sqrMagnitude > 0.01f;
            _isSprinting = InputManager.Instance.IsSprinting && !_isCrouching && hasMovementInput;

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

            HandleMovementNoise(_currentSpeed);

            bool bootsPreventJump = _neutronicBoots != null && _neutronicBoots.ShouldPreventJump;
            if (InputManager.Instance.JumpPressed && _isGrounded && !_isCrouching && !bootsPreventJump)
            {
                _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
                _isJumping = true;

                EmitJumpNoise();
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

                EmitJumpNoise();
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

        #region Noise System
        /// <summary>
        /// Called by either triggers or other systems when the player enters a new room.
        /// </summary>
        public void SetCurrentRoom(RoomNoisePreset roomNoisePreset)
        {
            _currentRoomNoise = roomNoisePreset;
        }

        private void HandleMovementNoise(float horizontalSpeed)
        {
            if (!_enableMovementNoise)
            {
                return;
            }

            if (NoiseManager.Instance == null)
            {
                return;
            }

            if (!_isGrounded)
            {
                _footstepTimer = 0f;
                return;
            }

            if (horizontalSpeed < _minSpeedForSteps)
            {
                _footstepTimer = 0f;
                return;
            }

            float interval = _baseFootstepInterval;
            NoiseLevel level = NoiseLevel.Medium;

            if (_isCrouching)
            {
                interval *= 1.4f;
                level = NoiseLevel.Low;
            }
            else if (_isSprinting)
            {
                interval *= 0.7f;
                level = NoiseLevel.High;
            }
            else if (IsWalkingToggled)
            {
                interval *= 1.1f;
                level = NoiseLevel.Medium;
            }

            _footstepTimer += Time.deltaTime;

            if (_footstepTimer >= interval)
            {
                _footstepTimer = 0f;

                NoiseManager.Instance.EmitNoise(
                    transform.position,
                    level,
                    NoiseCategory.Footsteps,
                    _currentRoomNoise);
            }
        }

        private void EmitJumpNoise()
        {
            if (!_enableMovementNoise)
            {
                return;
            }

            if (NoiseManager.Instance == null)
            {
                return;
            }

            NoiseManager.Instance.EmitNoise(transform.position, NoiseLevel.Medium, NoiseCategory.Jump, _currentRoomNoise);
        }

        #endregion
    }
}