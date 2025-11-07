using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RadialInventoryWheel : MonoBehaviour
{
    #region Variables
    [Header(" 'Wheel References' ")]
    [Tooltip("Center of the circle.")]
    [SerializeField] private RectTransform wheelRectTransform;

    [Tooltip("CanvasGroup to fade and block raycasts when hidden")]
    [SerializeField] private CanvasGroup wheelCanvasGroup;

    [Tooltip("Camera used for ScreenPoint conversion")]
    [SerializeField] private Camera uiCamera;

    [Header("Slots")]
    [Tooltip("All slots that belong to this wheel, in circular order.")]
    [SerializeField] private List<RadialSlot> slots = new List<RadialSlot>();

    [Header("Selection Settings")]
    [SerializeField] private float deadZoneRadius = 50f;

    [Tooltip("If true, selection only updates while Tab is held.")]
    [SerializeField] private bool requireHoldForSelection = true;

    [Header("Angle Mapping")]
    [Tooltip("Rotation offset in degrees applied to the selection logic.")]
    [SerializeField] private float angleOffset = 0f;

    [Tooltip("If true, interpret angles clockwise instead of counter clockwise.")]
    [SerializeField] private bool clockwise = false;

    [Header("Temp Item Prefabs")]
    [Tooltip("UI prefabs for temp items (Item1, Item2, Item3, etc.).")]
    [SerializeField] private GameObject[] tempItemPrefab;

    [Tooltip("Key to open the wheel by holding.")]
    [SerializeField] private KeyCode openKey = KeyCode.Tab;

    [Tooltip("Key to add a temp item to the next empty slot.")]
    [SerializeField] private KeyCode addItemKey = KeyCode.G;

    private bool _isWheelOpen;
    private int _currentSelectedIndex = -1;
    private int _tempItemIndex = 0;
    #endregion

    #region Awake
    private void Awake()
    {
        if (wheelRectTransform == null)
        {
            wheelRectTransform = GetComponent<RectTransform>();
            if (wheelRectTransform == null)
            {
                Debug.LogError("RadialInventoryWheel: There is no wheelRectTransform assigned and no RectTransform on this GameObject.");
            }
        }

        if (wheelCanvasGroup == null)
        {
            wheelCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (uiCamera == null)
        {
            uiCamera = null;
        }

        SetSelectedIndex(-1);
    }
    #endregion

    private void OnDestroy()
    {
        Debug.Log("RadialInventoryWheel: OnDestroy called on " + gameObject.name);
    }

    private void Update()
    {
        HandleInventoryToggleInput();
        HandleAddingTempItem();

        if (_isWheelOpen)
        {
            UpdateSelectionFromMouse();
        }
    }

    private void HandleInventoryToggleInput()
    {
        if (Input.GetKeyDown(openKey))
        {
            Debug.Log("RadialInventoryWheel: Detected openKey down: " + openKey);
            OpenWheel();
        }

        if (Input.GetKeyUp(openKey))
        {
            Debug.Log("RadialInventoryWheel: Detected openKey up: " + openKey);
            CloseWheel();
        }
    }

    #region Open / Close Inventory.
    private void OpenWheel()
    {
        _isWheelOpen = true;
        SetWheelVisible(true);
        UpdateSelectionFromMouse();
        Debug.Log("RadialInventoryWheel: Inventory opened.");
    }

    private void CloseWheel()
    {
        _isWheelOpen = false;
        SetWheelVisible(false);
        Debug.Log("RadialInventoryWheel: Inventory closed.");
    }
    #endregion

    private void SetWheelVisible(bool visible)
    {
        if (wheelCanvasGroup != null)
        {
            wheelCanvasGroup.alpha = visible ? 1f : 0f;
            wheelCanvasGroup.interactable = visible;
            wheelCanvasGroup.blocksRaycasts = visible;
        }
        else if (wheelRectTransform != null)
        {
            wheelRectTransform.gameObject.SetActive(visible);
        }
    }

    #region Cursor logic - Navigating through the inventory 'items'.
    private void UpdateSelectionFromMouse()
    {
        if (slots == null || slots.Count == 0 || wheelRectTransform == null)
        {
            return;
        }

        Vector2 centerScreenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, wheelRectTransform.position);
        Vector2 mousePos = Input.mousePosition;
        Vector2 dir = mousePos - centerScreenPos;

        float distance = dir.magnitude;
        if (distance < deadZoneRadius || dir == Vector2.zero)
        {
            SetSelectedIndex(-1);
            return;
        }

        // Base angle from +X axis, counter clockwise
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (angle < 0f)
        {
            angle += 360f;
        }

        if (clockwise)
        {
            angle = 360f - angle;
            if (angle >= 360f)
            {
                angle -= 360f;
            }
        }

        float sectorSize = 360f / slots.Count;

        // Apply the offset and center the slice on its direction
        float adjustedAngle = angle + angleOffset + sectorSize * 0.5f;

        if (adjustedAngle < 0f)
        {
            adjustedAngle += 360f;
        }

        int index = Mathf.FloorToInt(adjustedAngle / sectorSize) % slots.Count;

        SetSelectedIndex(index);
    }

    private void SetSelectedIndex(int newIndex)
    {
        if (newIndex == _currentSelectedIndex)
        {
            return;
        }

        _currentSelectedIndex = newIndex;

        Debug.Log("RadialInventoryWheel: Selected index = " + _currentSelectedIndex);

        for (int i = 0; i < slots.Count; i++)
        {
            bool isSelected = (i == _currentSelectedIndex);
            slots[i].SetHighlight(isSelected);
        }
    }
    #endregion

    private void HandleAddingTempItem()
    {
        if (!Input.GetKeyDown(addItemKey))
        {
            return;
        }

        if (tempItemPrefab == null || tempItemPrefab.Length == 0)
        {
            Debug.LogWarning("RadialInventoryWheel: No tempItemPrefab assigned.");
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].HasItem())
            {
                GameObject prefab = tempItemPrefab[_tempItemIndex % tempItemPrefab.Length];
                _tempItemIndex++;

                GameObject instance = Instantiate(prefab, slots[i].slotContentRoot);
                instance.transform.localScale = Vector3.one;
                instance.transform.localPosition = Vector3.zero;

                slots[i].SetItem(instance);
                Debug.Log("RadialInventoryWheel: Added debug item to slot " + i);
                return;
            }
        }

        Debug.Log("RadialInventoryWheel: All slots are fully filled with debug items.");
    }
}

#region Slots class
[System.Serializable]
public class RadialSlot
{
    #region Variables
    [Header("Slot References")]
    [Tooltip("The RectTransform of this slot.")]
    public RectTransform slotTransform;

    [Tooltip("Image for the slot background (used for highlight).")]
    public Image backgroundImage;

    [Tooltip("Parent where item UI prefab will be instantiated.")]
    public RectTransform slotContentRoot;

    [Header("Colors")]
    [Tooltip("Default color when not selected.")]
    public Color normalColor = Color.white;

    [Tooltip("Color when this slot is selected by cursor direction.")]
    public Color highlightColor = Color.yellow;

    [HideInInspector] public GameObject currentItemInstance;
    #endregion

    #region Public Functions
    public void SetHighlight(bool isHighlighted)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = isHighlighted ? highlightColor : normalColor;
        }
    }

    public bool HasItem()
    {
        return currentItemInstance != null;
    }

    public void SetItem(GameObject itemInstance)
    {
        currentItemInstance = itemInstance;
    }
    #endregion
}
#endregion