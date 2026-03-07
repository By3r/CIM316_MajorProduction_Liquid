using _Scripts.Core.Persistence;
using _Scripts.Core.SceneFlow;
using _Scripts.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadGameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MainMenuManager mainMenuManager;
    [SerializeField] private LoadGameSlotUI[] slotUIs;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button deleteAllButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text footerText;

    private void Awake()
    {
        if (slotUIs != null)
        {
            for (int i = 0; i < slotUIs.Length; i++)
            {
                if (slotUIs[i] != null)
                {
                    slotUIs[i].Initialize(this, i);
                }
            }
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(ContinueMostRecentSave);
        }

        if (deleteAllButton != null)
        {
            deleteAllButton.onClick.RemoveAllListeners();
            deleteAllButton.onClick.AddListener(DeleteAllSaves);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBackPressed);
        }
    }

    public void RefreshPanel()
    {
        bool anySaveExists = SaveSystem.AnySaveExists();

        if (slotUIs != null)
        {
            for (int i = 0; i < slotUIs.Length; i++)
            {
                GameSaveData data = SaveSystem.LoadGame(i);

                if (slotUIs[i] != null)
                {
                    slotUIs[i].RefreshSlot(data);
                }
            }
        }

        if (continueButton != null)
        {
            continueButton.interactable = anySaveExists;
        }

        if (deleteAllButton != null)
        {
            deleteAllButton.interactable = anySaveExists;
        }

        if (footerText != null)
        {
            footerText.text = anySaveExists ? "Load saved data." : "No save data found.";
        }
    }

    public void ContinueMostRecentSave()
    {
        SceneTransitionManager.Instance?.ContinueFromSave();
    }

    public void LoadSlot(int slotIndex)
    {
        if (!SaveSystem.SaveExists(slotIndex))
        {
            return;
        }

        SceneTransitionManager.Instance?.ContinueFromSaveSlot(slotIndex);
    }

    private void DeleteAllSaves()
    {
        SaveSystem.DeleteAllSaves();
        RefreshPanel();
        mainMenuManager?.RefreshMenu();
    }

    private void OnBackPressed()
    {
        mainMenuManager?.RefreshMenu();
    }
}