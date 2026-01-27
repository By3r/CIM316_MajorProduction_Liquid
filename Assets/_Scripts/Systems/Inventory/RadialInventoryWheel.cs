using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Scripts.Systems.Inventory
{
    public class RadialInventoryWheel : MonoBehaviour
    {
        #region Variables

        [Header("Wheel References")]
        [Tooltip("Center of the wheel in UI space.")]
        [SerializeField] private RectTransform wheelRectTransform;

        [Tooltip("CanvasGroup to fade and block raycasts when hidden.")]
        [SerializeField] private CanvasGroup wheelCanvasGroup;

        [Tooltip("Camera used for ScreenPoint conversion. Leave null for Screen Space Overlay.")]
        [SerializeField] private Camera uiCamera;

        [Header("Slots")]
        [Tooltip("All slot UI elements that belong to this wheel, in circular order.")]
        [SerializeField] private List<RadialInventorySlotUI> slots = new List<RadialInventorySlotUI>();

        [Tooltip("Radius in pixels from center to each slot.")]
        [SerializeField] private float slotRadius = 200f;

        [Header("Selection Settings")]
        [Tooltip("Inner dead zone where no slot is selected.")]
        [SerializeField] private float deadZoneRadius = 50f;

        [Tooltip("Angle offset in degrees applied to selection and layout.")]
        [SerializeField] private float angleOffset;

        [Tooltip("If true, layout and selection will go clockwise instead of counter clockwise.")]
        [SerializeField] private bool clockwise;

        [Tooltip("Extra step offset applied to the selected index.")]
        [SerializeField] private int selectionIndexOffsetSteps = 0;

        [Header("Input Settings")]
        [Tooltip("Key to hold in order to show the wheel.")]
        [SerializeField] private KeyCode openKey = KeyCode.Tab;

        [Tooltip("Key that confirms the currently selected slot.")]
        [SerializeField] private KeyCode confirmKey = KeyCode.Mouse0;

        [Tooltip("If true, selection only updates while the wheel is open (i.e. while openKey is held).")]
        [SerializeField] private bool requireHoldForSelection = true;

        [Header("Post Processing")]
        [Tooltip("URP Volume that contains Depth Of Field. Weight is driven by this wheel.")]
        [SerializeField] private Volume blurVolume;

        [Tooltip("How fast the blur fades in and out.")]
        [SerializeField] private float blurFadeSpeed = 8f;

        private bool _isWheelOpen;
        private int _currentSelectedIndex = -1;

        private float _targetBlurWeight;

        public event Action<int, InventoryItemData> OnSelectionChanged;
        public event Action<int, InventoryItemData> OnSlotConfirmed;
        public event Action<int, InventoryItemData> OnWheelClosedWithSelection;

        #endregion

        #region Unity

        private void Awake()
        {
            if (wheelRectTransform == null)
            {
                wheelRectTransform = GetComponent<RectTransform>();
                if (wheelRectTransform == null)
                {
                    Debug.LogError("WheelRectTransform is not assigned and no RectTransform found.");
                }
            }

            if (wheelCanvasGroup == null)
            {
                wheelCanvasGroup = GetComponent<CanvasGroup>();
            }

            if (blurVolume != null)
            {
                blurVolume.weight = 0f;
                _targetBlurWeight = 0f;
            }

            ArrangeSlotsRadially();
            SetSelectedIndex(-1);
            SetWheelVisible(false);
        }

        private void Update()
        {
            HandleOpenCloseInput();

            if (_isWheelOpen)
            {
                UpdateSelectionFromPointer();
                HandleConfirmInput();
            }

            UpdateBlurWeight();
        }

        #endregion

        #region Blur logic
        private void UpdateBlurWeight()
        {
            if (blurVolume == null)
            {
                return;
            }

            float current = blurVolume.weight;
            float target = _targetBlurWeight;

            if (Mathf.Approximately(current, target))
            {
                return;
            }

            float lerped = Mathf.Lerp(current, target, Time.unscaledDeltaTime * blurFadeSpeed);
            blurVolume.weight = lerped;
        }

        #endregion

        #region Public Functions
        public void SetItems(IReadOnlyList<InventoryItemData> items)
        {
            int count = slots.Count;
            for (int i = 0; i < count; i++)
            {
                InventoryItemData data = (items != null && i < items.Count) ? items[i] : null;
                slots[i].SetItem(data);
            }

            ArrangeSlotsRadially();
            SetSelectedIndex(-1);
        }

        #endregion

        #region Input
        private void HandleOpenCloseInput()
        {
            if (Input.GetKeyDown(openKey))
            {
                OpenWheel();
            }

            if (Input.GetKeyUp(openKey))
            {
                CloseWheel();
            }
        }

        private void HandleConfirmInput()
        {
            if (!Input.GetKeyDown(confirmKey))
            {
                return;
            }

            if (_currentSelectedIndex < 0 || _currentSelectedIndex >= slots.Count)
            {
                return;
            }

            RadialInventorySlotUI slot = slots[_currentSelectedIndex];
            InventoryItemData data = slot != null ? slot.ItemData : null;

            OnSlotConfirmed?.Invoke(_currentSelectedIndex, data);
        }

        #endregion

        #region Open / Close
        private void OpenWheel()
        {
            _isWheelOpen = true;
            SetWheelVisible(true);
            Cursor.visible = false;

            if (requireHoldForSelection)
            {
                UpdateSelectionFromPointer();
            }

            _targetBlurWeight = 1f;
        }

        private void CloseWheel()
        {
            int selectedIndex = _currentSelectedIndex;
            InventoryItemData selectedItem = null;
            Cursor.visible = true;

            if (selectedIndex >= 0 && selectedIndex < slots.Count)
            {
                RadialInventorySlotUI slot = slots[selectedIndex];
                selectedItem = slot != null ? slot.ItemData : null;
            }

            _isWheelOpen = false;
            SetWheelVisible(false);
            SetSelectedIndex(-1);

            _targetBlurWeight = 0f;

            if (selectedItem != null)
            {
                OnWheelClosedWithSelection?.Invoke(selectedIndex, selectedItem);
            }
        }

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

        #endregion

        #region Selection
        private void UpdateSelectionFromPointer()
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

            float sectorSize = 360f / Mathf.Max(1, slots.Count);

            float adjustedAngle = angle + angleOffset + sectorSize * 0.5f;
            if (adjustedAngle < 0f)
            {
                adjustedAngle += 360f;
            }

            int index = Mathf.FloorToInt(adjustedAngle / sectorSize) % slots.Count;

            if (selectionIndexOffsetSteps != 0 && slots.Count > 0)
            {
                index = (index + selectionIndexOffsetSteps) % slots.Count;
                if (index < 0)
                {
                    index += slots.Count;
                }
            }

            SetSelectedIndex(index);
        }

        private void SetSelectedIndex(int newIndex)
        {
            if (newIndex == _currentSelectedIndex)
            {
                return;
            }

            _currentSelectedIndex = newIndex;

            for (int i = 0; i < slots.Count; i++)
            {
                bool isSelected = (i == _currentSelectedIndex);
                slots[i].SetHighlight(isSelected);
            }

            InventoryItemData data = null;
            if (_currentSelectedIndex >= 0 && _currentSelectedIndex < slots.Count)
            {
                data = slots[_currentSelectedIndex].ItemData;
            }

            OnSelectionChanged?.Invoke(_currentSelectedIndex, data);
        }

        #endregion

        #region Layout
        /// <summary>
        /// Positions all slots in a circle around the wheel center.
        /// If a RadialSegmentArranger is present then this method will not modify the slot transforms.
        /// </summary>
        private void ArrangeSlotsRadially()
        {
            if (wheelRectTransform == null || slots == null || slots.Count == 0)
            {
                return;
            }

            RadialSegmentArranger arranger = GetComponent<RadialSegmentArranger>();
            if (arranger != null)
            {
                return;
            }

            float sectorSize = 360f / Mathf.Max(1, slots.Count);

            for (int i = 0; i < slots.Count; i++)
            {
                RadialInventorySlotUI slot = slots[i];
                if (slot == null || slot.SlotRectTransform == null)
                {
                    continue;
                }

                float angle = sectorSize * i;

                if (clockwise)
                {
                    angle = -angle;
                }

                angle += angleOffset;

                float rad = angle * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(rad) * slotRadius, Mathf.Sin(rad) * slotRadius);

                slot.SlotRectTransform.anchoredPosition = offset;
                slot.SlotRectTransform.localRotation = Quaternion.identity;
            }
        }
        #endregion
    }
}