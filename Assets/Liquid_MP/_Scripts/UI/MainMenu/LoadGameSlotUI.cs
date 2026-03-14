using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadGameSlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button slotButton;
    [SerializeField] private TMP_Text locationText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private GameObject noDataOverlay;

    private int slotIndex;
    private LoadGameManager loadGameManager;

    public void Initialize(LoadGameManager manager, int index)
    {
        loadGameManager = manager;
        slotIndex = index;

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnSlotPressed);
        }
    }

    public void RefreshSlot(_Scripts.Core.Persistence.GameSaveData saveData)
    {
        bool hasData = saveData != null;

        Debug.Log($"{name} | hasData = {hasData}");

        if (noDataOverlay != null)
        {
            noDataOverlay.SetActive(!hasData);
            noDataOverlay.transform.SetAsLastSibling();
        }

        if (locationText != null)
        {
            if (hasData)
                locationText.text = $"{saveData.PlayerName}\n\n{saveData.GetDisplayLocationName()}";
            else
                locationText.text = string.Empty;
        }

        if (dateText != null)
        {
            if (hasData)
            {
                string[] split = saveData.SaveCreatedAt.Split(' ');

                if (split.Length >= 2)
                {
                    dateText.text = $"{split[0]}\n{split[1]}";
                }
                else
                {
                    dateText.text = saveData.SaveCreatedAt;
                }
            }
            else
            {
                dateText.text = string.Empty;
            }
        }

        if (slotButton != null)
        {
            slotButton.interactable = hasData;
        }
    }
    private void OnSlotPressed()
    {
        loadGameManager?.LoadSlot(slotIndex);
    }
}