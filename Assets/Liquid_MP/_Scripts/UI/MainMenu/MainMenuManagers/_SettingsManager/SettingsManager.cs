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
        public TMP_Text Label;

        [Header("Visual States")]
        public GameObject HighlightObject;
        public GameObject PressedObject;
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

    [Header("Tab Label Colors")]
    [SerializeField] private Color normalTabTextColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color selectedTabTextColor = Color.white;

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

    [Header("Description")]
    [SerializeField] private TMP_Text descriptionText;
    private const string DefaultDescription = "No Description.";

    [Header("Keybindings")]
    [SerializeField] private KeybindManager keybindManager;

    [Header("Buttons")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button backButton;

    private GameSettingsData temporarySettings;
    private Resolution[] availableResolutions;
    private SettingsCategory currentCategory = SettingsCategory.Controls;
    private SettingsTabVisuals highlightedTab;

    private void Awake()
    {
        SetupListeners();
        CacheResolutions();
        ForceTabVisualRefresh();
        InitializeDescriptionItems();

        if (keybindManager != null)
        {
            keybindManager.Initialize();
        }
    }

    private void Start()
    {
        if (SettingsDataManager.Instance != null)
        {
            ApplyLiveSettings(SettingsDataManager.Instance.CurrentSettings);
        }
    }

    private void Update()
    {
        UpdateTabHighlightFromEventSystem();
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

        if (keybindManager != null)
        {
            keybindManager.LoadOverrides(temporarySettings.KeybindOverridesJson);
        }

        ShowControlsCategory();

        if (controlsTab.Button != null)
        {
            EventSystem.current?.SetSelectedGameObject(controlsTab.Button.gameObject);
            highlightedTab = controlsTab;
            RefreshTabVisualStates();
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

        if (controlsTab.Button != null)
        {
            controlsTab.Button.onClick.AddListener(ShowControlsCategory);
            AddTabPointerHelper(controlsTab.Button);
        }

        if (graphicsTab.Button != null)
        {
            graphicsTab.Button.onClick.AddListener(ShowGraphicsCategory);
            AddTabPointerHelper(graphicsTab.Button);
        }

        if (audioTab.Button != null)
        {
            audioTab.Button.onClick.AddListener(ShowAudioCategory);
            AddTabPointerHelper(audioTab.Button);
        }
    }
    private void AddTabPointerHelper(Button button)
    {
        if (button == null)
        {
            return;
        }

        SettingsTabPointerHelper helper = button.GetComponent<SettingsTabPointerHelper>();

        if (helper == null)
        {
            helper = button.gameObject.AddComponent<SettingsTabPointerHelper>();
        }

        helper.Initialize(this, button);
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
        highlightedTab = controlsTab;
        SetCategoryVisibility(SettingsCategory.Controls);
    }

    private void ShowGraphicsCategory()
    {
        currentCategory = SettingsCategory.Graphics;
        highlightedTab = graphicsTab;
        SetCategoryVisibility(SettingsCategory.Graphics);
    }

    private void ShowAudioCategory()
    {
        currentCategory = SettingsCategory.Audio;
        highlightedTab = audioTab;
        SetCategoryVisibility(SettingsCategory.Audio);
    }

    private void SetCategoryVisibility(SettingsCategory activeCategory)
    {
        if (controlsTab.Panel != null) controlsTab.Panel.SetActive(activeCategory == SettingsCategory.Controls);
        if (graphicsTab.Panel != null) graphicsTab.Panel.SetActive(activeCategory == SettingsCategory.Graphics);
        if (audioTab.Panel != null) audioTab.Panel.SetActive(activeCategory == SettingsCategory.Audio);

        RefreshTabVisualStates();
    }

    private void RefreshTabVisualStates()
    {
        RefreshSingleTabVisualState(controlsTab, SettingsCategory.Controls);
        RefreshSingleTabVisualState(graphicsTab, SettingsCategory.Graphics);
        RefreshSingleTabVisualState(audioTab, SettingsCategory.Audio);
    }

    public void NotifyTabPointerEntered(Button button)
    {
        if (button == null || !button.gameObject.activeInHierarchy)
        {
            return;
        }

        EventSystem.current?.SetSelectedGameObject(button.gameObject);
    }

    private void RefreshSingleTabVisualState(SettingsTabVisuals tab, SettingsCategory tabCategory)
    {
        bool isHighlighted = highlightedTab == tab;
        bool isPressed = currentCategory == tabCategory;

        if (tab.HighlightObject != null)
        {
            tab.HighlightObject.SetActive(isHighlighted);
        }

        if (tab.PressedObject != null)
        {
            tab.PressedObject.SetActive(isPressed);
        }

        if (tab.Label != null)
        {
            tab.Label.color = isPressed ? selectedTabTextColor : normalTabTextColor;
        }
    }

    private void UpdateTabHighlightFromEventSystem()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

        SettingsTabVisuals newHighlightedTab = null;

        if (selectedObject != null)
        {
            if (controlsTab.Button != null && selectedObject == controlsTab.Button.gameObject)
            {
                newHighlightedTab = controlsTab;
            }
            else if (graphicsTab.Button != null && selectedObject == graphicsTab.Button.gameObject)
            {
                newHighlightedTab = graphicsTab;
            }
            else if (audioTab.Button != null && selectedObject == audioTab.Button.gameObject)
            {
                newHighlightedTab = audioTab;
            }
        }

        if (highlightedTab != newHighlightedTab)
        {
            highlightedTab = newHighlightedTab;
            RefreshTabVisualStates();
        }
    }

    private void ForceTabVisualRefresh()
    {
        highlightedTab = controlsTab;
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

        if (keybindManager != null)
        {
            temporarySettings.KeybindOverridesJson = keybindManager.GetOverridesJson();
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

        if (keybindManager != null)
        {
            keybindManager.ResetAllBindings();
        }
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

    private void InitializeDescriptionItems()
    {
        SettingsDescriptionItem[] items = GetComponentsInChildren<SettingsDescriptionItem>(true);

        for (int i = 0; i < items.Length; i++)
        {
            items[i].Initialize(this);
        }

        ClearDescriptionText();
    }

    public void SetDescriptionText(string text)
    {
        if (descriptionText != null)
        {
            descriptionText.text = string.IsNullOrEmpty(text) ? DefaultDescription : text;
        }
    }

    public void ClearDescriptionText()
    {
        if (descriptionText != null)
        {
            descriptionText.text = DefaultDescription;
        }
    }
}