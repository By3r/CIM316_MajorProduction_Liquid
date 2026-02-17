using _Scripts.Core.Managers;
using _Scripts.Systems.Player;
using UnityEngine;

namespace _Scripts.Systems.Weapon
{
    /// <summary>
    /// Handles all viewmodel visual motion: mouse sway, movement bob, and ADS position/FOV transitions.
    /// Lives on the ViewmodelRoot GameObject (child of PlayerCamera), created by WeaponManager at runtime.
    ///
    /// Each frame it reads the current weapon's WeaponDataSO for hip/ADS positions and computes:
    ///   1. Base position — lerp between hipPosition and adsPosition based on ADS state
    ///   2. Mouse sway — subtle follow of mouse delta, reduced when ADS
    ///   3. Movement bob — sine wave based on player speed, increased when sprinting, reduced when ADS
    ///
    /// IMPORTANT: The final transform is applied to THIS transform (the ViewmodelRoot parent),
    /// NOT to the weapon child. The weapon child's localPosition/localRotation belong entirely
    /// to the Animator. If we wrote to the weapon child, LateUpdate would overwrite the
    /// Animator's output every frame, causing animations to appear to not play.
    ///
    /// WeaponManager tells this component which weapon is active and whether we're aiming.
    /// This component does not handle input — it only reacts to state set by WeaponManager.
    /// </summary>
    public class ViewmodelMotion : MonoBehaviour
    {
        #region Serialized Fields

        [Header("-- Mouse Sway --")]
        [Tooltip("How much the weapon sways with mouse movement.")]
        [SerializeField] private float _swayAmount = 0.02f;
        [Tooltip("Maximum sway offset in any direction.")]
        [SerializeField] private float _maxSway = 0.06f;
        [Tooltip("How fast the sway smoothly returns to center.")]
        [SerializeField] private float _swaySmoothness = 8f;
        [Tooltip("Sway multiplier when ADS (0.2 = 80% less sway for stability).")]
        [SerializeField] private float _adsSwayMultiplier = 0.2f;

        [Header("-- Movement Bob --")]
        [Tooltip("Frequency of the walk bob cycle.")]
        [SerializeField] private float _bobFrequency = 8f;
        [Tooltip("Horizontal bob amplitude at walk speed.")]
        [SerializeField] private float _bobHorizontalAmount = 0.01f;
        [Tooltip("Vertical bob amplitude at walk speed.")]
        [SerializeField] private float _bobVerticalAmount = 0.015f;
        [Tooltip("Bob amplitude multiplier when sprinting.")]
        [SerializeField] private float _sprintBobMultiplier = 1.6f;
        [Tooltip("Minimum player speed before bob kicks in.")]
        [SerializeField] private float _bobSpeedThreshold = 0.5f;
        [Tooltip("Bob multiplier when ADS (0.3 = 70% less bob for stability).")]
        [SerializeField] private float _adsBobMultiplier = 0.3f;

        #endregion

        #region Private Fields

        // References (set by WeaponManager via Initialize)
        private MovementController _movementController;
        private Camera _playerCamera;
        private float _baseFOV;

        // Current weapon state (set by WeaponManager via SetActiveWeapon / SetAiming)
        private WeaponDataSO _weaponData;
        private bool _isAiming;
        private bool _isActive;    // false when holstered or no weapon equipped

        // Sway runtime
        private Vector3 _swayOffset;

        // Bob runtime
        private float _bobTimer;
        private Vector3 _bobOffset;

        // ADS runtime
        private float _adsLerpT;   // 0 = hip, 1 = fully ADS

        #endregion

        #region Properties

        /// <summary>Current ADS lerp value (0 = hip, 1 = fully ADS). Useful for UI or other systems.</summary>
        public float ADSLerp => _adsLerpT;

        #endregion

        #region Initialization

        /// <summary>
        /// Called by WeaponManager after creating the ViewmodelRoot.
        /// Provides references this component needs but can't find on its own.
        /// </summary>
        public void Initialize(MovementController movementController, Camera playerCamera)
        {
            _movementController = movementController;
            _playerCamera = playerCamera;

            if (_playerCamera != null)
            {
                _baseFOV = _playerCamera.fieldOfView;
            }
        }

        #endregion

        #region Public API -- Called by WeaponManager

        /// <summary>
        /// Tells ViewmodelMotion which weapon data to read positions from.
        /// Call when a new weapon is drawn or re-activated.
        /// Pass null to clear (e.g., on holster).
        ///
        /// Note: We no longer take a weapon Transform reference because we apply
        /// motion to this.transform (ViewmodelRoot), not to the weapon child.
        /// The weapon child's transform belongs entirely to the Animator.
        /// </summary>
        public void SetActiveWeapon(Transform weaponTransform, WeaponDataSO weaponData)
        {
            _weaponData = weaponData;
            _isActive = weaponData != null;

            // Reset motion state on weapon change
            _swayOffset = Vector3.zero;
            _bobOffset = Vector3.zero;
            _bobTimer = 0f;
            _adsLerpT = 0f;
            _isAiming = false;
        }

        /// <summary>
        /// Sets whether the player is currently aiming down sights.
        /// Called by WeaponManager when aim state changes.
        /// </summary>
        public void SetAiming(bool aiming)
        {
            _isAiming = aiming;
        }

        #endregion

        #region Update

        private void LateUpdate()
        {
            if (!_isActive || _weaponData == null) return;

            UpdateSway();
            UpdateBob();
            UpdateADSLerp();
            ApplyTransform();
        }

        #endregion

        #region Sway

        /// <summary>
        /// Calculates weapon sway from mouse delta.
        /// The weapon subtly lags behind mouse movement for a natural weight feel.
        /// </summary>
        private void UpdateSway()
        {
            Vector2 lookInput = InputManager.Instance != null ? InputManager.Instance.LookInput : Vector2.zero;

            float targetX = -lookInput.x * _swayAmount;
            float targetY = -lookInput.y * _swayAmount;

            targetX = Mathf.Clamp(targetX, -_maxSway, _maxSway);
            targetY = Mathf.Clamp(targetY, -_maxSway, _maxSway);

            float adsReduction = _isAiming ? _adsSwayMultiplier : 1f;

            Vector3 target = new Vector3(targetX, targetY, 0f) * adsReduction;
            _swayOffset = Vector3.Lerp(_swayOffset, target, Time.deltaTime * _swaySmoothness);
        }

        #endregion

        #region Bob

        /// <summary>
        /// Calculates weapon bob from player movement.
        /// Uses a sine wave with separate horizontal/vertical amplitudes,
        /// scaled by speed and modified by sprint/ADS states.
        /// </summary>
        private void UpdateBob()
        {
            if (_movementController == null)
            {
                _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, Time.deltaTime * _swaySmoothness);
                return;
            }

            float speed = _movementController.CurrentSpeed;
            bool isGrounded = _movementController.IsGrounded;
            bool isSprinting = _movementController.IsSprinting;

            if (!isGrounded || speed < _bobSpeedThreshold)
            {
                _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, Time.deltaTime * _swaySmoothness);
                return;
            }

            float speedFactor = Mathf.Clamp01(speed / _movementController.MaxSpeed);
            float bobMultiplier = isSprinting ? _sprintBobMultiplier : 1f;

            if (_isAiming) bobMultiplier *= _adsBobMultiplier;

            _bobTimer += Time.deltaTime * _bobFrequency * speedFactor;

            float horizontalBob = Mathf.Sin(_bobTimer) * _bobHorizontalAmount * bobMultiplier * speedFactor;
            float verticalBob = Mathf.Sin(_bobTimer * 2f) * _bobVerticalAmount * bobMultiplier * speedFactor;

            Vector3 targetBob = new Vector3(horizontalBob, verticalBob, 0f);
            _bobOffset = Vector3.Lerp(_bobOffset, targetBob, Time.deltaTime * _swaySmoothness);
        }

        #endregion

        #region ADS Transition

        /// <summary>
        /// Smoothly moves the ADS lerp toward 0 (hip) or 1 (ADS) based on current aim state.
        /// The speed comes from the weapon's WeaponDataSO.adsTransitionSpeed.
        /// </summary>
        private void UpdateADSLerp()
        {
            float targetT = _isAiming ? 1f : 0f;
            float speed = _weaponData.adsTransitionSpeed;

            _adsLerpT = Mathf.MoveTowards(_adsLerpT, targetT, Time.deltaTime * speed);
        }

        #endregion

        #region Apply Final Transform

        /// <summary>
        /// Combines hip/ADS position lerp + sway + bob and applies to THIS transform
        /// (the ViewmodelRoot). The weapon child sits underneath and its localPosition/
        /// localRotation are controlled entirely by the Animator — we never touch them.
        ///
        /// This is the key to avoiding the "transform fight": Unity's Animator writes
        /// the weapon child's local transform first, then LateUpdate runs here and
        /// sets the PARENT's transform. The Animator's animation plays correctly in
        /// local space relative to the moving parent.
        /// </summary>
        private void ApplyTransform()
        {
            // Lerp between hip and ADS positions from weapon data
            Vector3 basePos = Vector3.Lerp(
                _weaponData.hipPosition,
                _weaponData.adsPosition,
                _adsLerpT
            );

            Quaternion baseRot = Quaternion.Slerp(
                Quaternion.Euler(_weaponData.hipRotation),
                Quaternion.Euler(_weaponData.adsRotation),
                _adsLerpT
            );

            // Add sway and bob offsets
            Vector3 finalPos = basePos + _swayOffset + _bobOffset;

            // Apply to ViewmodelRoot (this.transform), NOT the weapon child
            transform.localPosition = finalPos;
            transform.localRotation = baseRot;

            // ADS FOV transition (adsFOV is an offset: negative = zoom in, positive = zoom out)
            if (_playerCamera != null && Mathf.Abs(_weaponData.adsFOV) > 0.01f)
            {
                float targetFOV = _baseFOV + _weaponData.adsFOV;
                _playerCamera.fieldOfView = Mathf.Lerp(_baseFOV, targetFOV, _adsLerpT);
            }
            else if (_playerCamera != null)
            {
                // No offset configured — restore base FOV when transitioning out of ADS
                _playerCamera.fieldOfView = Mathf.Lerp(_playerCamera.fieldOfView, _baseFOV, Time.deltaTime * _weaponData.adsTransitionSpeed);
            }
        }

        #endregion
    }
}
