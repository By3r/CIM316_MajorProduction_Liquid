using System;
using System.Collections;
using UnityEngine;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Player;

namespace _Scripts.Systems.Machines
{
    /// <summary>
    /// A slot that accepts PowerCells to power machines or elevators.
    /// Player can insert/remove PowerCells via interaction.
    /// </summary>
    public class PowerCellSlot : MonoBehaviour
    {
        #region Events

        public event Action<bool> OnPowerStateChanged;

        #endregion

        #region Serialized Fields

        [Header("Slot Configuration")]
        [SerializeField] private string _slotId;
        [SerializeField] private bool _isPowered = false;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject _emptyVisual;
        [SerializeField] private GameObject _poweredVisual;
        [SerializeField] private Light _powerLight;
        [SerializeField] private Color _poweredLightColor = Color.green;
        [SerializeField] private Color _unpoweredLightColor = Color.red;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _insertSound;
        [SerializeField] private AudioClip _removeSound;

        [Header("Interaction")]
        [SerializeField] private float _interactionRange = 2f;
        [SerializeField] private string _interactionPrompt = "Insert PowerCell";
        [SerializeField] private string _removePrompt = "Remove PowerCell";

        [Header("Insert Animation")]
        [Tooltip("Pre-placed PowerCell model in the slot (starts deactivated).")]
        [SerializeField] private GameObject _powerCellModel;
        [Tooltip("Starting local position (where the PowerCell appears before inserting)")]
        [SerializeField] private Vector3 _startPosition;
        [Tooltip("Ending local position (where the PowerCell ends up when fully inserted)")]
        [SerializeField] private Vector3 _endPosition;
        [Tooltip("Duration of the insert/eject animation")]
        [SerializeField] private float _animationDuration = 0.4f;

        #endregion

        #region Private Fields

        private InventoryItemData _insertedPowerCell;
        private bool _isAnimating;
        private Quaternion _modelLocalRotation;
        private PlayerInventory _pendingInventory;
        private int _pendingSlotIndex;

        #endregion

        #region Properties

        public string SlotId => _slotId;
        public bool IsPowered => _isPowered;
        public bool IsAnimating => _isAnimating;
        public float InteractionRange => _interactionRange;
        public string CurrentPrompt => _isPowered ? _removePrompt : _interactionPrompt;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (string.IsNullOrEmpty(_slotId))
            {
                _slotId = System.Guid.NewGuid().ToString();
            }
        }

        private void Start()
        {
            // Cache the rotation of the PowerCell model
            if (_powerCellModel != null)
            {
                _modelLocalRotation = _powerCellModel.transform.localRotation;

                // Ensure it starts deactivated if not powered
                if (!_isPowered)
                {
                    _powerCellModel.SetActive(false);
                }
            }

            UpdateVisuals();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to insert a PowerCell from the player's inventory.
        /// Plays a screw-in animation if the PowerCell model is configured.
        /// The item is only removed from inventory after the animation completes.
        /// </summary>
        public bool TryInsertPowerCell(PlayerInventory inventory)
        {
            if (_isPowered || inventory == null || _isAnimating) return false;

            // Find a PowerCell in inventory
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty && slot.ItemData.itemType == PhysicalItemType.PowerCell)
                {
                    // Store reference for after animation completes
                    _pendingInventory = inventory;
                    _pendingSlotIndex = i;

                    // Start screw-in animation if model is assigned
                    if (_powerCellModel != null)
                    {
                        StartCoroutine(PlayScrewInAnimation());
                    }
                    else
                    {
                        // No animation, remove from inventory and power immediately
                        _insertedPowerCell = inventory.RemoveItemFromSlot(i);
                        SetPowered(true);
                    }
                    PlaySound(_insertSound);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes the PowerCell and returns it to inventory if possible.
        /// Plays a screw-out animation if configured.
        /// The item is only added to inventory after the animation completes.
        /// </summary>
        public bool TryRemovePowerCell(PlayerInventory inventory)
        {
            if (!_isPowered || _insertedPowerCell == null || _isAnimating) return false;

            if (inventory != null && inventory.HasRoomFor(_insertedPowerCell))
            {
                // Store reference for after animation completes
                _pendingInventory = inventory;

                // Start screw-out animation if model is assigned
                if (_powerCellModel != null)
                {
                    StartCoroutine(PlayScrewOutAnimation());
                }
                else
                {
                    // No animation, add to inventory and unpower immediately
                    inventory.TryAddItem(_insertedPowerCell);
                    _insertedPowerCell = null;
                    SetPowered(false);
                }
                PlaySound(_removeSound);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Toggles PowerCell insertion/removal based on current state.
        /// </summary>
        public bool TogglePowerCell(PlayerInventory inventory)
        {
            if (_isPowered)
            {
                return TryRemovePowerCell(inventory);
            }
            else
            {
                return TryInsertPowerCell(inventory);
            }
        }

        /// <summary>
        /// Force set power state without affecting inventory (for loading saves).
        /// </summary>
        public void SetPoweredState(bool powered, InventoryItemData powerCellData = null)
        {
            _insertedPowerCell = powerCellData;
            SetPowered(powered);
        }

        #endregion

        #region Private Methods

        private void SetPowered(bool powered)
        {
            _isPowered = powered;
            UpdateVisuals();
            OnPowerStateChanged?.Invoke(_isPowered);
        }

        private void UpdateVisuals()
        {
            if (_emptyVisual != null)
            {
                _emptyVisual.SetActive(!_isPowered);
            }

            if (_poweredVisual != null)
            {
                _poweredVisual.SetActive(_isPowered);
            }

            if (_powerLight != null)
            {
                _powerLight.color = _isPowered ? _poweredLightColor : _unpoweredLightColor;
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        #region Insert Animation

        /// <summary>
        /// Plays the insert animation: PowerCell slides from start position (A) to end position (B).
        /// Item is removed from inventory only after animation completes.
        /// </summary>
        private IEnumerator PlayScrewInAnimation()
        {
            _isAnimating = true;

            // Activate and position at start (A)
            _powerCellModel.transform.localPosition = _startPosition;
            _powerCellModel.transform.localRotation = _modelLocalRotation;
            _powerCellModel.SetActive(true);

            // Animate A -> B
            float elapsed = 0f;
            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);

                _powerCellModel.transform.localPosition = Vector3.Lerp(_startPosition, _endPosition, t);

                yield return null;
            }

            // Snap to final position (B)
            _powerCellModel.transform.localPosition = _endPosition;

            // NOW remove from inventory and power on
            if (_pendingInventory != null)
            {
                _insertedPowerCell = _pendingInventory.RemoveItemFromSlot(_pendingSlotIndex);
                _pendingInventory = null;
            }

            SetPowered(true);
            _isAnimating = false;
        }

        /// <summary>
        /// Plays the eject animation: PowerCell slides from end position (B) back to start position (A).
        /// Item is added to inventory only after animation completes.
        /// </summary>
        private IEnumerator PlayScrewOutAnimation()
        {
            _isAnimating = true;

            // Animate B -> A
            float elapsed = 0f;
            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);

                _powerCellModel.transform.localPosition = Vector3.Lerp(_endPosition, _startPosition, t);

                yield return null;
            }

            // Deactivate the model at position A
            _powerCellModel.SetActive(false);

            // NOW add to inventory and unpower
            if (_pendingInventory != null && _insertedPowerCell != null)
            {
                _pendingInventory.TryAddItem(_insertedPowerCell);
                _pendingInventory = null;
            }

            _insertedPowerCell = null;
            SetPowered(false);
            _isAnimating = false;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isPowered ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, _interactionRange);
        }

        #endregion
    }
}
