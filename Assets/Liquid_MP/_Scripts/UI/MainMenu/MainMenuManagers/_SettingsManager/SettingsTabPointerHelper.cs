using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SettingsTabPointerHelper : MonoBehaviour, IPointerEnterHandler
{
    private SettingsManager settingsManager;
    private Button button;

    public void Initialize(SettingsManager manager, Button targetButton)
    {
        settingsManager = manager;
        button = targetButton;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (settingsManager == null || button == null)
        {
            return;
        }

        settingsManager.NotifyTabPointerEntered(button);
    }
}