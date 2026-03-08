using _Scripts.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Can disable specified script components if they are not null and the settings panel is on.
// Includes the navigation between different options such as Controls, Graphics and Audio.
// This script is responsible for managing the settings panel and the UI appearance and other settings logic that other scripts might benefit from.
public class SettingsManager : MonoBehaviour
{
    [System.Serializable]
    private class SettingsTabVisuals
    {
        [Header("Tab")]
        public Button Button;
        public GameObject Panel;

        [Header("Visual States")]
        public GameObject HighlightObject;
        public GameObject PressedObjectA;
        public GameObject PressedObjectB;
    }

    private enum SettingsCategory
    {
        Controls,
        Graphics,
        Audio
    }

    [Header("References")]
    [SerializeField] private MainMenuManager mainMenuManager;
    [SerializeField] private MonoBehaviour[] scriptsToDisableWhileOpen;
    [SerializeField] private AudioMixer audioMixer;

    [Header("Category Tabs")]
    [SerializeField] private SettingsTabVisuals controlsTab;
    [SerializeField] private SettingsTabVisuals graphicsTab;
    [SerializeField] private SettingsTabVisuals audioTab;

    [Header("Controls")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text sensitivityValueText;
    [SerializeField] private Toggle invertYToggle;

    [Header("Graphics")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeText;
    [SerializeField] private TMP_Text musicVolumeText;
    [SerializeField] private TMP_Text sfxVolumeText;

    [Header("Buttons")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button backButton;

    private GameSettingsData temporarySettings;
    private Resolution[] availableResolutions;
    private SettingsCategory currentCategory = SettingsCategory.Controls;

    private void Awake()
    {
        SetupListeners();
        CacheResolutions();
        ForceTabVisualRefresh();
    }

    private void Start()
    {
        if (SettingsDataManager.Instance != null)
        {
            ApplyLiveSettings(SettingsDataManager.Instance.CurrentSettings);
        }
    }

    public void OpenSettings()
    {
        if (SettingsDataManager.Instance == null)
        {
            Debug.LogError("SettingsDataManager is missing.");
            return;
        }

        temporarySettings = new GameSettingsData();
        temporarySettings.CopyFrom(SettingsDataManager.Instance.CurrentSettings);

        SetScriptsEnabled(false);
        PopulateGraphicsDropdowns();
        UpdateUIFromSettings();

        ShowControlsCategory();

        if (controlsTab.Button != null)
        {
            EventSystem.current?.SetSelectedGameObject(controlsTab.Button.gameObject);
        }
    }

    private void SetupListeners()
    {
        if (controlsTab.Button != null) controlsTab.Button.onClick.AddListener(ShowControlsCategory);
        if (graphicsTab.Button != null) graphicsTab.Button.onClick.AddListener(ShowGraphicsCategory);
        if (audioTab.Button != null) audioTab.Button.onClick.AddListener(ShowAudioCategory);

        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        if (invertYToggle != null) invertYToggle.onValueChanged.AddListener(OnInvertYChanged);

        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

        if (applyButton != null) applyButton.onClick.AddListener(OnApplyPressed);
        if (resetButton != null) resetButton.onClick.AddListener(OnResetPressed);
        if (backButton != null) backButton.onClick.AddListener(OnBackPressed);
    }

    private void CacheResolutions()
    {
        availableResolutions = Screen.resolutions;
    }

    private void PopulateGraphicsDropdowns()
    {
        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();

            System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>();

            for (int i = 0; i < availableResolutions.Length; i++)
            {
                Resolution resolution = availableResolutions[i];
                options.Add($"{resolution.width} x {resolution.height} @ {resolution.refreshRateRatio.value:0}Hz");
            }

            resolutionDropdown.AddOptions(options);

            int safeIndex = Mathf.Clamp(temporarySettings.ResolutionIndex, 0, Mathf.Max(0, availableResolutions.Length - 1));
            resolutionDropdown.value = safeIndex;
            resolutionDropdown.RefreshShownValue();
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));

            int safeQualityIndex = Mathf.Clamp(temporarySettings.QualityIndex, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            qualityDropdown.value = safeQualityIndex;
            qualityDropdown.RefreshShownValue();
        }
    }

    private void UpdateUIFromSettings()
    {
        if (temporarySettings == null)
        {
            return;
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = temporarySettings.MouseSensitivity;
            UpdateSensitivityText(temporarySettings.MouseSensitivity);
        }

        if (invertYToggle != null)
        {
            invertYToggle.isOn = temporarySettings.InvertYAxis;
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = temporarySettings.Fullscreen;
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = temporarySettings.MasterVolume;
            UpdateMasterVolumeText(temporarySettings.MasterVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = temporarySettings.MusicVolume;
            UpdateMusicVolumeText(temporarySettings.MusicVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = temporarySettings.SfxVolume;
            UpdateSfxVolumeText(temporarySettings.SfxVolume);
        }
    }

    private void ShowControlsCategory()
    {
        currentCategory = SettingsCategory.Controls;
        SetCategoryVisibility(SettingsCategory.Controls);
    }

    private void ShowGraphicsCategory()
    {
        currentCategory = SettingsCategory.Graphics;
        SetCategoryVisibility(SettingsCategory.Graphics);
    }

    private void ShowAudioCategory()
    {
        currentCategory = SettingsCategory.Audio;
        SetCategoryVisibility(SettingsCategory.Audio);
    }

    private void SetCategoryVisibility(SettingsCategory activeCategory)
    {
        SetTabState(controlsTab, activeCategory == SettingsCategory.Controls);
        SetTabState(graphicsTab, activeCategory == SettingsCategory.Graphics);
        SetTabState(audioTab, activeCategory == SettingsCategory.Audio);
    }

    private void SetTabState(SettingsTabVisuals tab, bool isActive)
    {
        if (tab.Panel != null)
        {
            tab.Panel.SetActive(isActive);
        }

        if (tab.HighlightObject != null)
        {
            tab.HighlightObject.SetActive(isActive);
        }

        if (tab.PressedObjectA != null)
        {
            tab.PressedObjectA.SetActive(isActive);
        }

        if (tab.PressedObjectB != null)
        {
            tab.PressedObjectB.SetActive(isActive);
        }
    }

    private void ForceTabVisualRefresh()
    {
        SetCategoryVisibility(currentCategory);
    }

    private void OnSensitivityChanged(float value)
    {
        if (temporarySettings == null)
        {
            return;
        }

        temporarySettings.MouseSensitivity = value;
        UpdateSensitivityText(value);
    }

    private void OnInvertYChanged(bool value)
    {
        if (temporarySettings == null)
        {
            return;
        }

        temporarySettings.InvertYAxis = value;
    }

    private void OnResolutionChanged(int index)
    {
        if (temporarySettings == null)
        {
            return;
        }

        temporarySettings.ResolutionIndex = index;
    }

    private void OnQualityChanged(int index)
    {
        if (temporarySettings == null)
        {
            return;
        }

        temporarySettings.QualityIndex = index;
    }

    private void OnFullscreenChanged(bool value)
    {
        if (temporarySettings == null)
        {
            return;
        }

        temporarySettings.Fullscreen = value;
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (temporarySettings == null)
        {
            return;
        }

        temporarySettings.MasterVolume = value;
        UpdateMasterVolumeText(value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (temporarySettings == null)
        {
            return;
        }

        temporarySettings.MusicVolume = value;
        UpdateMusicVolumeText(value);
    }

    private void OnSfxVolumeChanged(float value)
    {
        if (temporarySettings == null)
        {
            return;
        }

        temporarySettings.SfxVolume = value;
        UpdateSfxVolumeText(value);
    }

    private void OnApplyPressed()
    {
        if (SettingsDataManager.Instance == null || temporarySettings == null)
        {
            return;
        }

        SettingsDataManager.Instance.CurrentSettings.CopyFrom(temporarySettings);
        SettingsDataManager.Instance.SaveSettings();
        ApplyLiveSettings(SettingsDataManager.Instance.CurrentSettings);
    }

    private void OnResetPressed()
    {
        if (temporarySettings == null)
        {
            return;
        }

        temporarySettings.ResetToDefaults();
        PopulateGraphicsDropdowns();
        UpdateUIFromSettings();
    }

    private void OnBackPressed()
    {
        SetScriptsEnabled(true);
        mainMenuManager?.RefreshMenu();
    }

    private void ApplyLiveSettings(GameSettingsData settings)
    {
        if (settings == null)
        {
            return;
        }

        int resolutionIndex = Mathf.Clamp(settings.ResolutionIndex, 0, Mathf.Max(0, availableResolutions.Length - 1));

        if (availableResolutions.Length > 0)
        {
            Resolution resolution = availableResolutions[resolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, settings.Fullscreen);
        }
        else
        {
            Screen.fullScreen = settings.Fullscreen;
        }

        int qualityIndex = Mathf.Clamp(settings.QualityIndex, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        QualitySettings.SetQualityLevel(qualityIndex, true);

        ApplyMixerVolume("MasterVolume", settings.MasterVolume);
        ApplyMixerVolume("MusicVolume", settings.MusicVolume);
        ApplyMixerVolume("SfxVolume", settings.SfxVolume);
    }

    private void ApplyMixerVolume(string parameterName, float normalizedValue)
    {
        if (audioMixer == null)
        {
            return;
        }

        float clampedValue = Mathf.Clamp(normalizedValue, 0.0001f, 1f);
        float decibels = Mathf.Log10(clampedValue) * 20f;
        audioMixer.SetFloat(parameterName, decibels);
    }

    private void SetScriptsEnabled(bool enabledState)
    {
        if (scriptsToDisableWhileOpen == null)
        {
            return;
        }

        for (int i = 0; i < scriptsToDisableWhileOpen.Length; i++)
        {
            if (scriptsToDisableWhileOpen[i] != null)
            {
                scriptsToDisableWhileOpen[i].enabled = enabledState;
            }
        }
    }

    private void UpdateSensitivityText(float value)
    {
        if (sensitivityValueText != null)
        {
            sensitivityValueText.text = value.ToString("F2");
        }
    }

    private void UpdateMasterVolumeText(float value)
    {
        if (masterVolumeText != null)
        {
            masterVolumeText.text = Mathf.RoundToInt(value * 100f).ToString();
        }
    }

    private void UpdateMusicVolumeText(float value)
    {
        if (musicVolumeText != null)
        {
            musicVolumeText.text = Mathf.RoundToInt(value * 100f).ToString();
        }
    }

    private void UpdateSfxVolumeText(float value)
    {
        if (sfxVolumeText != null)
        {
            sfxVolumeText.text = Mathf.RoundToInt(value * 100f).ToString();
        }
    }
}