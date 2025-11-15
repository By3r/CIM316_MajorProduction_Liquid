using UnityEngine;

namespace _Scripts.Player
{
    /// <summary>
    /// Singleton manager that holds the current player settings and handles persistence.
    /// Initializes settings from PlayerPrefs on startup.
    /// Provides a central point for other systems to access and modify player settings.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class PlayerSettingsManager : MonoBehaviour
    {
        #region Singleton

        /// <summary>
        /// Singleton instance of the PlayerSettingsManager.
        /// </summary>
        public static PlayerSettingsManager Instance { get; private set; }

        #endregion

        #region Public API

        /// <summary>
        /// Gets the current player settings instance.
        /// Contains both user-configurable (camera) and non-configurable (movement, jump) settings.
        /// </summary>
        public Player.PlayerSettings CurrentSettings { get; private set; }

        /// <summary>
        /// Saves the current player settings to PlayerPrefs.
        /// Called by SettingsUI when the player applies settings changes.
        /// </summary>
        public void SaveSettings()
        {
            CurrentSettings.SaveToPlayerPrefs();
        }

        #endregion

        #region Initialization

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CurrentSettings = new Player.PlayerSettings();
            CurrentSettings.LoadFromPlayerPrefs();
        }

        #endregion
    }
}