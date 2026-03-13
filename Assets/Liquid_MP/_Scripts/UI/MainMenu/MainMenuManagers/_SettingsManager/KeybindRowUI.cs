using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KeybindRowUI : MonoBehaviour
{
    [SerializeField] private InputActionReference actionReference;
    [SerializeField] private int bindingIndex;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text keyText;

    [Header("Conflict Visuals")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Color normalColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color listeningColor = new Color(0.13f, 0.13f, 0.13f, 1f);
    [SerializeField] private Color conflictColor = new Color(0.8f, 0.2f, 0.2f, 0.25f);

    private KeybindManager keybindManager;
    private InputActionRebindingExtensions.RebindingOperation rebindOperation;
    private bool isListening;

    public InputActionReference ActionReference => actionReference;
    public int BindingIndex => bindingIndex;
    public bool IsListening => isListening;

    public string EffectivePath
    {
        get
        {
            if (actionReference == null || actionReference.action == null)
                return string.Empty;

            var bindings = actionReference.action.bindings;
            if (bindingIndex < 0 || bindingIndex >= bindings.Count)
                return string.Empty;

            return bindings[bindingIndex].effectivePath;
        }
    }

    public void Initialize(KeybindManager manager)
    {
        keybindManager = manager;

        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }

        UpdateKeyDisplay();
    }

    public void UpdateKeyDisplay()
    {
        if (keyText == null || actionReference == null || actionReference.action == null)
            return;

        var bindings = actionReference.action.bindings;

        if (bindingIndex < 0 || bindingIndex >= bindings.Count)
            return;

        keyText.text = InputControlPath.ToHumanReadableString(
            bindings[bindingIndex].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    private void OnButtonClicked()
    {
        if (isListening || keybindManager == null)
            return;

        keybindManager.CancelActiveRebind();
        StartRebinding();
    }

    private void StartRebinding()
    {
        isListening = true;
        keyText.text = "...";

        if (buttonImage != null)
            buttonImage.color = listeningColor;

        var action = actionReference.action;
        action.Disable();

        rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(OnRebindComplete)
            .OnCancel(OnRebindCancel);

        // Exclude mouse for keyboard actions, but allow mouse buttons for Fire/Aim
        string actionName = action.name;
        if (actionName != "Fire" && actionName != "Aim")
        {
            rebindOperation.WithControlsExcluding("Mouse");
        }

        rebindOperation.Start();
    }

    private void OnRebindComplete(InputActionRebindingExtensions.RebindingOperation operation)
    {
        isListening = false;
        var action = actionReference.action;

        KeybindRowUI conflicting = keybindManager.CheckForConflict(this);

        if (conflicting != null)
        {
            string keyName = InputControlPath.ToHumanReadableString(
                action.bindings[bindingIndex].effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);

            // Revert the override
            action.RemoveBindingOverride(bindingIndex);
            UpdateKeyDisplay();

            keybindManager.OnConflictDetected(conflicting, this, keyName);
        }
        else
        {
            UpdateKeyDisplay();
        }

        if (buttonImage != null)
            buttonImage.color = normalColor;

        action.Enable();
        CleanupOperation();
    }

    private void OnRebindCancel(InputActionRebindingExtensions.RebindingOperation operation)
    {
        isListening = false;
        UpdateKeyDisplay();

        if (buttonImage != null)
            buttonImage.color = normalColor;

        actionReference.action.Enable();
        CleanupOperation();
    }

    public void CancelRebind()
    {
        if (!isListening || rebindOperation == null)
            return;

        rebindOperation.Cancel();
    }

    private void CleanupOperation()
    {
        rebindOperation?.Dispose();
        rebindOperation = null;
    }

    public void PlayConflictFlash()
    {
        StartCoroutine(ConflictFlashRoutine());
    }

    private IEnumerator ConflictFlashRoutine()
    {
        if (buttonImage == null)
            yield break;

        // Flash 3 times
        for (int i = 0; i < 3; i++)
        {
            buttonImage.color = conflictColor;
            yield return new WaitForSecondsRealtime(0.2f);
            buttonImage.color = normalColor;
            yield return new WaitForSecondsRealtime(0.2f);
        }

        // Slow fade back (2.5 seconds)
        buttonImage.color = conflictColor;
        float elapsed = 0f;
        float fadeDuration = 2.5f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeDuration;
            buttonImage.color = Color.Lerp(conflictColor, normalColor, t);
            yield return null;
        }

        buttonImage.color = normalColor;
    }

    private void OnDestroy()
    {
        CleanupOperation();
    }
}
