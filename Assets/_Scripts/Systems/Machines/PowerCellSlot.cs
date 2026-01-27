using System;
using UnityEngine;
using _Scripts.Systems.Inventory;

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

        #endregion

        #region Private Fields

        private InventoryItemData _insertedPowerCell;

        #endregion

        #region Properties

        public string SlotId => _slotId;
        public bool IsPowered => _isPowered;
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
            UpdateVisuals();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to insert a PowerCell from the player's inventory.
        /// </summary>
        public bool TryInsertPowerCell(PlayerInventory inventory)
        {
            if (_isPowered || inventory == null) return false;

            // Find a PowerCell in inventory
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty && slot.ItemData.itemType == PhysicalItemType.PowerCell)
                {
                    _insertedPowerCell = inventory.RemoveItemFromSlot(i);
                    if (_insertedPowerCell != null)
                    {
                        SetPowered(true);
                        PlaySound(_insertSound);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Removes the PowerCell and returns it to inventory if possible.
        /// </summary>
        public bool TryRemovePowerCell(PlayerInventory inventory)
        {
            if (!_isPowered || _insertedPowerCell == null) return false;

            if (inventory != null && inventory.HasRoomFor(_insertedPowerCell))
            {
                inventory.TryAddItem(_insertedPowerCell);
                _insertedPowerCell = null;
                SetPowered(false);
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

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isPowered ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, _interactionRange);
        }

        #endregion
    }
}
