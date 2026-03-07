using System;
using System.IO;
using UnityEngine;

public static class SettingsSaveSystem
{
    private const string SettingsFileName = "liquid_settings.json";

    private static string SettingsFilePath =>
        Path.Combine(Application.persistentDataPath, SettingsFileName);

    public static bool SettingsFileExists()
    {
        return File.Exists(SettingsFilePath);
    }

    public static void SaveSettings(GameSettingsData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SettingsFilePath, json);
            Debug.Log($"Saved settings to: {SettingsFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save settings: {ex}");
        }
    }

    public static GameSettingsData LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                Debug.LogWarning("No settings file found. Will use defaults.");
                return CreateDefaultSettings();
            }

            string json = File.ReadAllText(SettingsFilePath);
            GameSettingsData data = JsonUtility.FromJson<GameSettingsData>(json);

            if (data == null)
            {
                Debug.LogWarning("Settings file was invalid. Will use defaults.");
                return CreateDefaultSettings();
            }

            return data;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load settings: {ex}");
            return CreateDefaultSettings();
        }
    }

    public static void DeleteSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                File.Delete(SettingsFilePath);
                Debug.Log("Deleted settings file.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to delete settings file: {ex}");
        }
    }

    private static GameSettingsData CreateDefaultSettings()
    {
        GameSettingsData data = new GameSettingsData();
        data.ResetToDefaults();
        return data;
    }
}