using UnityEngine;

namespace _Scripts.Player
{
    /// <summary>
    /// Applies visual effects to the camera including head bob, FOV changes for sprinting/jumping/crouching, and position/rotation offsets.
    /// Receives movement state data via UpdateEffects() each frame and calculates corresponding visual effects.
    /// Supports enabling/disabling individual effects and allows runtime adjustment via public methods.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraEffectsController : MonoBehaviour
    {
        private Camera _camera;
        private CameraController _cameraController;
        
        [Header("Base Settings")]
        [SerializeField] private Vector3 _baseLocalPosition = new Vector3(0, 0.6f, 0);
        
        [Header("Camera Bob Settings")]
        [SerializeField] private float _bobFrequency = 2f;
        [SerializeField] private float _bobHorizontalAmplitude = 0.05f;
        [SerializeField] private float _bobVerticalAmplitude = 0.1f;
        [SerializeField] private float _bobSmoothness = 10f;
        [SerializeField] private float _bobSpeedThreshold = 0.1f;

        [Header("Walk Bob Modifiers")]
        [SerializeField] private float _walkBobFrequencyMultiplier = 0.8f;
        [SerializeField] private float _walkBobAmplitudeMultiplier = 0.8f;

        [Header("Sprint Bob Modifiers")]
        [SerializeField] private float _sprintBobFrequencyMultiplier = 1.5f;
        [SerializeField] private float _sprintBobAmplitudeMultiplier = 1.2f;
        
        [Header("Sprint FOV Settings")]
        [SerializeField] private bool _enableSprintFOV = true;
        [SerializeField] private float _sprintFOVIncrease = 10f;
        [SerializeField] private float _fovTransitionSpeed = 5f;
        
        [Header("Jump FOV Settings")]
        [SerializeField] private bool _enableJumpFOV = true;
        [SerializeField] private float _jumpFOVDecrease = 5f;
        [SerializeField] private float _jumpFOVTransitionSpeed = 8f;
        
        [Header("Crouch FOV Settings")]
        [SerializeField] private bool _enableCrouchFOV = false;
        [SerializeField] private float _crouchFOVDecrease = 3f;
        [SerializeField] private float _crouchFOVTransitionSpeed = 4f;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugInfo = false;
        
        private float _bobTimer;
        private Vector3 _bobOffset;
        private Vector3 _targetBobOffset;
        
        private float _baseFOV;
        private float _targetFOV;
        private float _currentFOV;
        
        private float _currentSpeed;
        private float _maxSpeed;
        private bool _isGrounded;
        private bool _isSprinting;
        private bool _isCrouching;
        private bool _isJumping;
        private bool _isWalking;
        private bool _cameraBobEnabled = true;
        
        private Vector3 _positionOffset = Vector3.zero;
        private Quaternion _rotationOffset = Quaternion.identity;
        private float _fovOffset = 0f;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _cameraController = GetComponent<CameraController>();
        }

        private void Start()
        {
            _baseFOV = _camera.fieldOfView;
            _currentFOV = _baseFOV;
            _targetFOV = _baseFOV;
            transform.localPosition = _baseLocalPosition;
        }

        /// <summary>
        /// Updates all camera effects based on current movement state.
        /// Should be called every frame from PlayerController.Update().
        /// Calculates head bob and FOV modifiers based on the provided movement parameters.
        /// </summary>
        /// <param name="currentSpeed">The current movement speed of the player.</param>
        /// <param name="maxSpeed">The maximum possible speed, used to normalize bob intensity.</param>
        /// <param name="isGrounded">Whether the player is currently on the ground.</param>
        /// <param name="isSprinting">Whether the player is currently sprinting.</param>
        /// <param name="isCrouching">Whether the player is currently crouching.</param>
        /// <param name="isJumping">Whether the player is currently jumping.</param>
        /// <param name="isWalking">Whether the player is in walk toggle mode.</param>
        /// <param name="cameraBobEnabled">Whether head bob effects should be applied.</param>
        public void UpdateEffects(float currentSpeed, float maxSpeed, bool isGrounded, bool isSprinting, bool isCrouching, bool isJumping, bool isWalking, bool cameraBobEnabled)
        {
            _currentSpeed = currentSpeed;
            _maxSpeed = maxSpeed;
            _isGrounded = isGrounded;
            _isSprinting = isSprinting;
            _isCrouching = isCrouching;
            _isJumping = isJumping;
            _isWalking = isWalking;
            _cameraBobEnabled = cameraBobEnabled;
            
            _positionOffset = Vector3.zero;
            _rotationOffset = Quaternion.identity;
            _fovOffset = 0f;
            
            if (_cameraBobEnabled)
            {
                CalculateCameraBob();
            }
            
            if (_enableSprintFOV || _enableJumpFOV || _enableCrouchFOV)
            {
                CalculateFOVEffects();
            }
            
            ApplyEffects();
        }

        private void CalculateCameraBob()
        {
            if (!_isGrounded || _currentSpeed < _bobSpeedThreshold)
            {
                _bobTimer = 0f;
                _targetBobOffset = Vector3.zero;
            }
            else
            {
                float speedFactor = Mathf.Clamp01(_currentSpeed / _maxSpeed);
                
                float frequencyMultiplier = 1f;
                float amplitudeMultiplier = 1f;

                if (_isSprinting)
                {
                    frequencyMultiplier = _sprintBobFrequencyMultiplier;
                    amplitudeMultiplier = _sprintBobAmplitudeMultiplier;
                }
                else if (_isWalking)
                {
                    frequencyMultiplier = _walkBobFrequencyMultiplier;
                    amplitudeMultiplier = _walkBobAmplitudeMultiplier;
                }
                
                _bobTimer += Time.deltaTime * _bobFrequency * frequencyMultiplier * speedFactor;
                
                float horizontalBob = Mathf.Sin(_bobTimer) * _bobHorizontalAmplitude * amplitudeMultiplier * speedFactor;
                float verticalBob = Mathf.Sin(_bobTimer * 2f) * _bobVerticalAmplitude * amplitudeMultiplier * speedFactor;
                
                _targetBobOffset = new Vector3(horizontalBob, verticalBob, 0f);
            }
            
            _bobOffset = Vector3.Lerp(_bobOffset, _targetBobOffset, Time.deltaTime * _bobSmoothness);
            _positionOffset += _bobOffset;
        }

        private void CalculateFOVEffects()
        {
            _targetFOV = _baseFOV;
            float transitionSpeed = _fovTransitionSpeed;
            
            if (_enableJumpFOV && _isJumping && !_isGrounded)
            {
                _targetFOV = _baseFOV - _jumpFOVDecrease;
                transitionSpeed = _jumpFOVTransitionSpeed;
            }
            else if (_enableCrouchFOV && _isCrouching)
            {
                _targetFOV = _baseFOV - _crouchFOVDecrease;
                transitionSpeed = _crouchFOVTransitionSpeed;
            }
            else if (_enableSprintFOV && _isSprinting && _isGrounded)
            {
                _targetFOV = _baseFOV + _sprintFOVIncrease;
            }
            
            _currentFOV = Mathf.Lerp(_currentFOV, _targetFOV, Time.deltaTime * transitionSpeed);
            _fovOffset = _currentFOV - _baseFOV;
        }

        private void ApplyEffects()
        {
            transform.localPosition = _baseLocalPosition + _positionOffset;
            
            if (_camera != null && _cameraController != null)
            {
                float settingsFOV = _cameraController.GetBaseFOV();
                if (settingsFOV > 0)
                {
                    _baseFOV = settingsFOV;
                }
                
                _camera.fieldOfView = _baseFOV + _fovOffset;
            }
        }

        /// <summary>
        /// Sets the base local position offset for the camera within the player.
        /// </summary>
        /// <param name="position">The new base local position.</param>
        public void SetBasePosition(Vector3 position)
        {
            _baseLocalPosition = position;
        }

        /// <summary>
        /// Adds an additional position offset to the camera for this frame.
        /// Useful for recoil, knockback, or other temporary position effects.
        /// </summary>
        /// <param name="offset">The position offset to add.</param>
        public void AddPositionOffset(Vector3 offset)
        {
            _positionOffset += offset;
        }

        /// <summary>
        /// Adds a rotation offset to the camera for this frame.
        /// Useful for recoil, screen shake, or other rotational effects.
        /// </summary>
        /// <param name="offset">The rotation offset to apply.</param>
        public void AddRotationOffset(Quaternion offset)
        {
            _rotationOffset *= offset;
        }

        /// <summary>
        /// Adds a field of view offset to the camera for this frame.
        /// Useful for temporary FOV changes independent of movement state.
        /// </summary>
        /// <param name="offset">The FOV offset to add.</param>
        public void AddFOVOffset(float offset)
        {
            _fovOffset += offset;
        }

        /// <summary>
        /// Resets all camera effects to their base state.
        /// Clears head bob, FOV changes, position and rotation offsets, and returns to base position.
        /// </summary>
        public void ResetEffects()
        {
            _bobTimer = 0f;
            _bobOffset = Vector3.zero;
            _targetBobOffset = Vector3.zero;
            _currentFOV = _baseFOV;
            _targetFOV = _baseFOV;
            
            transform.localPosition = _baseLocalPosition;
            transform.localRotation = Quaternion.identity;
            
            if (_camera != null)
            {
                _camera.fieldOfView = _baseFOV;
            }
        }
    }
}