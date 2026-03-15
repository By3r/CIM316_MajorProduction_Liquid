using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance { get; private set; }

    [System.Serializable]
    private class PauseButtonVisuals
    {
        public Button Button;
        public GameObject HighlightObject;
        public GameObject PressedObject;
        public TMP_Text Label;
    }

    #region Variables
    [Header("Scene Exclusion")]
    [SerializeField] private string menuSceneName = "MainMenu";

    [Header("Root Panels")]
    [SerializeField] private GameObject pauseRootPanel;
    [SerializeField] private GameObject optionsPanel;

    [Header("Pause Buttons")]
    [SerializeField] private PauseButtonVisuals resumeButton;
    [SerializeField] private PauseButtonVisuals optionsButton;
    [SerializeField] private PauseButtonVisuals quitButton;

    [Header("Button Label Colors")]
    [SerializeField] private Color normalButtonColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color highlightButtonColor = Color.white;

    [Header("Default Selected Button")]
    [SerializeField] private PauseButtonVisuals defaultSelectedButton;

    [Header("Volume")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeText;
    [SerializeField] private TMP_Text musicVolumeText;
    [SerializeField] private TMP_Text sfxVolumeText;

    [Header("Display")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Options Buttons")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button backButton;

    [Header("Scripts to Disable While Paused")]
    [SerializeField] private MonoBehaviour[] scriptsToDisableWhilePaused;

    private bool isPaused;
    private bool isInOptions;
    private bool _isTogglingPause;
    private PauseButtonVisuals highlightedButton;

    private GameSettingsData temporarySettings;
    private Resolution[] availableResolutions;
    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CacheResolutions();
        SetupPauseButtonListeners();
        SetupOptionsListeners();
        HideAllPanels();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (!isInOptions)
            UpdateButtonHighlightFromEventSystem();
    }

    public void OnPauseInput(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (isInOptions)
        {
            ShowPausePanel();
        }
        else
        {
            TogglePause();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[PauseMenu] Scene loaded: {scene.name} — isPaused: {isPaused}");

        if (scene.name == menuSceneName && isPaused)
        {
            Debug.Log("[PauseMenu] Menu scene detected while paused — forcing resume.");
            ForceResume();
        }
    }

    private bool IsMenuScene() => SceneManager.GetActiveScene().name == menuSceneName;

    public void TogglePause()
    {
        if (_isTogglingPause || IsMenuScene())
        {
            return;
        }

        _isTogglingPause = true;
        try
        {
            if (isPaused) Resume(); else Pause();
        }
        finally
        {
            _isTogglingPause = false;
        }
    }

    public void Pause()
    {
        Debug.Log($"[PauseMenu] Pause() called. IsMenuScene: {IsMenuScene()}, isPaused: {isPaused}");
        if (IsMenuScene() || isPaused)
        {
            return;
        }

        isPaused = true;
        Time.timeScale = 0f;
        SetGameplayScriptsEnabled(false);

        if (_Scripts.Core.Managers.GameManager.Instance != null)
            _Scripts.Core.Managers.GameManager.Instance.SetGameState(_Scripts.Core.Managers.GameState.Paused);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (CursorManager.CursorInstance != null)
            CursorManager.CursorInstance.SetDefaultCursor();

        ShowPausePanel();
    }

    public void Resume()
    {
        Debug.Log($"[PauseMenu] Resume() called. isPaused: {isPaused}");
        if (!isPaused)
        {
            return;
        }

        isPaused = false;
        isInOptions = false;
        Time.timeScale = 1f;
        SetGameplayScriptsEnabled(true);

        if (_Scripts.Core.Managers.GameManager.Instance != null)
            _Scripts.Core.Managers.GameManager.Instance.SetGameState(_Scripts.Core.Managers.GameState.Gameplay);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        HideAllPanels();
    }

    private void ForceResume()
    {
        isPaused = false;
        isInOptions = false;
        Time.timeScale = 1f;
        SetGameplayScriptsEnabled(true);
        HideAllPanels();
    }
    private void ShowPausePanel()
    {
        isInOptions = false;

        if (pauseRootPanel != null) pauseRootPanel.SetActive(true);

        if (optionsPanel != null) optionsPanel.SetActive(false);

        FocusDefaultButton();
    }

    private void ShowOptionsPanel()
    {
        isInOptions = true;

        if (pauseRootPanel != null) pauseRootPanel.SetActive(false);

        if (optionsPanel != null) optionsPanel.SetActive(true);

        OpenOptions();
    }

    private void HideAllPanels()
    {
        if (pauseRootPanel != null) pauseRootPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    private void SetupPauseButtonListeners()
    {
        if (resumeButton.Button != null) resumeButton.Button.onClick.AddListener(OnResumePressed);

        if (optionsButton.Button != null) optionsButton.Button.onClick.AddListener(OnOptionsPressed);

        if (quitButton.Button != null) quitButton.Button.onClick.AddListener(OnQuitPressed);

        AddPausePointerHelper(resumeButton);
        AddPausePointerHelper(optionsButton);
        AddPausePointerHelper(quitButton);
    }

    private void OnResumePressed()
    {
        Resume();
    }

    private void OnOptionsPressed()
    {
        ShowOptionsPanel();
    }

    private void OnQuitPressed()
    {
        ForceResume();
        SceneManager.LoadScene(menuSceneName);
    }

    private void FocusDefaultButton()
    {
        PauseButtonVisuals target = defaultSelectedButton ?? resumeButton;

        if (target?.Button != null)
        {
            EventSystem.current?.SetSelectedGameObject(target.Button.gameObject);
            highlightedButton = target;
        }

        RefreshAllButtonVisuals();
    }

    private void UpdateButtonHighlightFromEventSystem()
    {
        if (EventSystem.current == null || !isPaused) return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        PauseButtonVisuals newHighlight = null;

        if (selected != null)
        {
            if (resumeButton.Button != null && selected == resumeButton.Button.gameObject) newHighlight = resumeButton;
            else if (optionsButton.Button != null && selected == optionsButton.Button.gameObject) newHighlight = optionsButton;
            else if (quitButton.Button != null && selected == quitButton.Button.gameObject) newHighlight = quitButton;
        }

        if (highlightedButton != newHighlight)
        {
            highlightedButton = newHighlight;
            RefreshAllButtonVisuals();
        }
    }

    private void RefreshAllButtonVisuals()
    {
        RefreshSingleButtonVisual(resumeButton);
        RefreshSingleButtonVisual(optionsButton);
        RefreshSingleButtonVisual(quitButton);
    }

    private void RefreshSingleButtonVisual(PauseButtonVisuals btn)
    {
        if (btn == null) return;

        bool highlighted = highlightedButton == btn;

        if (btn.HighlightObject != null) btn.HighlightObject.SetActive(highlighted);
        if (btn.PressedObject != null) btn.PressedObject.SetActive(false);
        if (btn.Label != null) btn.Label.color = highlighted ? highlightButtonColor : normalButtonColor;
    }

    private void AddPausePointerHelper(PauseButtonVisuals buttonVisuals)
    {
        if (buttonVisuals?.Button == null) return;

        PauseButtonPointerHelper helper = buttonVisuals.Button.GetComponent<PauseButtonPointerHelper>();
        if (helper == null)
            helper = buttonVisuals.Button.gameObject.AddComponent<PauseButtonPointerHelper>();

        helper.Initialize(this, buttonVisuals.Button);
    }

    public void NotifyButtonPointerEntered(Button button)
    {
        if (button == null || !button.gameObject.activeInHierarchy) return;
        EventSystem.current?.SetSelectedGameObject(button.gameObject);
    }

    private void OpenOptions()
    {
        if (SettingsDataManager.Instance == null)
        {
            return;
        }

        temporarySettings = new GameSettingsData();
        temporarySettings.CopyFrom(SettingsDataManager.Instance.CurrentSettings);

        PopulateResolutionDropdown();
        PopulateQualityDropdown();
        UpdateOptionsUI();
    }

    private void SetupOptionsListeners()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

        if (applyButton != null) applyButton.onClick.AddListener(OnApplyPressed);
        if (backButton != null) backButton.onClick.AddListener(OnBackPressed);
    }

    private void OnMasterVolumeChanged(float value) { if (temporarySettings == null) return; temporarySettings.MasterVolume = value; UpdateVolumeText(masterVolumeText, value); }
    private void OnMusicVolumeChanged(float value) { if (temporarySettings == null) return; temporarySettings.MusicVolume = value; UpdateVolumeText(musicVolumeText, value); }
    private void OnSfxVolumeChanged(float value) { if (temporarySettings == null) return; temporarySettings.SfxVolume = value; UpdateVolumeText(sfxVolumeText, value); }
    private void OnResolutionChanged(int index) { if (temporarySettings == null) return; temporarySettings.ResolutionIndex = index; }
    private void OnQualityChanged(int index) { if (temporarySettings == null) return; temporarySettings.QualityIndex = index; }
    private void OnFullscreenChanged(bool value) { if (temporarySettings == null) return; temporarySettings.Fullscreen = value; }

    private void OnApplyPressed()
    {
        if (SettingsDataManager.Instance == null || temporarySettings == null) return;

        SettingsDataManager.Instance.CurrentSettings.CopyFrom(temporarySettings);
        SettingsDataManager.Instance.SaveSettings();
        ApplyLiveSettings(SettingsDataManager.Instance.CurrentSettings);
    }

    private void OnBackPressed()
    {
        ShowPausePanel();
    }

    private void ApplyLiveSettings(GameSettingsData settings)
    {
        if (settings == null) return;

        if (availableResolutions.Length > 0)
        {
            int resIdx = Mathf.Clamp(settings.ResolutionIndex, 0, availableResolutions.Length - 1);
            Resolution res = availableResolutions[resIdx];
            Screen.SetResolution(res.width, res.height, settings.Fullscreen);
        }
        else
        {
            Screen.fullScreen = settings.Fullscreen;
        }

        int qualityIdx = Mathf.Clamp(settings.QualityIndex, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(qualityIdx, true);

        ApplyMixerVolume("MasterVolume", settings.MasterVolume);
        ApplyMixerVolume("MusicVolume", settings.MusicVolume);
        ApplyMixerVolume("SfxVolume", settings.SfxVolume);
    }

    private void ApplyMixerVolume(string param, float normalized)
    {
        if (audioMixer == null) return;
        float db = Mathf.Log10(Mathf.Clamp(normalized, 0.0001f, 1f)) * 20f;
        audioMixer.SetFloat(param, db);
    }

    private void CacheResolutions()
    {
        availableResolutions = Screen.resolutions;
    }

    private void PopulateResolutionDropdown()
    {
        if (resolutionDropdown == null) { return; }

        resolutionDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>();
        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution r = availableResolutions[i];
            options.Add($"{r.width} x {r.height} @ {r.refreshRateRatio.value:0}Hz");
        }
        resolutionDropdown.AddOptions(options);

        int safe = Mathf.Clamp(temporarySettings.ResolutionIndex, 0, Mathf.Max(0, availableResolutions.Length - 1));
        resolutionDropdown.value = safe;
        resolutionDropdown.RefreshShownValue();
    }

    private void PopulateQualityDropdown()
    {
        if (qualityDropdown == null) { return; }

        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));

        int safe = Mathf.Clamp(temporarySettings.QualityIndex, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        qualityDropdown.value = safe;
        qualityDropdown.RefreshShownValue();
    }

    private void UpdateOptionsUI()
    {
        if (temporarySettings == null) return;

        if (masterVolumeSlider != null) { masterVolumeSlider.value = temporarySettings.MasterVolume; UpdateVolumeText(masterVolumeText, temporarySettings.MasterVolume); }
        if (musicVolumeSlider != null) { musicVolumeSlider.value = temporarySettings.MusicVolume; UpdateVolumeText(musicVolumeText, temporarySettings.MusicVolume); }
        if (sfxVolumeSlider != null) { sfxVolumeSlider.value = temporarySettings.SfxVolume; UpdateVolumeText(sfxVolumeText, temporarySettings.SfxVolume); }

        if (fullscreenToggle != null) fullscreenToggle.isOn = temporarySettings.Fullscreen;
    }

    private void UpdateVolumeText(TMP_Text label, float value)
    {
        if (label != null) label.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void SetGameplayScriptsEnabled(bool state)
    {
        if (scriptsToDisableWhilePaused == null) return;

        for (int i = 0; i < scriptsToDisableWhilePaused.Length; i++)
        {
            if (scriptsToDisableWhilePaused[i] != null)
                scriptsToDisableWhilePaused[i].enabled = state;
        }
    }

    public bool IsPaused => isPaused;
    public bool IsInOptions => isInOptions;
}