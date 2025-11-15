using _Scripts.Core;
using _Scripts.Core.Managers;
using _Scripts.Systems.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    /// <summary>
    /// Manages the settings UI including camera sensitivity, FOV, and visual effect toggles.
    /// Uses temporary settings that can be applied or discarded without affecting the live game.
    /// Communicates with PlayerController to apply changes in real-time.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Camera Settings")]
        [SerializeField] private Slider _sensitivitySlider;
        [SerializeField] private TextMeshProUGUI _sensitivityText;
        [SerializeField] private Slider _fovSlider;
        [SerializeField] private TextMeshProUGUI _fovText;
        [SerializeField] private Toggle _invertYToggle;
        [SerializeField] private Toggle _cameraBobToggle;

        [Header("Buttons")]
        [SerializeField] private Button _applyButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _closeButton;

        #endregion

        #region Private Fields

        private PlayerSettings _temporarySettings;
        private PlayerController _playerController;

        #endregion

        #region Initialization

        private void Awake()
        {
            SetupListeners();
        }

        private void SetupListeners()
        {
            if (_sensitivitySlider != null) _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            if (_fovSlider != null) _fovSlider.onValueChanged.AddListener(OnFOVChanged);
            if (_invertYToggle != null) _invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
            if (_cameraBobToggle != null) _cameraBobToggle.onValueChanged.AddListener(OnCameraBobChanged);
            
            if (_applyButton != null) _applyButton.onClick.AddListener(OnApplyClicked);
            if (_resetButton != null) _resetButton.onClick.AddListener(OnResetClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(OnCloseClicked);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Shows the settings UI and initializes temporary settings from the current player settings.
        /// Allows the player to adjust settings without immediately affecting the live game.
        /// </summary>
        public void ShowSettings()
        {
            gameObject.SetActive(true);
            
            _temporarySettings = new PlayerSettings();
            _temporarySettings.CopyFrom(PlayerSettingsManager.Instance.CurrentSettings);

            UpdateUIFromSettings();
        }

        #endregion

        #region Settings Management

        private void UpdateUIFromSettings()
        {
            if (_sensitivitySlider != null)
            {
                _sensitivitySlider.value = _temporarySettings.MouseSensitivity;
                UpdateSensitivityText(_temporarySettings.MouseSensitivity);
            }
            
            if (_fovSlider != null)
            {
                _fovSlider.value = _temporarySettings.FieldOfView;
                UpdateFOVText(_temporarySettings.FieldOfView);
            }
            
            if (_invertYToggle != null)
            {
                _invertYToggle.isOn = _temporarySettings.InvertYAxis;
            }
            
            if (_cameraBobToggle != null)
            {
                _cameraBobToggle.isOn = _temporarySettings.EnableCameraBob;
            }
        }

        #endregion

        #region Button Handlers

        private void OnApplyClicked()
        {
            // Re-acquire player controller reference
            if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentPlayer != null)
            {
                _playerController = PlayerManager.Instance.CurrentPlayer;
            }
            else
            {
                _playerController = null;
            }

            // Copy temporary settings to live settings
            PlayerSettingsManager.Instance.CurrentSettings.CopyFrom(_temporarySettings);

            // Save settings to disk
            PlayerSettingsManager.Instance.SaveSettings();

            // Refresh active player components
            if (_playerController != null)
            {
                _playerController.OnSettingsUpdated();
            }

            Debug.Log("[SettingsUI] Settings applied and saved.");
        }

        private void OnResetClicked()
        {
            _temporarySettings.ResetToDefaults();
            UpdateUIFromSettings();
        }

        private void OnCloseClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Gameplay);
            }
        }

        #endregion

        #region Slider/Toggle Handlers

        private void OnSensitivityChanged(float value)
        {
            _temporarySettings.MouseSensitivity = value;
            UpdateSensitivityText(value);
        }

        private void OnFOVChanged(float value)
        {
            _temporarySettings.FieldOfView = value;
            UpdateFOVText(value);
        }

        private void OnInvertYChanged(bool value)
        {
            _temporarySettings.InvertYAxis = value;
        }

        private void OnCameraBobChanged(bool value)
        {
            _temporarySettings.EnableCameraBob = value;
        }

        #endregion

        #region UI Updates

        private void UpdateSensitivityText(float value)
        {
            if (_sensitivityText != null) _sensitivityText.text = $"Sensitivity: {value:F2}";
        }

        private void UpdateFOVText(float value)
        {
            if (_fovText != null) _fovText.text = $"FOV: {value:F0}°";
        }

        #endregion
    }
}