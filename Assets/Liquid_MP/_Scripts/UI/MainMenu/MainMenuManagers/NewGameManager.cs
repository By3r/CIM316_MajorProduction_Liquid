using _Scripts.Core.Persistence;
using _Scripts.Core.SceneFlow;
using _Scripts.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NewGameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MainMenuManager mainMenuManager;
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Settings")]
    [SerializeField] private int minimumNameLength = 2;
    [SerializeField] private int maximumNameLength = 16;
    [SerializeField] private string defaultPlayerName = "Player";

    private void Awake()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmPressed);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBackPressed);
        }
    }

    public void PreparePanel()
    {
        if (playerNameInputField != null)
        {
            playerNameInputField.text = string.Empty;
            EventSystem.current?.SetSelectedGameObject(playerNameInputField.gameObject);
            playerNameInputField.ActivateInputField();
        }

        if (feedbackText != null)
        {
            feedbackText.text = string.Empty;
        }
    }

    private void OnConfirmPressed()
    {
        string enteredName = playerNameInputField != null ? playerNameInputField.text.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(enteredName))
        {
            enteredName = defaultPlayerName;
        }

        if (enteredName.Length < minimumNameLength)
        {
            if (feedbackText != null)
            {
                feedbackText.text = $"Name must be at least {minimumNameLength} characters.";
            }

            return;
        }

        if (enteredName.Length > maximumNameLength)
        {
            enteredName = enteredName.Substring(0, maximumNameLength);
        }

        SceneTransitionManager.Instance?.StartNewGameWithName(enteredName);
    }

    private void OnBackPressed()
    {
        mainMenuManager?.RefreshMenu();
    }
}