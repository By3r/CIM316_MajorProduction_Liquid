using UnityEngine;

namespace Liquid.Player.Equipment
{
    /// <summary>
    /// Configuration settings for the Neutronic Boots ceiling walking system.
    /// Controls detection range, activation timing, transition speeds, and camera behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "NeutronicBootsSettings", menuName = "Liquid/Equipment/Neutronic Boots Settings")]
    public class NeutronicBootsSettings : ScriptableObject
    {
        [Header("Detection")]
        [Tooltip("Maximum distance to detect valid ceiling surfaces above the player")]
        [SerializeField] private float _maxCeilingDetectionDistance = 3f;
        
        [Tooltip("Layer mask for surfaces that can be walked on with neutronic boots")]
        [SerializeField] private LayerMask _ceilingWalkableLayer;
        
        [Tooltip("Radius of the sphere cast used for ceiling detection")]
        [SerializeField] private float _detectionRadius = 0.3f;

        [Header("Activation")]
        [Tooltip("How long the player must hold jump to activate ceiling walk (in seconds)")]
        [SerializeField] private float _activationHoldTime = 1.5f;
        
        [Tooltip("Speed at which the player rotates from floor to ceiling orientation")]
        [SerializeField] private float _rotationTransitionSpeed = 3f;
        
        [Tooltip("Speed at which the player moves toward the ceiling during activation")]
        [SerializeField] private float _ceilingApproachSpeed = 2f;

        [Header("Camera Behavior")]
        [Tooltip("Should the camera rotate with the player when walking on ceiling?")]
        [SerializeField] private bool _rotateCameraWithPlayer = true;
        
        [Tooltip("Speed at which camera rotates during ceiling walk transition")]
        [SerializeField] private float _cameraRotationSpeed = 3f;
        
        [Tooltip("Should mouse look controls be inverted when on ceiling?")]
        [SerializeField] private bool _invertCameraControls = false;

        [Header("Movement on Ceiling")]
        [Tooltip("Movement speed multiplier while walking on ceiling (1.0 = normal speed)")]
        [SerializeField] private float _ceilingMovementSpeedMultiplier = 0.8f;
        
        [Tooltip("Should sprinting be allowed on ceiling?")]
        [SerializeField] private bool _allowSprintOnCeiling = false;
        
        [Tooltip("Grace period after activating ceiling walk before checking for ceiling contact (prevents immediate fall)")]
        [SerializeField] private float _ceilingContactGracePeriod = 0.5f;

        [Header("Dismount")]
        [Tooltip("Speed at which player falls when ceiling surface ends")]
        [SerializeField] private float _dismountFallSpeed = 2f;

        [Header("UI")]
        [Tooltip("Speed at which UI elements fade in/out")]
        [SerializeField] private float _uiFadeSpeed = 5f;

        [Header("Debug")]
        [Tooltip("Draw debug gizmos for ceiling detection")]
        [SerializeField] private bool _showDebugGizmos = true;
        
        [Tooltip("Color for ceiling detection rays in debug mode")]
        [SerializeField] private Color _debugRayColor = Color.cyan;

        #region Public Properties

        public float MaxCeilingDetectionDistance => _maxCeilingDetectionDistance;
        public LayerMask CeilingWalkableLayer => _ceilingWalkableLayer;
        public float DetectionRadius => _detectionRadius;
        public float ActivationHoldTime => _activationHoldTime;
        public float RotationTransitionSpeed => _rotationTransitionSpeed;
        public float CeilingApproachSpeed => _ceilingApproachSpeed;
        public bool RotateCameraWithPlayer => _rotateCameraWithPlayer;
        public float CameraRotationSpeed => _cameraRotationSpeed;
        public bool InvertCameraControls => _invertCameraControls;
        public float CeilingMovementSpeedMultiplier => _ceilingMovementSpeedMultiplier;
        public bool AllowSprintOnCeiling => _allowSprintOnCeiling;
        public float CeilingContactGracePeriod => _ceilingContactGracePeriod;
        public float DismountFallSpeed => _dismountFallSpeed;
        public float UIFadeSpeed => _uiFadeSpeed;
        public bool ShowDebugGizmos => _showDebugGizmos;
        public Color DebugRayColor => _debugRayColor;

        #endregion
    }
}