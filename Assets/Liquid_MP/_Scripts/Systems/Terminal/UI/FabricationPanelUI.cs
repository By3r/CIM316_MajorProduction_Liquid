using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace _Scripts.Systems.Terminal.UI
{
    /// <summary>
    /// Manages the Fabrication tab of the safe room terminal.
    /// Left side: scrollable schematics list. Right side: selected schematic detail.
    /// </summary>
    public class FabricationPanelUI : MonoBehaviour
    {
        #region Events

        /// <summary>
        /// Fired when the player confirms crafting. Passes the recipe index.
        /// </summary>
        public event Action<int> OnCraftConfirmed;

        #endregion

        #region Serialized Fields

        [Header("Schematics List — Left Panel")]
        [SerializeField] private Transform _schematicListContainer;
        [SerializeField] private GameObject _schematicItemPrefab;
        [SerializeField] private TextMeshProUGUI _schematicCountText;

        [Header("Detail View — Right Panel")]
        [SerializeField] private TextMeshProUGUI _detailTitle;
        [SerializeField] private TextMeshProUGUI _detailSubtitle;
        [SerializeField] private Transform _ingredientContainer;
        [SerializeField] private GameObject _ingredientRowPrefab;

        [Header("Output Display")]
        [SerializeField] private Image _outputIcon;
        [SerializeField] private TextMeshProUGUI _outputName;
        [SerializeField] private TextMeshProUGUI _outputQuantity;

        [Header("Craft Button")]
        [SerializeField] private Button _craftButton;
        [SerializeField] private RectTransform _craftProgressLeft;
        [SerializeField] private RectTransform _craftProgressRight;

        [Header("Hold Settings")]
        [SerializeField] private float _holdDuration = 1.2f;
        [SerializeField] private float _progressMaxWidth = 550f;

        #endregion

        #region Private Fields

        private List<SchematicListItemUI> _listItems = new List<SchematicListItemUI>();
        private List<IngredientRowUI> _ingredientRows = new List<IngredientRowUI>();
        private int _selectedIndex = -1;
        private bool _isHolding;
        private float _holdTimer;
        private bool _canCraft;

        #endregion

        #region Properties

        public int SelectedIndex => _selectedIndex;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Wire hold events via EventTrigger instead of onClick
            if (_craftButton != null)
            {
                var trigger = _craftButton.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                    trigger = _craftButton.gameObject.AddComponent<EventTrigger>();

                var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                pointerDown.callback.AddListener(_ => OnCraftHoldStart());
                trigger.triggers.Add(pointerDown);

                var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                pointerUp.callback.AddListener(_ => OnCraftHoldRelease());
                trigger.triggers.Add(pointerUp);

                var pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                pointerExit.callback.AddListener(_ => OnCraftHoldRelease());
                trigger.triggers.Add(pointerExit);
            }

            ResetCraftProgress();
        }

        private void Update()
        {
            if (!_isHolding) return;

            _holdTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_holdTimer / _holdDuration);
            float width = t * _progressMaxWidth;

            SetProgressWidth(width);

            // Craft completes when hold is full
            if (t >= 1f)
            {
                _isHolding = false;
                ResetCraftProgress();

                if (_selectedIndex >= 0)
                    OnCraftConfirmed?.Invoke(_selectedIndex);
            }
        }

        #endregion

        #region Public Methods — List Management

        /// <summary>
        /// Clears and rebuilds the schematics list.
        /// Call this when recipes change (e.g., new schematic found).
        /// Each entry needs: name, whether the player can craft it.
        /// </summary>
        public void PopulateList(string[] names, bool[] available)
        {
            ClearList();

            if (names == null || names.Length == 0)
            {
                if (_schematicCountText != null)
                    _schematicCountText.text = "0 LOADED";
                return;
            }

            for (int i = 0; i < names.Length; i++)
            {
                GameObject obj = Instantiate(_schematicItemPrefab, _schematicListContainer);
                var item = obj.GetComponent<SchematicListItemUI>();

                if (item == null)
                {
                    Debug.LogError("[FabricationPanelUI] Schematic item prefab missing SchematicListItemUI!");
                    continue;
                }

                bool avail = i < available.Length && available[i];
                item.Initialize(i, names[i], avail);

                int idx = i; // Capture for closure
                item.Button.onClick.AddListener(() => SelectSchematic(idx));

                _listItems.Add(item);
            }

            if (_schematicCountText != null)
                _schematicCountText.text = $"{names.Length} LOADED";

            // Auto-select first item
            if (_listItems.Count > 0)
                SelectSchematic(0);
        }

        /// <summary>
        /// Updates the availability state of a single recipe in the list.
        /// </summary>
        public void UpdateAvailability(int index, bool available)
        {
            if (index >= 0 && index < _listItems.Count)
                _listItems[index].SetAvailable(available);
        }

        #endregion

        #region Public Methods — Detail View

        /// <summary>
        /// Selects a schematic and updates the detail view.
        /// The caller is responsible for populating the detail via ShowDetail().
        /// </summary>
        public void SelectSchematic(int index)
        {
            if (index < 0 || index >= _listItems.Count) return;

            // Deselect previous
            if (_selectedIndex >= 0 && _selectedIndex < _listItems.Count)
                _listItems[_selectedIndex].SetSelected(false);

            _selectedIndex = index;
            _listItems[index].SetSelected(true);
        }

        /// <summary>
        /// Populates the detail panel for the currently selected schematic.
        /// </summary>
        public void ShowDetail(string title, string subtitle,
                               string[] ingredientNames, Sprite[] ingredientIcons,
                               int[] have, int[] need,
                               Sprite outputIcon, string outputName, int outputQty,
                               bool canCraft)
        {
            // Title
            if (_detailTitle != null) _detailTitle.text = title;
            if (_detailSubtitle != null) _detailSubtitle.text = subtitle;

            // Clear old ingredient rows
            ClearIngredients();

            // Spawn ingredient rows
            if (ingredientNames != null)
            {
                for (int i = 0; i < ingredientNames.Length; i++)
                {
                    GameObject obj = Instantiate(_ingredientRowPrefab, _ingredientContainer);
                    var row = obj.GetComponent<IngredientRowUI>();

                    if (row == null)
                    {
                        Debug.LogError("[FabricationPanelUI] Ingredient row prefab missing IngredientRowUI!");
                        continue;
                    }

                    Sprite icon = (ingredientIcons != null && i < ingredientIcons.Length)
                        ? ingredientIcons[i] : null;
                    int h = (have != null && i < have.Length) ? have[i] : 0;
                    int n = (need != null && i < need.Length) ? need[i] : 1;

                    row.Setup(ingredientNames[i], icon, h, n);
                    _ingredientRows.Add(row);
                }
            }

            // Output
            if (_outputIcon != null && outputIcon != null)
                _outputIcon.sprite = outputIcon;

            if (_outputName != null)
                _outputName.text = outputName ?? "";

            if (_outputQuantity != null)
                _outputQuantity.text = $"\u00D7{outputQty}";

            // Craft button
            SetCraftButtonState(canCraft);
        }

        /// <summary>
        /// Updates the craft button state without rebuilding the detail view.
        /// </summary>
        public void SetCraftButtonState(bool canCraft)
        {
            _canCraft = canCraft;

            if (_craftButton != null)
                _craftButton.interactable = canCraft;

            // Reset progress if state changed
            _isHolding = false;
            ResetCraftProgress();
        }

        #endregion

        #region Private Methods

        private void ClearList()
        {
            foreach (var item in _listItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _listItems.Clear();
            _selectedIndex = -1;

            ClearIngredients();
        }

        private void ClearIngredients()
        {
            foreach (var row in _ingredientRows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            _ingredientRows.Clear();
        }

        private void OnCraftHoldStart()
        {
            if (!_canCraft || _selectedIndex < 0) return;

            _isHolding = true;
            _holdTimer = 0f;
        }

        private void OnCraftHoldRelease()
        {
            if (!_isHolding) return;

            _isHolding = false;
            ResetCraftProgress();
        }

        private void SetProgressWidth(float width)
        {
            if (_craftProgressLeft != null)
            {
                var size = _craftProgressLeft.sizeDelta;
                size.x = width;
                _craftProgressLeft.sizeDelta = size;
            }

            if (_craftProgressRight != null)
            {
                var size = _craftProgressRight.sizeDelta;
                size.x = width;
                _craftProgressRight.sizeDelta = size;
            }
        }

        private void ResetCraftProgress()
        {
            _holdTimer = 0f;
            SetProgressWidth(0f);
        }

        #endregion
    }
}
