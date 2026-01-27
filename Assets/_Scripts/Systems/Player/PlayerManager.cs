using _Scripts.Core.Managers;
using UnityEngine;

namespace _Scripts.Systems.Player
{
    /// <summary>
    /// Manages player instantiation, registration, and respawning.
    /// Provides singleton access to the current active player and handles player lifecycle.
    /// Automatically respawns the player when death events are received.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerManager : MonoBehaviour
    {
        #region Singleton

        /// <summary>
        /// Singleton instance of the PlayerManager.
        /// </summary>
        public static PlayerManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [SerializeField] private GameObject _playerPrefab;

        [SerializeField] private Vector3 _defaultSpawnPosition = new(0, 1, 0);
        [SerializeField] private Quaternion _defaultSpawnRotation = Quaternion.identity;

        #endregion

        #region Private Fields

        private PlayerController _currentPlayer;
        private bool _isPlayerFrozen;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the currently active player controller instance.
        /// Returns null if no player has been spawned yet.
        /// </summary>
        public PlayerController CurrentPlayer => _currentPlayer;

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
        }

        private void SubscribeToEvents()
        {
            if (GameManager.Instance?.EventManager != null)
            {
                GameManager.Instance.EventManager.Subscribe(GameEvents.OnPlayerDeath, HandlePlayerDeath);
                GameManager.Instance.EventManager.Subscribe("OnFloorGenerationStarted", HandleFloorGenerationStarted);
                GameManager.Instance.EventManager.Subscribe("OnFloorGenerationComplete", HandleFloorGenerationComplete);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Registers a player controller as the currently active player.
        /// Called by PlayerController during its Awake.
        /// </summary>
        /// <param name="player">The PlayerController instance to register.</param>
        public void RegisterPlayer(PlayerController player)
        {
            _currentPlayer = player;
        }

        /// <summary>
        /// Gets the current player, or spawns a new one if none exists.
        /// Useful for lazy initialization of the player.
        /// </summary>
        /// <param name="spawnPosition">World position to spawn at. Uses default if null.</param>
        /// <param name="spawnRotation">Rotation to spawn with. Uses default if null.</param>
        /// <returns>The active PlayerController instance.</returns>
        public PlayerController GetOrSpawnPlayer(Vector3? spawnPosition = null, Quaternion? spawnRotation = null)
        {
            if (_currentPlayer != null)
            {
                return _currentPlayer;
            }
            return SpawnPlayer(spawnPosition, spawnRotation);
        }

        /// <summary>
        /// Spawns a new player at the specified position and rotation.
        /// Uses the assigned prefab if available, otherwise creates a basic player with default components.
        /// Publishes the OnPlayerRespawn event when complete.
        /// </summary>
        /// <param name="spawnPosition">World position to spawn at. Uses default if null.</param>
        /// <param name="spawnRotation">Rotation to spawn with. Uses default if null.</param>
        /// <returns>The newly spawned PlayerController instance.</returns>
        public PlayerController SpawnPlayer(Vector3? spawnPosition = null, Quaternion? spawnRotation = null)
        {
            Vector3 finalPosition = spawnPosition ?? _defaultSpawnPosition;
            Quaternion finalRotation = spawnRotation ?? _defaultSpawnRotation;

            GameObject playerObj = _playerPrefab != null 
                ? Instantiate(_playerPrefab, finalPosition, finalRotation) 
                : CreateBasicPlayer(finalPosition, finalRotation);

            PlayerController newPlayer = playerObj.GetComponent<PlayerController>();
            if (newPlayer == null)
            {
                newPlayer = playerObj.AddComponent<PlayerController>();
            }
            
            // The spawned player will register itself via its own Awake method :P
            
            GameManager.Instance?.EventManager?.Publish(GameEvents.OnPlayerRespawn);
            return newPlayer;
        }

        #endregion

        #region Private Implementation

        private GameObject CreateBasicPlayer(Vector3 position, Quaternion rotation)
        {
            GameObject playerObj = new GameObject("Player");
            playerObj.transform.SetPositionAndRotation(position, rotation);
            CharacterController controller = playerObj.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0, 1, 0);
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(playerObj.transform);
            capsule.transform.localPosition = new Vector3(0, 1, 0);
            Destroy(capsule.GetComponent<Collider>());
            return playerObj;
        }

        private void HandlePlayerDeath()
        {
            if (_currentPlayer != null)
            {
                Destroy(_currentPlayer.gameObject);
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

        /// <summary>
        /// Called when floor generation starts. Freezes the player to prevent falling.
        /// </summary>
        private void HandleFloorGenerationStarted()
        {
            FreezePlayer();
        }

        /// <summary>
        /// Called when floor generation completes. Unfreezes the player.
        /// </summary>
        private void HandleFloorGenerationComplete()
        {
            UnfreezePlayer();
        }

        /// <summary>
        /// Freezes the player in place during floor transitions.
        /// Disables CharacterController and player input to prevent falling through the world.
        /// </summary>
        public void FreezePlayer()
        {
            if (_currentPlayer == null || _isPlayerFrozen) return;

            _isPlayerFrozen = true;

            // Disable CharacterController to prevent physics/gravity
            CharacterController characterController = _currentPlayer.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            // Disable player input
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(false);
            }
        }

        /// <summary>
        /// Unfreezes the player after floor generation is complete.
        /// Re-enables CharacterController and player input.
        /// </summary>
        public void UnfreezePlayer()
        {
            if (_currentPlayer == null || !_isPlayerFrozen) return;

            _isPlayerFrozen = false;

            // Re-enable CharacterController
            CharacterController characterController = _currentPlayer.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = true;
            }

            // Re-enable player input
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(true);
            }
        }

        #endregion
    }
}