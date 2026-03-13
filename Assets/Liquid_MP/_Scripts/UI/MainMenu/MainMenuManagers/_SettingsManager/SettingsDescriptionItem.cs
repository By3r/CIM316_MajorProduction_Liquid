using UnityEngine;
using UnityEngine.EventSystems;

public class SettingsDescriptionItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea(2, 4)]
    [SerializeField] private string description;

    private SettingsManager settingsManager;

    public void Initialize(SettingsManager manager)
    {
        settingsManager = manager;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (settingsManager != null)
        {
            settingsManager.SetDescriptionText(description);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (settingsManager != null)
        {
            settingsManager.ClearDescriptionText();
        }
    }
}
