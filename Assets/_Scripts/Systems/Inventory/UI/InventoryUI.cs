using UnityEngine;
using UnityEngine.InputSystem;
using _Scripts.Core.Managers;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// Main inventory UI controller.
    /// Opens with TAB key, shows 3 physical item slots (left) and 3 ingredient counters (right).
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        #region Singleton

        private static InventoryUI _instance;
        public static InventoryUI Instance => _instance;

        #endregion

        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private InventorySlotUI[] _slotUIs;
        [SerializeField] private IngredientCounterUI[] _ingredientCounterUIs;
        [SerializeField] private ARGramsCounterUI _arGramsCounterUI;

        [Header("Context Menu & Examination")]
        [SerializeField] private ItemContextMenu _contextMenu;
        [SerializeField] private HolographicItemExaminer _holographicExaminer;

        [Header("Drop Settings")]
        [Tooltip("Prefab spawns this far in front of the player when dropping.")]
        [SerializeField] private float _dropDistance = 1.5f;
        [SerializeField] private float _dropForce = 2f;

        [Header("Settings")]
        [SerializeField] private bool _pauseGameWhenOpen = false;

        #endregion

        #region Private Fields

        private bool _isOpen;
        private PlayerInventory _playerInventory;
        private InputAction _toggleAction;
        private InputAction _closeAction;

        #endregion

        #region Properties

        public bool IsOpen => _isOpen;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            _playerInventory = PlayerInventory.Instance;

            if (_playerInventory != null)
            {
                _playerInventory.OnSlotChanged += HandleSlotChanged;
                _playerInventory.OnIngredientChanged += HandleIngredientChanged;
                _playerInventory.OnARGramsChanged += HandleARGramsChanged;
            }

            // If no panel assigned, assume this GO has a child panel or use self
            if (_inventoryPanel == null)
            {
                // Try to find a child named "Panel" or use first child
                Transform panelTransform = transform.Find("Panel");
                if (panelTransform != null)
                {
                    _inventoryPanel = panelTransform.gameObject;
                }
                else if (transform.childCount > 0)
                {
                    _inventoryPanel = transform.GetChild(0).gameObject;
                }
            }

            // Set up slot indices and subscribe to right-click events
            SetupSlotUIs();

            // Set up context menu events
            SetupContextMenu();

            // Initialize UI with current inventory state
            RefreshUI();

            // Start closed
            CloseInventory();
        }

        private void SetupSlotUIs()
        {
            if (_slotUIs == null) return;

            for (int i = 0; i < _slotUIs.Length; i++)
            {
                if (_slotUIs[i] != null)
                {
                    _slotUIs[i].SetSlotIndex(i);
                    _slotUIs[i].OnRightClicked += HandleSlotRightClicked;
                }
            }
        }

        private void SetupContextMenu()
        {
            if (_contextMenu != null)
            {
                _contextMenu.OnDropRequested += HandleDropRequested;
                _contextMenu.OnExamineRequested += HandleExamineRequested;
            }
        }

        private void OnEnable()
        {
            _toggleAction = new InputAction("ToggleInventory", InputActionType.Button, "<Keyboard>/tab");
            _toggleAction.performed += OnToggleInventory;
            _toggleAction.Enable();

            // ESC closes inventory (intercepts before pause menu)
            _closeAction = new InputAction("CloseInventory", InputActionType.Button, "<Keyboard>/escape");
            _closeAction.performed += OnCloseInventory;
            _closeAction.Enable();
        }

        private void OnDisable()
        {
            if (_toggleAction != null)
            {
                _toggleAction.performed -= OnToggleInventory;
                _toggleAction.Disable();
                _toggleAction.Dispose();
            }

            if (_closeAction != null)
            {
                _closeAction.performed -= OnCloseInventory;
                _closeAction.Disable();
                _closeAction.Dispose();
            }

            if (_playerInventory != null)
            {
                _playerInventory.OnSlotChanged -= HandleSlotChanged;
                _playerInventory.OnIngredientChanged -= HandleIngredientChanged;
                _playerInventory.OnARGramsChanged -= HandleARGramsChanged;
            }

            // Unsubscribe from slot events
            if (_slotUIs != null)
            {
                foreach (var slotUI in _slotUIs)
                {
                    if (slotUI != null)
                    {
                        slotUI.OnRightClicked -= HandleSlotRightClicked;
                    }
                }
            }

            // Unsubscribe from context menu events
            if (_contextMenu != null)
            {
                _contextMenu.OnDropRequested -= HandleDropRequested;
                _contextMenu.OnExamineRequested -= HandleExamineRequested;
            }
        }

        #endregion

        #region Input Handling

        private void OnToggleInventory(InputAction.CallbackContext context)
        {
            // Don't open inventory if game is paused
            if (!_isOpen && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
                return;

            ToggleInventory();
        }

        private void OnCloseInventory(InputAction.CallbackContext context)
        {
            // Only close if inventory is open
            if (_isOpen)
            {
                CloseInventory();
            }
        }

        #endregion

        #region Public Methods

        public void ToggleInventory()
        {
            if (_isOpen)
            {
                CloseInventory();
            }
            else
            {
                OpenInventory();
            }
        }

        public void OpenInventory()
        {
            _isOpen = true;

            if (_inventoryPanel != null)
            {
                _inventoryPanel.SetActive(true);
            }

            RefreshUI();

            if (_pauseGameWhenOpen)
            {
                Time.timeScale = 0f;
            }

            // Disable player input (movement, camera, etc.)
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(false);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseInventory()
        {
            _isOpen = false;

            // Close context menu and examiner first
            if (_contextMenu != null)
            {
                _contextMenu.Hide();
            }

            if (_holographicExaminer != null)
            {
                _holographicExaminer.Hide();
            }

            if (_inventoryPanel != null)
            {
                _inventoryPanel.SetActive(false);
            }

            if (_pauseGameWhenOpen)
            {
                Time.timeScale = 1f;
            }

            // Re-enable player input
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(true);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void RefreshUI()
        {
            if (_playerInventory == null) return;

            // Update slot UIs
            if (_slotUIs != null)
            {
                for (int i = 0; i < _slotUIs.Length && i < _playerInventory.SlotCount; i++)
                {
                    if (_slotUIs[i] != null)
                    {
                        InventorySlot slot = _playerInventory.GetSlot(i);
                        _slotUIs[i].UpdateSlot(slot);
                    }
                }
            }

            // Update ingredient counters
            if (_ingredientCounterUIs != null)
            {
                foreach (var counterUI in _ingredientCounterUIs)
                {
                    if (counterUI != null)
                    {
                        int count = _playerInventory.GetIngredientCount(counterUI.IngredientType);
                        int cap = _playerInventory.GetIngredientCap(counterUI.IngredientType);
                        counterUI.UpdateCounter(count, cap);
                    }
                }
            }

            // Update AR grams counter
            if (_arGramsCounterUI != null)
            {
                _arGramsCounterUI.UpdateCounter(_playerInventory.ARGrams, _playerInventory.ARGramsCap);
            }
        }

        #endregion

        #region Event Handlers

        private void HandleSlotChanged(int slotIndex, InventorySlot slot)
        {
            if (_slotUIs != null && slotIndex < _slotUIs.Length && _slotUIs[slotIndex] != null)
            {
                _slotUIs[slotIndex].UpdateSlot(slot);
            }
        }

        private void HandleIngredientChanged(IngredientType type, int newCount)
        {
            if (_ingredientCounterUIs == null) return;

            foreach (var counterUI in _ingredientCounterUIs)
            {
                if (counterUI != null && counterUI.IngredientType == type)
                {
                    int cap = _playerInventory.GetIngredientCap(type);
                    counterUI.UpdateCounter(newCount, cap);
                    break;
                }
            }
        }

        private void HandleARGramsChanged(int newAmount)
        {
            if (_arGramsCounterUI != null)
            {
                _arGramsCounterUI.UpdateCounter(newAmount, _playerInventory.ARGramsCap);
            }
        }

        private void HandleSlotRightClicked(int slotIndex, Vector2 screenPosition)
        {
            // Close examiner if open
            if (_holographicExaminer != null && _holographicExaminer.IsOpen)
            {
                _holographicExaminer.Hide();
            }

            // Show context menu at click position
            if (_contextMenu != null)
            {
                _contextMenu.Show(slotIndex, screenPosition);
            }
        }

        private void HandleDropRequested(int slotIndex)
        {
            if (_playerInventory == null) return;

            InventorySlot slot = _playerInventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return;

            InventoryItemData itemData = slot.ItemData;

            // Remove item from inventory
            _playerInventory.RemoveItemFromSlot(slotIndex);

            // Spawn the item in the world
            SpawnDroppedItem(itemData);

            Debug.Log($"[InventoryUI] Dropped {itemData.displayName}");
        }

        private void HandleExamineRequested(int slotIndex)
        {
            Debug.Log($"[InventoryUI] Examine requested for slot {slotIndex}");

            if (_playerInventory == null)
            {
                Debug.LogWarning("[InventoryUI] PlayerInventory is null!");
                return;
            }

            if (_holographicExaminer == null)
            {
                Debug.LogWarning("[InventoryUI] HolographicExaminer is not assigned!");
                return;
            }

            InventorySlot slot = _playerInventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                Debug.LogWarning($"[InventoryUI] Slot {slotIndex} is null or empty!");
                return;
            }

            Debug.Log($"[InventoryUI] Opening holographic examiner for: {slot.ItemData.displayName}");

            // Close inventory panel while examining
            if (_inventoryPanel != null)
            {
                _inventoryPanel.SetActive(false);
            }

            _holographicExaminer.Show(slot.ItemData);
        }

        private void SpawnDroppedItem(InventoryItemData itemData)
        {
            if (itemData.worldPrefab == null)
            {
                Debug.LogWarning($"[InventoryUI] Cannot drop {itemData.displayName} - no worldPrefab assigned");
                return;
            }

            // Find player camera for drop direction
            Camera playerCamera = Camera.main;
            if (playerCamera == null) return;

            Vector3 spawnPosition = playerCamera.transform.position +
                                    playerCamera.transform.forward * _dropDistance;

            // Spawn the prefab
            GameObject droppedItem = Instantiate(itemData.worldPrefab, spawnPosition, Quaternion.identity);

            // Apply some force to make it feel natural
            Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(playerCamera.transform.forward * _dropForce, ForceMode.Impulse);
            }
        }

        #endregion
    }
}
