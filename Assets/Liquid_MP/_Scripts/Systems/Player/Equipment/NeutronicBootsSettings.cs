using UnityEngine;

namespace Liquid.Player.Equipment
{
    /// <summary>
    /// Configuration settings for the Neutronic Boots ceiling walking system.
    /// Controls detection range, activation timing, transition speeds, ceiling physics, and camera behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "NeutronicBootsSettings", menuName = "Liquid/Equipment/Neutronic Boots Settings")]
    public class NeutronicBootsSettings : ScriptableObject
    {
        [Header("Detection")]
        [Tooltip("Maximum distance to detect valid ceiling surfaces above the player")]
        [SerializeField] private float _maxCeilingDetectionDistance = 3f;
        
        [Tooltip("Detection distance while on ceiling for physics validation")]
        [SerializeField] private float _ceilingDetectionDistance = 2.5f;
        
        [Tooltip("Layer mask for surfaces that can be walked on with neutronic boots")]
        [SerializeField] private LayerMask _ceilingWalkableLayer;
        
        [Tooltip("Radius of the sphere cast used for ceiling detection")]
        [SerializeField] private float _detectionRadius = 0.3f;

        [Header("Activation")]
        [Tooltip("How long the player must hold jump to activate ceiling walk (in seconds)")]
        [SerializeField] private float _activationHoldTime = 1.5f;
        
        [Tooltip("Time window in seconds where releasing jump still counts as a tap. Holding longer than this will start the boot activation.")]
        [SerializeField] private float _jumpGracePeriod = 0.1f;

        [Header("Transitions")]
        [Tooltip("How long the rotation animation takes when mounting or dismounting from a ceiling (in seconds).")]
        [SerializeField] private float _rotationTransitionDuration = 0.25f;

        [Header("Ceiling Physics")]
        [Tooltip("Strength of the sticky force that pushes player toward ceiling surface")]
        [SerializeField] private float _stickyForceStrength = 15f;
        
        [Tooltip("Maximum sticky force magnitude to prevent excessive correction")]
        [SerializeField] private float _maxStickyForce = 20f;
        
        [Tooltip("Friction applied when no input (higher = stops faster)")]
        [Range(1f, 50f)]
        [SerializeField] private float _ceilingFriction = 15f;
        
        [Tooltip("Friction coefficient applied to movement with input (lower = more slip)")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _ceilingFrictionCoefficient = 0.85f;
        
        [Tooltip("How quickly player accelerates on ceiling")]
        [Range(1f, 20f)]
        [SerializeField] private float _ceilingAcceleration = 10f;

        [Header("Camera Behavior")]
        [Tooltip("Should the camera rotate with the player when walking on ceiling?")]
        [SerializeField] private bool _rotateCameraWithPlayer = true;
        
        [Tooltip("Should mouse look controls be inverted when on ceiling?")]
        [SerializeField] private bool _invertCameraControls = false;

        [Header("Movement on Ceiling")]
        [Tooltip("Movement speed multiplier while walking on ceiling (1.0 = normal speed)")]
        [SerializeField] private float _ceilingMovementSpeedMultiplier = 0.8f;
        
        [Tooltip("Should sprinting be allowed on ceiling?")]
        [SerializeField] private bool _allowSprintOnCeiling = false;
        
        [Tooltip("Grace period after activating ceiling walk before checking for ceiling contact (prevents immediate fall)")]
        [SerializeField] private float _ceilingContactGracePeriod = 0.5f;

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
        public float CeilingDetectionDistance => _ceilingDetectionDistance;
        public LayerMask CeilingWalkableLayer => _ceilingWalkableLayer;
        public float DetectionRadius => _detectionRadius;
        public float ActivationHoldTime => _activationHoldTime;
        public float JumpGracePeriod => _jumpGracePeriod;
        public float RotationTransitionDuration => _rotationTransitionDuration;
        public float StickyForceStrength => _stickyForceStrength;
        public float MaxStickyForce => _maxStickyForce;
        public float CeilingFriction => _ceilingFriction;
        public float CeilingFrictionCoefficient => _ceilingFrictionCoefficient;
        public float CeilingAcceleration => _ceilingAcceleration;
        public bool RotateCameraWithPlayer => _rotateCameraWithPlayer;
        public bool InvertCameraControls => _invertCameraControls;
        public float CeilingMovementSpeedMultiplier => _ceilingMovementSpeedMultiplier;
        public bool AllowSprintOnCeiling => _allowSprintOnCeiling;
        public float CeilingContactGracePeriod => _ceilingContactGracePeriod;
        public float UIFadeSpeed => _uiFadeSpeed;
        public bool ShowDebugGizmos => _showDebugGizmos;
        public Color DebugRayColor => _debugRayColor;

        #endregion
    }
}