using UnityEngine;

[System.Serializable]
public class GameSettingsData
{
    #region Variables
    [Header("Controls")]
    public float MouseSensitivity = 2.5f;
    public bool InvertYAxis = false;

    [Header("Graphics")]
    public int ResolutionIndex = 0;
    public int QualityIndex = 2;
    public bool Fullscreen = true;

    [Header("Audio")]
    public float MasterVolume = 1f;
    public float MusicVolume = 1f;
    public float SfxVolume = 1f;

    [Header("Keybindings")]
    public string KeybindOverridesJson = "";
    #endregion

    public void CopyFrom(GameSettingsData other)
    {
        if (other == null)
        {
            return;
        }

        MouseSensitivity = other.MouseSensitivity;
        InvertYAxis = other.InvertYAxis;
        ResolutionIndex = other.ResolutionIndex;
        QualityIndex = other.QualityIndex;
        Fullscreen = other.Fullscreen;
        MasterVolume = other.MasterVolume;
        MusicVolume = other.MusicVolume;
        SfxVolume = other.SfxVolume;
        KeybindOverridesJson = other.KeybindOverridesJson;
    }

    public void ResetToDefaults()
    {
        MouseSensitivity = 2.5f;
        InvertYAxis = false;
        ResolutionIndex = 0;
        QualityIndex = 2;
        Fullscreen = true;
        MasterVolume = 1f;
        MusicVolume = 1f;
        SfxVolume = 1f;
        KeybindOverridesJson = "";
    }
}