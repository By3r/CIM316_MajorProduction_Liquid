using _Scripts.Core;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    /// <summary>
    /// Manages the pause menu UI including visibility, button interactions, and navigation.
    /// Responds to game state changes to show/hide the pause menu.
    /// Handles Resume, Settings, Main Menu, and Quit button actions.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private bool _freezeGameOnPause = true;

        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private GameObject _settingsPanel;

        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _quitButton;

        [SerializeField] private SettingsUI _settingsUI;

        #endregion

        #region Initialization

        private void Awake()
        {
            SetupButtonListeners();
        }

        private void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.EventManager != null)
            {
                GameManager.Instance.EventManager.Subscribe<GameState>(GameEvents.OnGameStateChanged, HandleGameStateChanged);
            }
            HidePauseMenu();
        }

        private void SetupButtonListeners()
        {
            if (_resumeButton != null) _resumeButton.onClick.AddListener(OnResumeClicked);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            if (_quitButton != null) _quitButton.onClick.AddListener(OnQuitClicked);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Shows the pause menu UI and hides the settings panel.
        /// </summary>
        public void ShowPauseMenu()
        {
            if (_pausePanel != null) _pausePanel.SetActive(true);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
        }

        /// <summary>
        /// Hides both the pause menu and settings panel UIs.
        /// </summary>
        public void HidePauseMenu()
        {
            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
        }

        #endregion

        #region Event Handlers

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.Paused:
                    ShowPauseMenu();
                    Time.timeScale = _freezeGameOnPause ? 0f : 1f;
                    break;
                    
                case GameState.Gameplay:
                    HidePauseMenu();
                    Time.timeScale = 1f;
                    break;
            }
        }

        private void OnResumeClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Gameplay);
            }
        }

        private void OnSettingsClicked()
        {
            Time.timeScale = 1f;
            if (_pausePanel != null) _pausePanel.SetActive(false);

            if (_settingsUI != null) _settingsUI.ShowSettings();
            else if (_settingsPanel != null) _settingsPanel.SetActive(true);
        }

        private void OnMainMenuClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.MainMenu);
            }
        }

        private void OnQuitClicked()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (GameManager.Instance != null && GameManager.Instance.EventManager != null)
            {
                GameManager.Instance.EventManager.Unsubscribe<GameState>(GameEvents.OnGameStateChanged, HandleGameStateChanged);
            }
        }

        #endregion
    }
}