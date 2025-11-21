using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// I will be discarding this to rework it. It's current state is unecessarily complex.
/// </summary>
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
    [SerializeField] private float angleOffset = 125f;

    [Tooltip("If true, interpret angles clockwise instead of counter clockwise.")]
    [SerializeField] private bool clockwise = true;

    [Header("Temp Item Prefabs")]
    [Tooltip("UI prefabs for temp items (Item1, Item2, Item3, etc.).")]
    [SerializeField] private GameObject[] tempItemPrefab;

    [Header("Legacy Keyboard Input (Optional Fallback)")]
    [Tooltip("Key to open the wheel by holding.")]
    [SerializeField] private KeyCode openKey = KeyCode.Tab;

    [Tooltip("Key to add a temp item to the next empty slot.")]
    [SerializeField] private KeyCode addItemKey = KeyCode.G;

    [Header("Input Actions (New Input System)")]
    [Tooltip("Action to open/hold the radial wheel (for example, bound to Tab key).")]
    [SerializeField] private InputActionReference openWheelAction;

    [Tooltip("Action to add a temp item to the next empty slot.")]
    [SerializeField] private InputActionReference addItemAction;

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

        ConfigurePieSlices();
    }
    #endregion

    private void OnEnable()
    {
        if (openWheelAction != null)
        {
            openWheelAction.action.performed += OnOpenWheelPerformed;
            openWheelAction.action.canceled += OnOpenWheelCanceled;
            openWheelAction.action.Enable();
        }

        if (addItemAction != null)
        {
            addItemAction.action.performed += OnAddItemPerformed;
            addItemAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (openWheelAction != null)
        {
            openWheelAction.action.performed -= OnOpenWheelPerformed;
            openWheelAction.action.canceled -= OnOpenWheelCanceled;
            openWheelAction.action.Disable();
        }

        if (addItemAction != null)
        {
            addItemAction.action.performed -= OnAddItemPerformed;
            addItemAction.action.Disable();
        }
    }

    private void Update()
    {
        if (openWheelAction == null)
        {
            HandleInventoryToggleInput();
        }

        if (addItemAction == null)
        {
            HandleAddingTempItemLegacy();
        }

        if (_isWheelOpen)
        {
            UpdateSelectionFromMouse();
        }
    }

    private void HandleInventoryToggleInput()
    {
        if (Input.GetKeyDown(openKey))
        {
            Debug.Log("Detected openKey down: " + openKey);
            OpenWheel();
        }

        if (Input.GetKeyUp(openKey))
        {
            Debug.Log("Detected openKey up: " + openKey);
            CloseWheel();
        }
    }

    #region Open / Close Inventory.
    private void OpenWheel()
    {
        _isWheelOpen = true;
        SetWheelVisible(true);
        UpdateSelectionFromMouse();
        Debug.Log("Inventory opened.");
    }

    private void CloseWheel()
    {
        _isWheelOpen = false;
        SetWheelVisible(false);
        Debug.Log("Inventory closed.");
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

    private void ConfigurePieSlices()
    {
        if (slots == null || slots.Count == 0)
        {
            return;
        }

        float sliceFill = 1f / slots.Count;
        float anglePerSlice = 360f / slots.Count;

        for (int i = 0; i < slots.Count; i++)
        {
            RadialSlot slot = slots[i];
            Image bg = slot.backgroundImage;

            if (bg == null)
            {
                continue;
            }

            bg.type = Image.Type.Filled;
            bg.fillMethod = Image.FillMethod.Radial360;
            bg.fillAmount = sliceFill;
            bg.fillClockwise = clockwise;
            bg.fillOrigin = 2;

            if (slot.slotTransform != null)
            {
                float sliceAngle = anglePerSlice * i + angleOffset;
                slot.slotTransform.localRotation = Quaternion.Euler(0f, 0f, -sliceAngle);
            }
        }
    }

    #region Cursor logic: Navigating through the inventory 'items'.
    private void UpdateSelectionFromMouse()
    {
        if (slots == null || slots.Count == 0 || wheelRectTransform == null)
        {
            return;
        }

        Vector2 mousePos;
        if (Mouse.current != null)
        {
            mousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            mousePos = Input.mousePosition;
        }

        Vector2 centerScreenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, wheelRectTransform.position);
        Vector2 dir = mousePos - centerScreenPos;

        float distance = dir.magnitude;
        if (distance < deadZoneRadius || dir == Vector2.zero)
        {
            SetSelectedIndex(-1);
            return;
        }

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

        Debug.Log("Selected index = " + _currentSelectedIndex);

        for (int i = 0; i < slots.Count; i++)
        {
            bool isSelected = (i == _currentSelectedIndex);
            slots[i].SetHighlight(isSelected);
        }
    }
    #endregion

    private void OnOpenWheelPerformed(InputAction.CallbackContext context)
    {
        OpenWheel();
    }

    private void OnOpenWheelCanceled(InputAction.CallbackContext context)
    {
        CloseWheel();
    }

    private void OnAddItemPerformed(InputAction.CallbackContext context)
    {
        TryAddTempItem();
    }

    private void HandleAddingTempItemLegacy()
    {
        if (!Input.GetKeyDown(addItemKey))
        {
            return;
        }

        TryAddTempItem();
    }

    private void TryAddTempItem()
    {
        if (tempItemPrefab == null || tempItemPrefab.Length == 0)
        {
            Debug.LogWarning("No tempItemPrefab assigned.");
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].HasItem())
            {
                GameObject prefab = tempItemPrefab[_tempItemIndex % tempItemPrefab.Length];
                _tempItemIndex++;

                GameObject instance = Object.Instantiate(prefab, slots[i].slotContentRoot);

                PositionItemInSlot(i, instance);

                slots[i].SetItem(instance);
                Debug.Log("Added debug item to slot " + i);
                return;
            }
        }

        Debug.Log("RadialInventoryWheel: All slots are fully filled with debug items.");
    }

    private void PositionItemInSlot(int slotIndex, GameObject itemInstance)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count)
        {
            return;
        }

        RadialSlot slot = slots[slotIndex];

        RectTransform itemRect = itemInstance.GetComponent<RectTransform>();
        if (itemRect == null)
        {
            return;
        }

        itemRect.anchorMin = new Vector2(0.5f, 0.5f);
        itemRect.anchorMax = new Vector2(0.5f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);

        float radius = slot.itemRadius;

        itemRect.anchoredPosition = new Vector2(radius, 0f);

        itemRect.localRotation = Quaternion.identity;
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

    [Header("Item Layout")]
    [Tooltip("Distance from the wheel center where the item should sit inside this pie slice.")]
    public float itemRadius = 100f;

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