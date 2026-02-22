using System.Collections;
using _Scripts.Core.Managers;
using _Scripts.Systems.Player;
using UnityEngine;

namespace Liquid.Player.Equipment
{
    /// <summary>
    /// Manages the Neutronic Boots equipment system for ceiling walking.
    /// Handles activation via hold-to-activate, player/camera rotation transitions,
    /// gravity reversal, custom ceiling physics, and dismount mechanics.
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
        private bool _isConsideringActivation; // Manages the tap-vs-hold state
        private float _ceilingGraceTimer;
        private float _dismountCooldownTimer; 
        private bool _isDismounting;
        
        // Component references
        private _Scripts.Systems.Player.MovementController _movementController;
        private _Scripts.Systems.Player.CameraController _cameraController;

        // Ceiling physics state
        private Vector3 _ceilingVelocity;
        private float _baseMovementSpeed;
        private float _gravityValue;

        #endregion

        #region Public Properties

        public bool IsOnCeiling => _isOnCeiling;
        public bool IsTransitioning => _isTransitioning;
        public float ActivationProgress => _isHoldingActivation ? Mathf.Clamp01((_activationHoldTimer - _settings.JumpGracePeriod) / (_settings.ActivationHoldTime - _settings.JumpGracePeriod)) : 0f;
        
        // This property stops the MovementController from jumping on its own, giving this script full control.
        public bool ShouldPreventJump =>
            _isOnCeiling || 
            _isTransitioning ||
            // Proactively prevent any jump if we are even considering a ceiling walk.
            (_ceilingDetector.IsCeilingAvailable && InputManager.Instance.IsJumpHeld && _movementController.IsGrounded) ||
            _isConsideringActivation;
            
        public bool ShouldOverrideMovement => (_isOnCeiling || _isTransitioning) && !_isDismounting;
        public float CeilingSpeed => _ceilingVelocity.magnitude;
        public float MaxCeilingSpeed => _baseMovementSpeed * (_settings != null ? _settings.CeilingMovementSpeedMultiplier : 1f);


        #endregion

        #region Initialization

        private void Awake()
        {
            _ceilingDetector = gameObject.AddComponent<CeilingDetector>();
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
            
            if (_activationSlider != null) _activationSlider.value = 0f;
            if (_activationSliderCanvasGroup != null) _activationSliderCanvasGroup.alpha = 0f;
            if (_ceilingAvailableIcon != null) _ceilingAvailableIcon.alpha = 0f;

            if (_movementController != null)
            {
                _baseMovementSpeed = _movementController.WalkSpeed;
                _gravityValue = _movementController.Gravity;
            }
            else
            {
                _gravityValue = Physics.gravity.y;
                Debug.LogWarning("[NeutronicBoots] MovementController not found. Using system gravity.");
            }
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (_settings == null) return;

            _ceilingDetector.DetectCeiling();
            UpdateUI();

            if (_ceilingGraceTimer > 0f) _ceilingGraceTimer -= Time.deltaTime;
            if (_dismountCooldownTimer > 0f) _dismountCooldownTimer -= Time.deltaTime;
            
            if (!_isOnCeiling && !_isTransitioning && _dismountCooldownTimer <= 0f)
            {
                HandleActivationInput();
            }

            if (_isOnCeiling && !_isTransitioning && !_isDismounting)
            {
                HandleCeilingPhysics();
            }

            if (_isOnCeiling && !_isTransitioning && InputManager.Instance.JumpPressed)
            {
                StartCoroutine(DismountFromCeiling());
            }
        }

        #endregion

        #region Activation Logic

        private void HandleActivationInput()
        {
            bool canActivate = _ceilingDetector.IsCeilingAvailable && _movementController != null && _movementController.IsGrounded;

            // --- State Machine for Tap-vs-Hold ---

            // Entry condition: Player presses Jump while on the ground with a ceiling available.
            if (InputManager.Instance.JumpPressed && canActivate && !_isConsideringActivation)
            {
                _isConsideringActivation = true;
                _activationHoldTimer = 0f;
            }

            // Logic while in the "considering" state
            if (_isConsideringActivation)
            {
                // Action 1: Player RELEASES the button. This is the highest priority.
                if (InputManager.Instance.JumpReleased)
                {
                    // If the hold time was short enough, it's a "tap". JUMP IMMEDIATELY.
                    if (_activationHoldTimer < _settings.JumpGracePeriod)
                    {
                        _movementController.ForceJump();
                    }
                    // No matter what, releasing the button resets the state.
                    _isConsideringActivation = false;
                    _isHoldingActivation = false;
                }
                // Action 2: Player CONTINUES TO HOLD the button.
                else if (InputManager.Instance.IsJumpHeld)
                {
                    _activationHoldTimer += Time.deltaTime;
                    // If the hold time passes the grace period, it's now officially a "hold".
                    if (_activationHoldTimer >= _settings.JumpGracePeriod)
                    {
                        _isHoldingActivation = true;
                    }
                }
                // Fallback: If the button is no longer held (but wasn't released this frame), reset.
                else
                {
                    _isConsideringActivation = false;
                    _isHoldingActivation = false;
                }
            }
            
            // --- Logic for when a "Hold" is confirmed ---
            if (_isHoldingActivation)
            {
                // Update the UI slider
                if (_activationSlider != null) _activationSlider.value = ActivationProgress;

                // Check for full activation
                if (_activationHoldTimer >= _settings.ActivationHoldTime)
                {
                    StartCoroutine(ActivateCeilingWalk());
                    _isConsideringActivation = false;
                    _isHoldingActivation = false;
                }
            }
            else
            {
                // Ensure slider is hidden if not holding
                if (_activationSlider != null) _activationSlider.value = 0;
            }
        }

        #endregion

        #region Ceiling Walk Activation

        private IEnumerator ActivateCeilingWalk()
        {
            if (_isTransitioning) yield break;

            _isTransitioning = true;
            transform.position = _ceilingDetector.LastCeilingHit.point - (Vector3.up * (_characterController.height / 2f));
            if (_movementController != null) _movementController.SetGravityMultiplier(-1f);

            Transform playerTransform = _playerBody != null ? _playerBody : transform;
            Quaternion startPlayerRotation = playerTransform.rotation;
            Quaternion targetPlayerRotation = startPlayerRotation * Quaternion.Euler(0f, 0f, 180f);
            
            if (PlayerSettingsManager.Instance.CurrentSettings.EnableCameraBob)
            {
                float elapsed = 0f;
                while (elapsed < _settings.RotationTransitionDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / _settings.RotationTransitionDuration);
                    playerTransform.rotation = Quaternion.Slerp(startPlayerRotation, targetPlayerRotation, t);
                    
                    Vector3 antiGravityForce = Vector3.up * -_gravityValue;
                    _characterController.Move(antiGravityForce * Time.deltaTime);
                
                    yield return null;
                }
            }
            
            playerTransform.rotation = targetPlayerRotation;

            _isOnCeiling = true;
            _ceilingGraceTimer = _settings.CeilingContactGracePeriod;
            _ceilingVelocity = Vector3.zero;
            _isTransitioning = false;
            
            Debug.Log("[NeutronicBoots] Ceiling walk activated!");
        }

        #endregion

        #region Ceiling Physics

        private void HandleCeilingPhysics()
        {
            if (_isDismounting) return;

            Vector2 input = InputManager.Instance.MoveInput;
            Transform playerTransform = _playerBody != null ? _playerBody : transform;
            Vector3 playerPosition = transform.position;
            Vector3 rayDirection = Vector3.up;

            if (Physics.Raycast(
                playerPosition,
                rayDirection,
                out RaycastHit hit,
                _settings.CeilingDetectionDistance,
                _settings.CeilingWalkableLayer,
                QueryTriggerInteraction.Ignore))
            {
                Vector3 antiGravityForce = Vector3.up * -_gravityValue;
                Vector3 stickyForce = CalculateStickyForce(hit, rayDirection);
                Vector3 ceilingMovement = CalculateCeilingMovement(input, hit.normal, playerTransform);
                ceilingMovement = ApplyFriction(ceilingMovement);

                Vector3 finalMovement = antiGravityForce + stickyForce + ceilingMovement;
                _characterController.Move(finalMovement * Time.deltaTime);
            }
            else
            {
                if (_ceilingGraceTimer > 0f) return;
                if (_isDismounting || _isTransitioning) return;
                StartCoroutine(DismountFromCeiling());
            }
        }

        private Vector3 CalculateStickyForce(RaycastHit hit, Vector3 rayDirection)
        {
            float targetDistance = _characterController.height * 0.5f;
            float currentDistance = hit.distance;
            float distanceError = currentDistance - targetDistance;

            float forceStrength = distanceError * _settings.StickyForceStrength;
            forceStrength = Mathf.Clamp(forceStrength, -_settings.MaxStickyForce, _settings.MaxStickyForce);

            return rayDirection * forceStrength;
        }

        private Vector3 CalculateCeilingMovement(Vector2 input, Vector3 surfaceNormal, Transform playerTransform)
        {
            if (input.magnitude < 0.01f) return Vector3.zero;

            Vector3 forward = playerTransform.forward;
            Vector3 right = playerTransform.right;
            Vector3 desiredMoveDirection = (right * input.x + forward * input.y).normalized;
            Vector3 projectedMovement = Vector3.ProjectOnPlane(desiredMoveDirection, surfaceNormal);

            float speed = _baseMovementSpeed * _settings.CeilingMovementSpeedMultiplier;
            if (_settings.AllowSprintOnCeiling && InputManager.Instance.IsSprinting)
            {
                speed *= 1.5f;
            }

            return projectedMovement * speed;
        }

        private Vector3 ApplyFriction(Vector3 movement)
        {
            Vector2 input = InputManager.Instance.MoveInput;
            
            if (input.magnitude < 0.01f)
            {
                _ceilingVelocity = Vector3.Lerp(_ceilingVelocity, Vector3.zero, _settings.CeilingFriction * Time.deltaTime);
                return _ceilingVelocity;
            }

            _ceilingVelocity = Vector3.Lerp(_ceilingVelocity, movement, _settings.CeilingAcceleration * Time.deltaTime);
            return _ceilingVelocity * _settings.CeilingFrictionCoefficient;
        }

        #endregion

        #region Dismount Logic

        private IEnumerator DismountFromCeiling()
        {
            if (_isTransitioning || _isDismounting) yield break;
            
            _isDismounting = true;
            _isTransitioning = true;
            _isOnCeiling = false;
            _ceilingVelocity = Vector3.zero;
            _dismountCooldownTimer = 1.5f;
            
            if (_movementController != null) _movementController.SetGravityMultiplier(1f);

            Transform playerTransform = _playerBody != null ? _playerBody : transform;
            Quaternion startPlayerRotation = playerTransform.rotation;
            Quaternion targetPlayerRotation = startPlayerRotation * Quaternion.Euler(0f, 0f, 180f);
            
            if (PlayerSettingsManager.Instance.CurrentSettings.EnableCameraBob)
            {
                float elapsed = 0f;
                while (elapsed < _settings.RotationTransitionDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / _settings.RotationTransitionDuration);
                    playerTransform.rotation = Quaternion.Slerp(startPlayerRotation, targetPlayerRotation, t);
                    yield return null;
                }
            }
            
            playerTransform.rotation = targetPlayerRotation;
            
            _isTransitioning = false;
            _isDismounting = false;
            
            Debug.Log("[NeutronicBoots] Dismounted from ceiling.");
        }

        #endregion

        #region UI Management

        private void UpdateUI()
        {
            if (_ceilingAvailableIcon != null)
            {
                float targetAlpha = (_ceilingDetector.IsCeilingAvailable && !_isOnCeiling) ? 1f : 0f;
                _ceilingAvailableIcon.alpha = Mathf.Lerp(_ceilingAvailableIcon.alpha, targetAlpha, Time.deltaTime * _settings.UIFadeSpeed);
            }

            if (_activationSliderCanvasGroup != null)
            {
                float targetAlpha = _isHoldingActivation ? 1f : 0f;
                _activationSliderCanvasGroup.alpha = Mathf.Lerp(_activationSliderCanvasGroup.alpha, targetAlpha, Time.deltaTime * _settings.UIFadeSpeed);
            }
        }

        #endregion
    }
}