using UnityEngine;

namespace _Scripts.Player
{
    /// <summary>
    /// Data class that holds all configurable and non-configurable player settings.
    /// User-configurable settings (camera) can be modified via the settings menu and persisted to PlayerPrefs.
    /// Non-configurable settings (movement, jump) are determined by game design.
    /// </summary>
    [System.Serializable]
    public class PlayerSettings
    {
        #region Movement Speed Settings (Not User-Configurable)

        /// <summary>
        /// Base walking speed in units per second.
        /// </summary>
        public float WalkSpeed = 5f;

        /// <summary>
        /// Sprint speed in units per second when the sprint key is held.
        /// </summary>
        public float SprintSpeed = 8f;

        /// <summary>
        /// Crouching speed in units per second.
        /// </summary>
        public float CrouchSpeed = 2.5f;

        #endregion

        #region Jump Settings (Not User-Configurable)

        /// <summary>
        /// The initial upward velocity applied when jumping.
        /// </summary>
        public float JumpForce = 5f;

        /// <summary>
        /// Downward acceleration in units per second squared. Typical value is -9.81f.
        /// </summary>
        public float Gravity = -9.81f;

        #endregion

        #region Camera Settings (User-Configurable)

        /// <summary>
        /// Mouse/look sensitivity multiplier. Higher values result in faster camera rotation.
        /// User-configurable via settings menu.
        /// </summary>
        public float MouseSensitivity = 2f;

        /// <summary>
        /// Camera field of view in degrees.
        /// User-configurable via settings menu.
        /// </summary>
        public float FieldOfView = 60f;

        /// <summary>
        /// If true, the Y-axis (vertical look) is inverted.
        /// User-configurable via settings menu.
        /// </summary>
        public bool InvertYAxis = false;

        /// <summary>
        /// If true, head bob effects are applied when moving.
        /// User-configurable via settings menu.
        /// </summary>
        public bool EnableCameraBob = true;

        /// <summary>
        /// Maximum angle in degrees the camera can look upward.
        /// </summary>
        public float MaxLookUpAngle = 80f;

        /// <summary>
        /// Maximum angle in degrees the camera can look downward.
        /// </summary>
        public float MaxLookDownAngle = 80f;

        #endregion

        #region Input Keys (Not User-Configurable)

        /// <summary>
        /// Key code for the sprint action.
        /// </summary>
        public KeyCode SprintKey = KeyCode.LeftShift;

        /// <summary>
        /// Key code for the crouch action.
        /// </summary>
        public KeyCode CrouchKey = KeyCode.LeftControl;

        /// <summary>
        /// Key code for the jump action.
        /// </summary>
        public KeyCode JumpKey = KeyCode.Space;

        /// <summary>
        /// Key code for the interact action.
        /// </summary>
        public KeyCode InteractKey = KeyCode.E;

        #endregion

        #region Public Methods

        /// <summary>
        /// Copies user-configurable settings from another PlayerSettings instance.
        /// Only copies settings that can be modified via the settings menu (camera-related).
        /// </summary>
        /// <param name="other">The PlayerSettings instance to copy from.</param>
        public void CopyFrom(PlayerSettings other)
        {
            this.MouseSensitivity = other.MouseSensitivity;
            this.FieldOfView = other.FieldOfView;
            this.InvertYAxis = other.InvertYAxis;
            this.EnableCameraBob = other.EnableCameraBob;
        }

        /// <summary>
        /// Loads user-configurable settings from Unity's PlayerPrefs.
        /// Settings not found in PlayerPrefs will use their default values.
        /// Typically called during initialization by PlayerSettingsManager.
        /// </summary>
        public void LoadFromPlayerPrefs()
        {
            MouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 2f);
            FieldOfView = PlayerPrefs.GetFloat("FieldOfView", 60f);
            InvertYAxis = PlayerPrefs.GetInt("InvertYAxis", 0) == 1;
            EnableCameraBob = PlayerPrefs.GetInt("EnableCameraBob", 1) == 1;
        }

        /// <summary>
        /// Saves user-configurable settings to Unity's PlayerPrefs for persistence.
        /// Called by SettingsUI when the player applies new settings.
        /// </summary>
        public void SaveToPlayerPrefs()
        {
            PlayerPrefs.SetFloat("MouseSensitivity", MouseSensitivity);
            PlayerPrefs.SetFloat("FieldOfView", FieldOfView);
            PlayerPrefs.SetInt("InvertYAxis", InvertYAxis ? 1 : 0);
            PlayerPrefs.SetInt("EnableCameraBob", EnableCameraBob ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Resets all settings to their default values.
        /// Called by SettingsUI when the player clicks the "Reset to Defaults" button.
        /// </summary>
        public void ResetToDefaults()
        {
            WalkSpeed = 2f;
            SprintSpeed = 6f;
            CrouchSpeed = 2.5f;
            JumpForce = 1f;
            Gravity = -25f;
            MouseSensitivity = 2.5f;
            FieldOfView = 60f;
            InvertYAxis = false;
            EnableCameraBob = true;
            MaxLookUpAngle = 80f;
            MaxLookDownAngle = 80f;
        }

        #endregion
    }
}