using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeybindManager : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private SettingsManager settingsManager;

    private KeybindRowUI[] keybindRows;

    public void Initialize()
    {
        keybindRows = GetComponentsInChildren<KeybindRowUI>(true);

        for (int i = 0; i < keybindRows.Length; i++)
        {
            keybindRows[i].Initialize(this);
        }
    }

    public void CancelActiveRebind()
    {
        if (keybindRows == null)
            return;

        for (int i = 0; i < keybindRows.Length; i++)
        {
            if (keybindRows[i].IsListening)
            {
                keybindRows[i].CancelRebind();
            }
        }
    }

    public KeybindRowUI CheckForConflict(KeybindRowUI reboundRow)
    {
        if (keybindRows == null)
            return null;

        string newPath = reboundRow.EffectivePath;

        if (string.IsNullOrEmpty(newPath))
            return null;

        for (int i = 0; i < keybindRows.Length; i++)
        {
            KeybindRowUI other = keybindRows[i];

            if (other == reboundRow)
                continue;

            if (other.EffectivePath == newPath)
                return other;
        }

        return null;
    }

    public void OnConflictDetected(KeybindRowUI conflicting, KeybindRowUI attempted, string keyName)
    {
        conflicting.PlayConflictFlash();
        attempted.PlayConflictFlash();

        string actionName = conflicting.ActionReference.action.name;
        string friendlyName = AddSpacesBeforeCapitals(actionName);

        // For composite parts, add the part name
        var binding = conflicting.ActionReference.action.bindings[conflicting.BindingIndex];
        if (binding.isPartOfComposite && !string.IsNullOrEmpty(binding.name))
        {
            string partName = char.ToUpper(binding.name[0]) + binding.name.Substring(1);
            friendlyName = friendlyName + " (" + partName + ")";
        }

        if (settingsManager != null)
        {
            settingsManager.SetDescriptionText(
                keyName + " is already bound to " + friendlyName + ". Choose another key.");
        }

        StartCoroutine(ClearDescriptionAfterDelay(4f));
    }

    private IEnumerator ClearDescriptionAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (settingsManager != null)
        {
            settingsManager.ClearDescriptionText();
        }
    }

    public string GetOverridesJson()
    {
        if (inputActions == null)
            return string.Empty;

        return inputActions.SaveBindingOverridesAsJson();
    }

    public void LoadOverrides(string json)
    {
        if (inputActions == null)
            return;

        if (!string.IsNullOrEmpty(json))
        {
            inputActions.LoadBindingOverridesFromJson(json);
        }

        UpdateAllDisplays();
    }

    public void ResetAllBindings()
    {
        if (inputActions == null)
            return;

        inputActions.RemoveAllBindingOverrides();
        UpdateAllDisplays();
    }

    public void UpdateAllDisplays()
    {
        if (keybindRows == null)
            return;

        for (int i = 0; i < keybindRows.Length; i++)
        {
            keybindRows[i].UpdateKeyDisplay();
        }
    }

    private static string AddSpacesBeforeCapitals(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var sb = new System.Text.StringBuilder();
        sb.Append(text[0]);

        for (int i = 1; i < text.Length; i++)
        {
            if (char.IsUpper(text[i]))
                sb.Append(' ');

            sb.Append(text[i]);
        }

        return sb.ToString();
    }
}
