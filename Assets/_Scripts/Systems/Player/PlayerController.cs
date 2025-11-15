using _Scripts.Core.Managers;
using UnityEngine;

namespace _Scripts.Systems.Player
{
    /// <summary>
    /// Central controller that orchestrates all player systems including movement, camera control, interaction, and visual effects.
    /// Manages component initialization, coordinates frame updates, and relays settings changes to child systems.
    /// Registers itself with PlayerManager during instantiation.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        #region Private Fields

        private PlayerSettings _mySettings;

        private MovementController _movementController;
        private CameraController _cameraController;
        private CameraEffectsController _cameraEffectsController;
        private InteractionController _interactionController;

        [SerializeField] private Transform _cameraTransform;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the interaction controller for this player.
        /// </summary>
        public InteractionController InteractionController => _interactionController;

        #endregion

        #region Initialization

        private void Awake()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.RegisterPlayer(this);
            }
            else
            {
                Debug.LogError("[PlayerController] Could not find PlayerManager instance to register with!");
            }
            
            InitializeComponents();
        }

        private void Start()
        {
            _mySettings = PlayerSettingsManager.Instance.CurrentSettings;

            _movementController.Initialize(_mySettings);
            _cameraController.Initialize(transform, _mySettings);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Gameplay);
            }
        }

        private void InitializeComponents()
        {
            // Movement Controller
            _movementController = GetComponent<MovementController>();
            if (_movementController == null) _movementController = gameObject.AddComponent<MovementController>();

            // Camera Setup
            if (_cameraTransform == null)
            {
                _cameraTransform = transform.Find("PlayerCamera");
                if (_cameraTransform == null)
                {
                    GameObject cameraObj = new GameObject("PlayerCamera");
                    cameraObj.transform.SetParent(transform);
                    cameraObj.transform.localPosition = new Vector3(0, 0.6f, 0);
                    cameraObj.AddComponent<Camera>();
                    cameraObj.AddComponent<AudioListener>();
                    _cameraTransform = cameraObj.transform;
                }
            }

            // Camera Controller
            _cameraController = _cameraTransform.GetComponent<CameraController>();
            if (_cameraController == null) _cameraController = _cameraTransform.gameObject.AddComponent<CameraController>();

            // Camera Effects Controller
            _cameraEffectsController = _cameraTransform.GetComponent<CameraEffectsController>();
            if (_cameraEffectsController == null)
            {
                _cameraEffectsController = _cameraTransform.gameObject.AddComponent<CameraEffectsController>();
            }

            // Interaction Controller
            _interactionController = GetComponent<InteractionController>();
            if (_interactionController == null)
            {
                _interactionController = gameObject.AddComponent<InteractionController>();
            }
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            _cameraController.HandleCameraRotation();
            _movementController.HandleMovement();

            if (_cameraEffectsController != null && _movementController != null)
            {
                _cameraEffectsController.UpdateEffects(
                    _movementController.CurrentSpeed,
                    _movementController.MaxSpeed,
                    _movementController.IsGrounded,
                    _movementController.IsSprinting,
                    _movementController.IsCrouching,
                    _movementController.IsJumping,
                    _movementController.IsWalkingToggled,
                    _mySettings.EnableCameraBob
                );
            }

            // Note: InteractionController updates itself in its own Update()
            // No need to call anything here, but  can access its state via the property
        }

        #endregion

        #region Public API

        /// <summary>
        /// Called when player settings are updated from the settings menu.
        /// Reinitializes movement and camera controllers with the new settings.
        /// Should be called by SettingsUI or other systems that modify player settings.
        /// </summary>
        public void OnSettingsUpdated()
        {
            _mySettings = PlayerSettingsManager.Instance.CurrentSettings;

            if (_movementController != null)
            {
                _movementController.Initialize(_mySettings);
            }
            if (_cameraController != null)
            {
                _cameraController.Initialize(transform, _mySettings);
            }
        }

        #endregion
    }
}