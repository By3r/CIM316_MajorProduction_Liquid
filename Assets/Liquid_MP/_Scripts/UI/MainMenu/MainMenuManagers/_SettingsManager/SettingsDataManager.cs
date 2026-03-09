using UnityEngine;

public class SettingsDataManager : MonoBehaviour
{
    public static SettingsDataManager Instance { get; private set; }

    public GameSettingsData CurrentSettings { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        CurrentSettings = SettingsSaveSystem.LoadSettings();
    }

    public void SaveSettings()
    {
        if (CurrentSettings == null)
        {
            return;
        }

        SettingsSaveSystem.SaveSettings(CurrentSettings);
    }

    public void ReloadSettings()
    {
        CurrentSettings = SettingsSaveSystem.LoadSettings();
    }

    public void ResetSettingsToDefaults()
    {
        if (CurrentSettings == null)
        {
            CurrentSettings = new GameSettingsData();
        }

        CurrentSettings.ResetToDefaults();
        SaveSettings();
    }
}