using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PauseButtonPointerHelper : MonoBehaviour, IPointerEnterHandler
{
    #region Variables
    private PauseMenuManager pauseMenuManager;
    private Button button;
    #endregion

    public void Initialize(PauseMenuManager manager, Button btn)
    {
        pauseMenuManager = manager;
        button = btn;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pauseMenuManager?.NotifyButtonPointerEntered(button);
    }
}