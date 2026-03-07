using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Scripts.UI.MainMenu
{
    public class MenuButtonPointerHelper : MonoBehaviour, IPointerEnterHandler
    {
        private MainMenuManager mainMenuManager;
        private Button button;

        public void Initialize(MainMenuManager manager, Button targetButton)
        {
            mainMenuManager = manager;
            button = targetButton;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (mainMenuManager == null || button == null)
            {
                return;
            }

            mainMenuManager.NotifyPointerEntered(button);
        }
    }
}