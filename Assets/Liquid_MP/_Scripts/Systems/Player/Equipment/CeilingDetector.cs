using UnityEngine;

namespace Liquid.Player.Equipment
{
    /// <summary>
    /// Detects and validates ceiling surfaces that can be walked on with Neutronic Boots.
    /// Performs continuous upward raycasting and provides data about the detected ceiling.
    /// </summary>
    public class CeilingDetector : MonoBehaviour
    {
        private NeutronicBootsSettings _settings;
        private Transform _playerTransform;
        
        private bool _isCeilingAvailable;
        private RaycastHit _lastCeilingHit;
        private Vector3 _ceilingNormal;
        private float _distanceToCeiling;

        #region Public Properties

        /// <summary>
        /// Gets whether a valid ceiling surface is currently above the player.
        /// </summary>
        public bool IsCeilingAvailable => _isCeilingAvailable;

        /// <summary>
        /// Gets the raycast hit information for the detected ceiling.
        /// </summary>
        public RaycastHit LastCeilingHit => _lastCeilingHit;

        /// <summary>
        /// Gets the normal vector of the detected ceiling surface.
        /// </summary>
        public Vector3 CeilingNormal => _ceilingNormal;

        /// <summary>
        /// Gets the distance from the player to the detected ceiling.
        /// </summary>
        public float DistanceToCeiling => _distanceToCeiling;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the ceiling detector with settings and player transform.
        /// </summary>
        /// <param name="settings">The neutronic boots configuration settings.</param>
        /// <param name="playerTransform">The transform of the player character.</param>
        public void Initialize(NeutronicBootsSettings settings, Transform playerTransform)
        {
            _settings = settings;
            _playerTransform = playerTransform;
        }

        #endregion

        #region Detection

        /// <summary>
        /// Performs ceiling detection using a sphere cast upward from the player.
        /// Should be called every frame to maintain up-to-date ceiling information.
        /// </summary>
        public void DetectCeiling()
        {
            if (_settings == null || _playerTransform == null)
            {
                _isCeilingAvailable = false;
                return;
            }

            Vector3 rayOrigin = _playerTransform.position + Vector3.up * 0.5f;
            Vector3 rayDirection = _playerTransform.up;

            // Perform sphere cast to detect ceiling
            if (Physics.SphereCast(
                rayOrigin,
                _settings.DetectionRadius,
                rayDirection,
                out RaycastHit hit,
                _settings.MaxCeilingDetectionDistance,
                _settings.CeilingWalkableLayer,
                QueryTriggerInteraction.Ignore))
            {
                _lastCeilingHit = hit;
                _ceilingNormal = hit.normal;
                _distanceToCeiling = hit.distance;
                _isCeilingAvailable = true;
            }
            else
            {
                _isCeilingAvailable = false;
                _distanceToCeiling = float.MaxValue;
            }
        }

        /// <summary>
        /// Checks if the player is still on a valid ceiling surface (for continuous validation).
        /// </summary>
        /// <param name="playerFeetPosition">The position of the player's feet (or attachment point).</param>
        /// <param name="checkDistance">Distance to check above the player's feet.</param>
        /// <returns>True if still on valid ceiling, false otherwise.</returns>
        public bool IsStillOnCeiling(Vector3 playerFeetPosition, float checkDistance = 0.2f)
        {
            if (_settings == null) return false;

            // Cast a short ray upward (from ceiling perspective) to check if still on surface
            return Physics.SphereCast(
                playerFeetPosition,
                _settings.DetectionRadius * 0.5f,
                Vector3.up,
                out _,
                checkDistance,
                _settings.CeilingWalkableLayer,
                QueryTriggerInteraction.Ignore);
        }

        #endregion

        #region Debug Visualization

        private void OnDrawGizmos()
        {
            if (_settings == null || !_settings.ShowDebugGizmos || _playerTransform == null) return;

            Vector3 rayOrigin = _playerTransform.position + Vector3.up * 0.5f;
            Vector3 rayDirection = _playerTransform.up;

            // Draw detection ray
            Gizmos.color = _isCeilingAvailable ? Color.green : _settings.DebugRayColor;
            Gizmos.DrawRay(rayOrigin, rayDirection * _settings.MaxCeilingDetectionDistance);

            // Draw detection sphere at origin
            Gizmos.DrawWireSphere(rayOrigin, _settings.DetectionRadius);

            // If ceiling detected, draw hit point and normal
            if (_isCeilingAvailable)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_lastCeilingHit.point, _settings.DetectionRadius);
                
                Gizmos.color = Color.red;
                Gizmos.DrawRay(_lastCeilingHit.point, _ceilingNormal * 0.5f);
            }
        }

        #endregion
    }
}