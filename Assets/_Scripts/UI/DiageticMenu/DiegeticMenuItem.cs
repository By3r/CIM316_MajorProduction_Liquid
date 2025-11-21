using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(Button))]
public class DiegeticMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    #region Variables
    [Header("Visuals")]
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image highlightImage;

    private DiegeticMenuController controller;
    private Button button;
    #endregion

    public void Initialize(DiegeticMenuController owner)
    {
        controller = owner;
        button = GetComponent<Button>();
        SetHighlighted(false);
    }

    public void SetHighlighted(bool isHighlighted)
    {
        if (highlightImage != null)
        {
            highlightImage.enabled = isHighlighted;
        }

        if (label != null)
        {
            label.fontStyle = isHighlighted ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (controller != null)
        {
            controller.SetSelectedItem(this);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (controller != null)
        {
            controller.ActivateSelectedItem();
        }
    }

    public void Invoke()
    {
        if (button != null)
        {
            button.onClick?.Invoke();
        }
    }
}