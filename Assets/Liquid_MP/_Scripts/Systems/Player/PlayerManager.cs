using _Scripts.Core.Managers;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.ProceduralGeneration;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using UnityEngine;

namespace _Scripts.Systems.Player
{
    /// <summary>
    /// Manages player instantiation, registration, and respawning.
    /// Provides singleton access to the current active player GameObject.
    /// Works with the Kinemation TacticalShooterPlayer — no longer depends on PlayerController.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerManager : MonoBehaviour
    {
        #region Singleton

        public static PlayerManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [SerializeField] private GameObject _playerPrefab;

        [SerializeField] private Vector3 _defaultSpawnPosition = new(0, 1, 0);
        [SerializeField] private Quaternion _defaultSpawnRotation = Quaternion.identity;

        #endregion

        #region Private Fields

        private GameObject _currentPlayer;
        private bool _isPlayerFrozen;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the currently active player GameObject.
        /// Returns null if no player has been spawned yet.
        /// </summary>
        public GameObject CurrentPlayer => _currentPlayer;

        /// <summary>
        /// Gets whether the player is currently frozen (during floor transitions).
        /// </summary>
        public bool IsPlayerFrozen => _isPlayerFrozen;

        #endregion

        #region Initialization

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            SubscribeToEvents();

            // If no player registered yet, scan the scene for one.
            if (_currentPlayer == null)
            {
                var tsp = FindObjectOfType<TacticalShooterPlayer>();
                if (tsp != null)
                {
                    _currentPlayer = tsp.gameObject;
                }
                else
                {
                    Debug.LogWarning("[PlayerManager] No TacticalShooterPlayer found in scene during Start().");
                }
            }
        }

        private void SubscribeToEvents()
        {
            if (GameManager.Instance?.EventManager != null)
            {
                GameManager.Instance.EventManager.Subscribe(GameEvents.OnPlayerDeath, HandlePlayerDeath);
                GameManager.Instance.EventManager.Subscribe("OnFloorGenerationStarted", HandleFloorGenerationStarted);
                GameManager.Instance.EventManager.Subscribe("OnFloorGenerationComplete", HandleFloorGenerationComplete);
            }
            else
            {
                Debug.LogError("[PlayerManager] GameManager.Instance or EventManager is null! Cannot subscribe to events.");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Registers a player GameObject as the currently active player.
        /// Called by TacticalShooterPlayer during its Start.
        /// </summary>
        public void RegisterPlayer(GameObject player)
        {
            _currentPlayer = player;
        }

        /// <summary>
        /// Gets the current player, or spawns a new one if none exists.
        /// </summary>
        public GameObject GetOrSpawnPlayer(Vector3? spawnPosition = null, Quaternion? spawnRotation = null)
        {
            if (_currentPlayer != null)
            {
                return _currentPlayer;
            }
            return SpawnPlayer(spawnPosition, spawnRotation);
        }

        /// <summary>
        /// Spawns a new player at the specified position and rotation.
        /// Uses the assigned prefab. The player registers itself via TacticalShooterPlayer.Start().
        /// Publishes the OnPlayerRespawn event when complete.
        /// </summary>
        public GameObject SpawnPlayer(Vector3? spawnPosition = null, Quaternion? spawnRotation = null)
        {
            Vector3 finalPosition = spawnPosition ?? _defaultSpawnPosition;
            Quaternion finalRotation = spawnRotation ?? _defaultSpawnRotation;

            if (_playerPrefab == null)
            {
                Debug.LogError("[PlayerManager] No player prefab assigned! Cannot spawn player.");
                return null;
            }

            GameObject playerObj = Instantiate(_playerPrefab, finalPosition, finalRotation);

            // The spawned player registers itself via TacticalShooterPlayer.Start()

            GameManager.Instance?.EventManager?.Publish(GameEvents.OnPlayerRespawn);
            return playerObj;
        }

        #endregion

        #region Private Implementation

        private void HandlePlayerDeath()
        {
            if (_currentPlayer != null)
            {
                Destroy(_currentPlayer);
                _currentPlayer = null;
            }
            SpawnPlayer();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance?.EventManager != null)
            {
                GameManager.Instance.EventManager.Unsubscribe(GameEvents.OnPlayerDeath, HandlePlayerDeath);
                GameManager.Instance.EventManager.Unsubscribe("OnFloorGenerationStarted", HandleFloorGenerationStarted);
                GameManager.Instance.EventManager.Unsubscribe("OnFloorGenerationComplete", HandleFloorGenerationComplete);
            }
        }

        #endregion

        #region Floor Transition Handling

        private void HandleFloorGenerationStarted()
        {
            FreezePlayer();
        }

        private void HandleFloorGenerationComplete()
        {
            MovePlayerToSafeRoom();
            UnfreezePlayer();

            // Fade the screen back in — triggered here (not in Elevator.Start())
            // because PlayerManager persists across floor gen while Elevator gets destroyed.
            if (ScreenFade.Instance.CurrentAlpha > 0f)
            {
                ScreenFade.Instance.FadeIn(0.5f);
            }

            // Restore inventory after floor generation
            var fm = FloorStateManager.Instance;
            if (fm != null && PlayerInventory.Instance != null)
            {
                InventorySaveData savedInventory = fm.GetSavedInventory();
                if (savedInventory != null)
                    PlayerInventory.Instance.RestoreFromSaveData(savedInventory);
            }

            // Restore equipment after floor generation
            if (fm != null && PlayerEquipment.Instance != null)
            {
                EquipmentSaveData savedEquipment = fm.GetSavedEquipment();
                if (savedEquipment != null)
                    PlayerEquipment.Instance.RestoreFromSaveData(savedEquipment);
            }
        }

        /// <summary>
        /// Restores the player to their saved position after floor generation.
        /// Falls back to the geometric center of the safe elevator room if no
        /// position was saved (first spawn / no previous transition).
        /// </summary>
        private void MovePlayerToSafeRoom()
        {
            if (_currentPlayer == null)
            {
                var tsp = FindObjectOfType<TacticalShooterPlayer>();
                if (tsp != null)
                {
                    _currentPlayer = tsp.gameObject;
                }
                else
                {
                    Debug.LogError("[PlayerManager] MovePlayerToSafeRoom — No player in scene.");
                    return;
                }
            }

            // Disable CharacterController to allow direct position change
            var cc = _currentPlayer.GetComponent<CharacterController>();
            bool ccWasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            // Try to restore saved position from before the floor transition
            var fm = FloorStateManager.Instance;
            if (fm != null && fm.TryGetSavedPlayerPosition(out Vector3 savedPos, out Vector3 savedRot))
            {
                _currentPlayer.transform.position = savedPos;
                _currentPlayer.transform.rotation = Quaternion.Euler(savedRot);
                fm.ClearSavedPlayerPosition();
            }
            else
            {
                // Fallback: center the player in the safe room (first spawn)
                GameObject safeRoom = GameObject.Find("SafeElevatorRoom");
                if (safeRoom == null)
                {
                    Debug.LogWarning("[PlayerManager] Could not find SafeElevatorRoom. Player not moved.");
                    if (cc != null && ccWasEnabled) cc.enabled = true;
                    return;
                }

                var boundsChecker = safeRoom.GetComponent<BoundsChecker>();
                Vector3 roomCenter;
                if (boundsChecker != null)
                {
                    Bounds worldBounds = boundsChecker.GetBounds();
                    roomCenter = worldBounds.center;
                }
                else
                {
                    roomCenter = safeRoom.transform.position;
                }

                _currentPlayer.transform.position = new Vector3(roomCenter.x, roomCenter.y + 1f, roomCenter.z);
            }

            if (cc != null && ccWasEnabled) cc.enabled = true;
        }

        /// <summary>
        /// Freezes the player in place during floor transitions.
        /// Disables CharacterController, MovementController, and TacticalShooterPlayer.
        /// </summary>
        public void FreezePlayer()
        {
            if (_currentPlayer == null || _isPlayerFrozen) return;

            _isPlayerFrozen = true;

            var cc = _currentPlayer.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            var mc = _currentPlayer.GetComponent<MovementController>();
            if (mc != null) mc.enabled = false;

            var tsp = _currentPlayer.GetComponent<TacticalShooterPlayer>();
            if (tsp != null) tsp.enabled = false;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(false);
            }
        }

        /// <summary>
        /// Unfreezes the player after floor generation is complete.
        /// Re-enables CharacterController, MovementController, and TacticalShooterPlayer.
        /// </summary>
        public void UnfreezePlayer()
        {
            if (_currentPlayer == null || !_isPlayerFrozen) return;

            _isPlayerFrozen = false;

            var cc = _currentPlayer.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            var mc = _currentPlayer.GetComponent<MovementController>();
            if (mc != null) mc.enabled = true;

            var tsp = _currentPlayer.GetComponent<TacticalShooterPlayer>();
            if (tsp != null) tsp.enabled = true;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(true);
            }
        }

        #endregion
    }
}
