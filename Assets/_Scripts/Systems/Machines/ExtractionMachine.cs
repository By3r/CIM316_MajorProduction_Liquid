/*
 * NOT YET INTEGRATED - Commented out for later integration
 * Remove #if false and #endif when ready to integrate ExtractionMachine system
 */
#if false

using System;
using UnityEngine;
using UnityEngine.Events;
using _Scripts.Systems.Inventory;

namespace _Scripts.Systems.Machines
{
    /// <summary>
    /// Extraction Machine for extracting AR from mineral veins.
    /// Player must insert AR Container and stay in the room during extraction.
    /// Leaving the room severs the connection and stops extraction.
    /// </summary>
    public class ExtractionMachine : MonoBehaviour
    {
        #region Events

        public event Action<ExtractionState> OnStateChanged;
        public event Action<int> OnExtractionProgress;

        #endregion

        #region Serialized Fields

        [Header("Machine Configuration")]
        [SerializeField] private string _machineId;
        [SerializeField] private int _arPerSecond = 5;
        [SerializeField] private int _totalARAvailable = 100;

        [Header("Room Detection")]
        [SerializeField] private Collider _roomBounds;
        [SerializeField] private string _playerTag = "Player";

        [Header("Visual Feedback")]
        [SerializeField] private GameObject _idleVisual;
        [SerializeField] private GameObject _extractingVisual;
        [SerializeField] private GameObject _containerSlotVisual;
        [SerializeField] private ParticleSystem _extractionParticles;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _extractionLoopSound;
        [SerializeField] private AudioClip _insertContainerSound;
        [SerializeField] private AudioClip _removeContainerSound;
        [SerializeField] private AudioClip _linkSeveredSound;

        [Header("Events")]
        [SerializeField] private UnityEvent _onExtractionStarted;
        [SerializeField] private UnityEvent _onExtractionComplete;
        [SerializeField] private UnityEvent _onLinkSevered;

        #endregion

        #region Private Fields

        private ExtractionState _currentState = ExtractionState.Idle;
        private PlayerInventory _linkedPlayer;
        private int _arExtracted = 0;
        private int _arRemaining;
        private float _extractionTimer = 0f;
        private bool _playerInRoom = false;

        #endregion

        #region Properties

        public ExtractionState CurrentState => _currentState;
        public int ARRemaining => _arRemaining;
        public int ARExtracted => _arExtracted;
        public bool IsExtracting => _currentState == ExtractionState.Extracting;
        public string InteractionPrompt
        {
            get
            {
                return _currentState switch
                {
                    ExtractionState.Idle => "Insert AR Container",
                    ExtractionState.ContainerInserted => "Start Extraction",
                    ExtractionState.Extracting => "Extracting... Stay in room",
                    ExtractionState.Complete => "Remove AR Container",
                    ExtractionState.Depleted => "Mineral depleted",
                    _ => ""
                };
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (string.IsNullOrEmpty(_machineId))
            {
                _machineId = System.Guid.NewGuid().ToString();
            }

            _arRemaining = _totalARAvailable;
        }

        private void Start()
        {
            UpdateVisuals();
        }

        private void Update()
        {
            if (_currentState == ExtractionState.Extracting)
            {
                ProcessExtraction();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(_playerTag))
            {
                _playerInRoom = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(_playerTag))
            {
                _playerInRoom = false;

                if (_currentState == ExtractionState.Extracting)
                {
                    SeverLink();
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to insert an AR Container from the player's inventory.
        /// </summary>
        public bool TryInsertContainer(PlayerInventory inventory)
        {
            if (_currentState != ExtractionState.Idle || inventory == null) return false;

            // Find an ARContainer in inventory
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty && slot.ItemData.itemType == PhysicalItemType.ARContainer)
                {
                    inventory.RemoveItemFromSlot(i);
                    _linkedPlayer = inventory;
                    SetState(ExtractionState.ContainerInserted);
                    PlaySound(_insertContainerSound);
                    Debug.Log($"[ExtractionMachine] Container inserted");
                    return true;
                }
            }

            Debug.Log($"[ExtractionMachine] No AR Container in inventory");
            return false;
        }

        /// <summary>
        /// Starts the extraction process if container is inserted.
        /// </summary>
        public bool StartExtraction()
        {
            if (_currentState != ExtractionState.ContainerInserted) return false;
            if (_arRemaining <= 0)
            {
                SetState(ExtractionState.Depleted);
                return false;
            }

            SetState(ExtractionState.Extracting);
            _extractionTimer = 0f;
            _arExtracted = 0;

            if (_audioSource != null && _extractionLoopSound != null)
            {
                _audioSource.clip = _extractionLoopSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }

            _onExtractionStarted?.Invoke();
            Debug.Log($"[ExtractionMachine] Extraction started. {_arRemaining}g AR available.");
            return true;
        }

        /// <summary>
        /// Removes the container and transfers extracted AR to player.
        /// </summary>
        public bool TryRemoveContainer(PlayerInventory inventory)
        {
            if (_currentState != ExtractionState.Complete && _currentState != ExtractionState.ContainerInserted)
                return false;

            if (inventory == null) return false;

            // Transfer AR grams to player
            if (_arExtracted > 0)
            {
                int added = inventory.AddARGrams(_arExtracted);
                Debug.Log($"[ExtractionMachine] Transferred {added}g AR to player");
            }

            // Return container to inventory (simplified - just assume they have room)
            _linkedPlayer = null;
            _arExtracted = 0;
            SetState(ExtractionState.Idle);
            PlaySound(_removeContainerSound);

            return true;
        }

        /// <summary>
        /// Main interaction handler - context-sensitive based on state.
        /// </summary>
        public void Interact(PlayerInventory inventory)
        {
            switch (_currentState)
            {
                case ExtractionState.Idle:
                    TryInsertContainer(inventory);
                    break;
                case ExtractionState.ContainerInserted:
                    StartExtraction();
                    break;
                case ExtractionState.Complete:
                    TryRemoveContainer(inventory);
                    break;
            }
        }

        #endregion

        #region Private Methods

        private void ProcessExtraction()
        {
            if (!_playerInRoom)
            {
                SeverLink();
                return;
            }

            _extractionTimer += Time.deltaTime;

            if (_extractionTimer >= 1f)
            {
                _extractionTimer = 0f;

                int toExtract = Mathf.Min(_arPerSecond, _arRemaining);
                _arExtracted += toExtract;
                _arRemaining -= toExtract;

                OnExtractionProgress?.Invoke(_arExtracted);
                Debug.Log($"[ExtractionMachine] Extracted {_arExtracted}g total. {_arRemaining}g remaining.");

                if (_arRemaining <= 0)
                {
                    CompleteExtraction();
                }
            }
        }

        private void CompleteExtraction()
        {
            SetState(ExtractionState.Complete);
            StopExtractionAudio();
            _onExtractionComplete?.Invoke();
            Debug.Log($"[ExtractionMachine] Extraction complete! {_arExtracted}g extracted.");
        }

        private void SeverLink()
        {
            Debug.Log($"[ExtractionMachine] Link severed! Player left room. {_arExtracted}g lost.");

            // AR extracted is lost when link is severed
            _arExtracted = 0;
            SetState(ExtractionState.Idle);
            _linkedPlayer = null;

            StopExtractionAudio();
            PlaySound(_linkSeveredSound);
            _onLinkSevered?.Invoke();
        }

        private void SetState(ExtractionState newState)
        {
            _currentState = newState;
            UpdateVisuals();
            OnStateChanged?.Invoke(_currentState);
        }

        private void UpdateVisuals()
        {
            if (_idleVisual != null)
            {
                _idleVisual.SetActive(_currentState == ExtractionState.Idle || _currentState == ExtractionState.Depleted);
            }

            if (_extractingVisual != null)
            {
                _extractingVisual.SetActive(_currentState == ExtractionState.Extracting);
            }

            if (_containerSlotVisual != null)
            {
                bool hasContainer = _currentState != ExtractionState.Idle && _currentState != ExtractionState.Depleted;
                _containerSlotVisual.SetActive(hasContainer);
            }

            if (_extractionParticles != null)
            {
                if (_currentState == ExtractionState.Extracting)
                {
                    _extractionParticles.Play();
                }
                else
                {
                    _extractionParticles.Stop();
                }
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        private void StopExtractionAudio()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
                _audioSource.loop = false;
            }
        }

        #endregion
    }

    public enum ExtractionState
    {
        Idle,               // No container, waiting for player
        ContainerInserted,  // Container in, ready to start
        Extracting,         // Actively extracting AR
        Complete,           // Extraction finished, container ready for removal
        Depleted            // No more AR in this vein
    }
}

#endif
