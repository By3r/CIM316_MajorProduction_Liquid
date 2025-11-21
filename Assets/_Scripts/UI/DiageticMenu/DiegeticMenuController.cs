using UnityEngine;
using UnityEngine.InputSystem;

public class DiegeticMenuController : MonoBehaviour
{
    #region Variables
    [Header("Menu Items")]
    [SerializeField] private DiegeticMenuItem[] items;
    [SerializeField] private int startIndex = 0;

    [Header("Menu State")]
    [SerializeField] private CanvasGroup menuCanvasGroup;

    [Header("Positioning")]
    [SerializeField] private Transform menuRoot;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float distanceFromCamera = 2.0f;
    [SerializeField] private float verticalOffset = 0.0f;
    [SerializeField] private bool followCameraRotation = true;

    private int _currentIndex = 0;
    #endregion

    private void Awake()
    {
        if (items == null || items.Length == 0)
        {
            Debug.LogWarning("DiegeticMenuController has no items assigned.");
        }
        else
        {
            _currentIndex = Mathf.Clamp(startIndex, 0, items.Length - 1);

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null)
                {
                    items[i].Initialize(this);
                }
            }
        }

        if (menuRoot == null)
        {
            menuRoot = transform;
        }

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        UpdateVisualSelection();
    }

    private void Update()
    {
        if (menuCanvasGroup != null && !menuCanvasGroup.interactable)
        {
            return;
        }

        HandleKeyboardInput();
    }

    private void HandleKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
        {
            MoveSelection(-1);
        }

        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
        {
            MoveSelection(1);
        }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
        {
            ActivateSelectedItem();
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            // TODO: Close settings if active.
        }
    }

    private void MoveSelection(int direction)
    {
        if (items == null || items.Length == 0)
        {
            return;
        }

        _currentIndex = (_currentIndex + direction + items.Length) % items.Length;
        UpdateVisualSelection();
    }

    private void UpdateVisualSelection()
    {
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
            {
                continue;
            }

            bool isHighlighted = (i == _currentIndex);
            items[i].SetHighlighted(isHighlighted);
        }
    }

    public void SetSelectedItem(DiegeticMenuItem item)
    {
        if (items == null)
        {
            return;
        }

        int index = System.Array.IndexOf(items, item);
        if (index >= 0 && index < items.Length)
        {
            _currentIndex = index;
            UpdateVisualSelection();
        }
    }

    public void ActivateSelectedItem()
    {
        if (items == null || items.Length == 0)
        {
            return;
        }

        DiegeticMenuItem item = items[_currentIndex];
        if (item != null)
        {
            item.Invoke();
        }
    }
}