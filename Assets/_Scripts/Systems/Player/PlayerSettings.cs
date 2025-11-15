using UnityEngine;

namespace _Scripts.Systems.Player
{
    /// <summary>
    /// Data class that holds all configurable and non-configurable player settings.
    /// User-configurable settings (camera) can be modified via the settings menu and persisted to PlayerPrefs.
    /// Non-configurable settings (movement, jump) are determined by game design.
    /// </summary>
    [System.Serializable]
    public class PlayerSettings
    {
        #region Camera Settings

        [Tooltip("Mouse/look sensitivity multiplier. Higher values = faster camera rotation. User-configurable.")]
        public float MouseSensitivity = 2f;

        [Tooltip("Camera field of view in degrees. User-configurable.")]
        public float FieldOfView = 60f;

        [Tooltip("If true, the Y-axis (vertical look) is inverted. User-configurable.")]
        public bool InvertYAxis;

        [Tooltip("If true, head bob effects are applied when moving. User-configurable.")]
        public bool EnableCameraBob = true;

        [Tooltip("Maximum angle in degrees the camera can look upward.")]
        public float MaxLookUpAngle = 80f;

        [Tooltip("Maximum angle in degrees the camera can look downward.")]
        public float MaxLookDownAngle = 80f;

        #endregion

        #region Public Methods

        /// <summary>
        /// Copies user-configurable settings from another PlayerSettings instance.
        /// Only copies settings that can be modified via the settings menu (camera-related).
        /// </summary>
        /// <param name="other">The PlayerSettings instance to copy from.</param>
        public void CopyFrom(PlayerSettings other)
        {
            MouseSensitivity = other.MouseSensitivity;
            FieldOfView = other.FieldOfView;
            InvertYAxis = other.InvertYAxis;
            EnableCameraBob = other.EnableCameraBob;
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