using _Scripts.Core.Managers;
using UnityEngine;

namespace _Scripts.Systems.Player
{
    /// <summary>
    /// Handles first-person camera rotation and FOV settings.
    /// Reads look input from InputManager and applies mouse sensitivity, Y-axis inversion, and angle clamping.
    /// Must be initialized with a player body transform and PlayerSettings after instantiation.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform _playerBody;
        private Camera _camera;

        private PlayerSettings _settings;

        private float _xRotation;
        private Vector2 _lookInput;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        /// <summary>
        /// Initializes the camera controller with the player body and settings.
        /// Must be called before HandleCameraRotation() is used.
        /// </summary>
        /// <param name="playerBody">The player body transform to rotate for horizontal look.</param>
        /// <param name="settings">The player settings containing sensitivity, FOV, and inversion preferences.</param>
        public void Initialize(Transform playerBody, PlayerSettings settings)
        {
            _playerBody = playerBody;
            _settings = settings;
            ApplySettings();
        }

        /// <summary>
        /// Applies the current player settings to the camera (primarily FOV).
        /// Called during initialization and when settings are updated.
        /// </summary>
        public void ApplySettings()
        {
            if (_camera != null && _settings != null)
            {
                _camera.fieldOfView = _settings.FieldOfView;
            }
        }

        /// <summary>
        /// Gets the base field of view from settings.
        /// Used by CameraEffectsController to calculate FOV modifiers (sprint, jump, etc.).
        /// </summary>
        /// <returns>The base FOV value, or 60f if settings are not initialized.</returns>
        public float GetBaseFOV()
        {
            return _settings?.FieldOfView ?? 60f;
        }

        /// <summary>
        /// Updates camera rotation based on current look input and player settings.
        /// Handles mouse sensitivity, Y-axis inversion, and angle clamping.
        /// Should be called every frame from PlayerController.Update().
        /// </summary>
        public void HandleCameraRotation()
        {
            if (_settings == null || InputManager.Instance == null) return;

            _lookInput = InputManager.Instance.LookInput;

            float mouseX = _lookInput.x * _settings.MouseSensitivity * 0.02f;
            float mouseY = _lookInput.y * _settings.MouseSensitivity * 0.02f;

            if (_settings.InvertYAxis)
            {
                mouseY = -mouseY;
            }
        
            _xRotation -= mouseY;

            _xRotation = Mathf.Clamp(_xRotation, -_settings.MaxLookUpAngle, _settings.MaxLookDownAngle);

            transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

            if (_playerBody != null)
            {
                _playerBody.Rotate(Vector3.up * mouseX);
            }
        }
    }
}