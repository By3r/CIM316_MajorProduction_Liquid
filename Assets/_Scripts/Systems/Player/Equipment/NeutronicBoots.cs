using System.Collections;
using _Scripts.Core.Managers;
using UnityEngine;

namespace Liquid.Player.Equipment
{
    /// <summary>
    /// Manages the Neutronic Boots equipment system for ceiling walking.
    /// Handles activation via hold-to-activate, player/camera rotation transitions,
    /// gravity reversal, and dismount mechanics.
    /// </summary>
    public class NeutronicBoots : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField] private NeutronicBootsSettings _settings;
        [SerializeField] private Transform _playerBody;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private CharacterController _characterController;
        
        [Header("UI References")]
        [SerializeField] private UnityEngine.UI.Slider _activationSlider;
        [SerializeField] private CanvasGroup _activationSliderCanvasGroup;
        [SerializeField] private CanvasGroup _ceilingAvailableIcon;

        #endregion

        #region Private Fields

        private CeilingDetector _ceilingDetector;
        
        // State tracking
        private bool _isOnCeiling;
        private bool _isTransitioning;
        private float _activationHoldTimer;
        private bool _isHoldingActivation;
        private float _ceilingGraceTimer; // Timer to prevent immediate fall after activation
        
        // Transition tracking
        private Quaternion _targetPlayerRotation;
        private Quaternion _targetCameraRotation;
        private Vector3 _targetPosition;
        
        // Original values (for reverting)
        private float _originalGravity;
        private Quaternion _originalPlayerRotation;
        private Quaternion _originalCameraRotation;

        // Component references
        private _Scripts.Systems.Player.MovementController _movementController;
        private _Scripts.Systems.Player.CameraController _cameraController;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets whether the player is currently walking on the ceiling.
        /// </summary>
        public bool IsOnCeiling => _isOnCeiling;

        /// <summary>
        /// Gets whether the player is currently transitioning to/from ceiling.
        /// </summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>
        /// Gets the current activation progress (0 to 1) for UI display.
        /// </summary>
        public float ActivationProgress => _settings != null ? Mathf.Clamp01(_activationHoldTimer / _settings.ActivationHoldTime) : 0f;

        /// <summary>
        /// Gets whether boots should prevent jumping (holding for activation or on ceiling).
        /// MovementController should check this before allowing jumps.
        /// </summary>
        public bool ShouldPreventJump => _isHoldingActivation || _isOnCeiling || _isTransitioning;

        #endregion

        #region Initialization

        private void Awake()
        {
            // Add ceiling detector component
            _ceilingDetector = gameObject.AddComponent<CeilingDetector>();
            
            // Get component references
            _characterController = GetComponent<CharacterController>();
            _movementController = GetComponent<_Scripts.Systems.Player.MovementController>();
            
            if (_cameraTransform != null)
            {
                _cameraController = _cameraTransform.GetComponent<_Scripts.Systems.Player.CameraController>();
            }
        }

        private void Start()
        {
            if (_settings == null)
            {
                Debug.LogError("[NeutronicBoots] Settings not assigned! Please assign NeutronicBootsSettings in the inspector.");
                enabled = false;
                return;
            }

            _ceilingDetector.Initialize(_settings, _playerBody != null ? _playerBody : transform);
            
            // Store original values
            _originalPlayerRotation = _playerBody != null ? _playerBody.rotation : transform.rotation;
            if (_cameraTransform != null)
            {
                _originalCameraRotation = _cameraTransform.localRotation;
            }

            // Initialize UI
            if (_activationSlider != null)
            {
                _activationSlider.value = 0f;
            }
            
            if (_activationSliderCanvasGroup != null)
            {
                _activationSliderCanvasGroup.alpha = 0f;
            }

            if (_ceilingAvailableIcon != null)
            {
                _ceilingAvailableIcon.alpha = 0f;
            }
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (_settings == null) return;

            // Continuous ceiling detection
            _ceilingDetector.DetectCeiling();

            // Handle UI visibility
            UpdateUI();

            // Handle activation input
            if (!_isOnCeiling && !_isTransitioning)
            {
                HandleActivationInput();
            }

            // Count down grace timer
            if (_ceilingGraceTimer > 0f)
            {
                _ceilingGraceTimer -= Time.deltaTime;
            }

            // Validate ceiling surface while on ceiling (after grace period)
            if (_isOnCeiling && !_isTransitioning && _ceilingGraceTimer <= 0f)
            {
                ValidateCeilingContact();
            }

            // Handle dismount input
            if (_isOnCeiling && InputManager.Instance.JumpPressed)
            {
                StartCoroutine(DismountFromCeiling());
            }
        }

        #endregion

        #region Activation Logic

        private void HandleActivationInput()
        {
            // Check if jump is being held
            bool jumpHeld = InputManager.Instance != null && InputManager.Instance.IsJumpHeld;

            if (jumpHeld && _ceilingDetector.IsCeilingAvailable && _movementController != null && _movementController.IsGrounded)
            {
                _isHoldingActivation = true;
                _activationHoldTimer += Time.deltaTime;

                // Update slider
                if (_activationSlider != null)
                {
                    _activationSlider.value = ActivationProgress;
                }

                // Check if activation complete
                if (_activationHoldTimer >= _settings.ActivationHoldTime)
                {
                    StartCoroutine(ActivateCeilingWalk());
                    _activationHoldTimer = 0f;
                    _isHoldingActivation = false;
                }
            }
            else
            {
                // Reset if released early
                if (_isHoldingActivation)
                {
                    _activationHoldTimer = 0f;
                    _isHoldingActivation = false;
                    
                    if (_activationSlider != null)
                    {
                        _activationSlider.value = 0f;
                    }
                }
            }
        }

        #endregion

        #region Ceiling Walk Activation

        private IEnumerator ActivateCeilingWalk()
        {
            _isTransitioning = true;

            // Calculate target rotations
            Transform targetTransform = _playerBody != null ? _playerBody : transform;
            Quaternion startPlayerRotation = targetTransform.rotation;
            Quaternion targetPlayerRotation = startPlayerRotation * Quaternion.Euler(180f, 0f, 0f);

            // Calculate target position (move to ceiling)
            Vector3 startPosition = transform.position;
            Vector3 targetPosition = _ceilingDetector.LastCeilingHit.point - (Vector3.up * (_characterController.height / 2f));

            // Camera rotation setup
            Quaternion startCameraRotation = Quaternion.identity;
            Quaternion targetCameraRotation = Quaternion.identity;
            
            if (_settings.RotateCameraWithPlayer && _cameraTransform != null)
            {
                startCameraRotation = _cameraTransform.localRotation;
                targetCameraRotation = startCameraRotation * Quaternion.Euler(180f, 0f, 0f);
            }

            float transitionProgress = 0f;

            while (transitionProgress < 1f)
            {
                transitionProgress += Time.deltaTime * _settings.RotationTransitionSpeed;
                float t = Mathf.SmoothStep(0f, 1f, transitionProgress);

                // Rotate player
                targetTransform.rotation = Quaternion.Slerp(startPlayerRotation, targetPlayerRotation, t);

                // Move toward ceiling
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);

                // Rotate camera if enabled
                if (_settings.RotateCameraWithPlayer && _cameraTransform != null)
                {
                    _cameraTransform.localRotation = Quaternion.Slerp(startCameraRotation, targetCameraRotation, t);
                }

                yield return null;
            }

            // Finalize
            targetTransform.rotation = targetPlayerRotation;
            transform.position = targetPosition;
            
            if (_settings.RotateCameraWithPlayer && _cameraTransform != null)
            {
                _cameraTransform.localRotation = targetCameraRotation;
            }

            _isOnCeiling = true;
            _isTransitioning = false;
            
            // Set grace period to prevent immediate fall detection
            _ceilingGraceTimer = _settings.CeilingContactGracePeriod;

            // Reverse gravity so player sticks to ceiling
            if (_movementController != null)
            {
                _movementController.SetGravityMultiplier(-1f);
            }
            
            Debug.Log("[NeutronicBoots] Ceiling walk activated!");
        }

        #endregion

        #region Ceiling Validation

        private void ValidateCeilingContact()
        {
            // Check if player is still on valid ceiling surface
            // When upside-down, we need to check in the player's "up" direction (which points toward ceiling)
            Transform checkTransform = _playerBody != null ? _playerBody : transform;
            Vector3 checkPosition = transform.position;
            Vector3 checkDirection = checkTransform.up; // This will point toward ceiling when upside-down
            
            float checkDistance = _characterController.height * 0.6f; // Check slightly more than half the height
            
            // Raycast in the "up" direction (toward ceiling when inverted)
            bool stillOnCeiling = Physics.Raycast(
                checkPosition,
                checkDirection,
                out RaycastHit hit,
                checkDistance,
                _settings.CeilingWalkableLayer,
                QueryTriggerInteraction.Ignore);
            
            // Debug visualization
            if (_settings.ShowDebugGizmos)
            {
                Debug.DrawRay(checkPosition, checkDirection * checkDistance, stillOnCeiling ? Color.green : Color.red);
            }
            
            if (!stillOnCeiling)
            {
                // Surface ended - dismount immediately
                Debug.Log("[NeutronicBoots] Lost ceiling contact - falling!");
                StartCoroutine(DismountFromCeiling(true));
            }
        }

        #endregion

        #region Dismount Logic

        private IEnumerator DismountFromCeiling(bool immediatefall = false)
        {
            if (_isTransitioning) yield break;

            // Restore normal gravity FIRST before any transitions
            if (_movementController != null)
            {
                _movementController.SetGravityMultiplier(1f);
            }

            _isTransitioning = true;
            _isOnCeiling = false;

            if (immediatefall)
            {
                // Just fall - no fancy transition
                Transform targetTransform = _playerBody != null ? _playerBody : transform;
                targetTransform.rotation = _originalPlayerRotation;
                
                if (_settings.RotateCameraWithPlayer && _cameraTransform != null)
                {
                    _cameraTransform.localRotation = _originalCameraRotation;
                }

                _isTransitioning = false;
                Debug.Log("[NeutronicBoots] Dismounted from ceiling (immediate fall)");
                yield break;
            }

            // Smooth transition back to normal
            Transform playerTransform = _playerBody != null ? _playerBody : transform;
            Quaternion startPlayerRotation = playerTransform.rotation;
            
            Quaternion startCameraRotation = Quaternion.identity;
            if (_settings.RotateCameraWithPlayer && _cameraTransform != null)
            {
                startCameraRotation = _cameraTransform.localRotation;
            }

            float transitionProgress = 0f;

            while (transitionProgress < 1f)
            {
                transitionProgress += Time.deltaTime * _settings.RotationTransitionSpeed;
                float t = Mathf.SmoothStep(0f, 1f, transitionProgress);

                // Rotate player back
                playerTransform.rotation = Quaternion.Slerp(startPlayerRotation, _originalPlayerRotation, t);

                // Rotate camera back if enabled
                if (_settings.RotateCameraWithPlayer && _cameraTransform != null)
                {
                    _cameraTransform.localRotation = Quaternion.Slerp(startCameraRotation, _originalCameraRotation, t);
                }

                yield return null;
            }

            // Finalize
            playerTransform.rotation = _originalPlayerRotation;
            
            if (_settings.RotateCameraWithPlayer && _cameraTransform != null)
            {
                _cameraTransform.localRotation = _originalCameraRotation;
            }

            _isTransitioning = false;
            Debug.Log("[NeutronicBoots] Dismounted from ceiling");
        }

        #endregion

        #region UI Management

        private void UpdateUI()
        {
            // Fade ceiling available icon
            if (_ceilingAvailableIcon != null)
            {
                float targetAlpha = (_ceilingDetector.IsCeilingAvailable && !_isOnCeiling) ? 1f : 0f;
                _ceilingAvailableIcon.alpha = Mathf.Lerp(
                    _ceilingAvailableIcon.alpha,
                    targetAlpha,
                    Time.deltaTime * _settings.UIFadeSpeed
                );
            }

            // Fade activation slider
            if (_activationSliderCanvasGroup != null)
            {
                float targetAlpha = _isHoldingActivation ? 1f : 0f;
                _activationSliderCanvasGroup.alpha = Mathf.Lerp(
                    _activationSliderCanvasGroup.alpha,
                    targetAlpha,
                    Time.deltaTime * _settings.UIFadeSpeed
                );
            }
        }

        #endregion
    }
}