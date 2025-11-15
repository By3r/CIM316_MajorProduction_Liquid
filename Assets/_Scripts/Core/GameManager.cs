using UnityEngine;

namespace _Scripts.Core
{
    /// <summary>
    /// Central manager for global game state and system initialization.
    /// Provides singleton access to the EventManager and InputManager.
    /// Handles transitions between different game states (Gameplay, Paused, MainMenu, etc.).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class GameManager : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of the GameManager.
        /// </summary>
        public static GameManager Instance { get; private set; }

        /// <summary>
        /// Gets the current game state (Gameplay, Paused, MainMenu, etc.).
        /// </summary>
        public GameState CurrentState { get; private set; }
        
        /// <summary>
        /// Gets the current floor number the player is on.
        /// </summary>
        public int CurrentFloorNumber { get; private set; }

        /// <summary>
        /// Gets the global EventManager for inter-system communication.
        /// </summary>
        public EventManager EventManager { get; private set; }
        
        /// <summary>
        /// Gets the global InputManager for handling player input.
        /// </summary>
        public InputManager InputManager { get; private set; }

        private void Awake()
        {
            InitializeSingleton();
            InitializeManagers();
        }

        private void InitializeSingleton()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void InitializeManagers()
        {
            EventManager = GetComponent<EventManager>();
            if (EventManager == null)
            {
                EventManager = gameObject.AddComponent<EventManager>();
            }

            InputManager = GetComponent<InputManager>();
            if (InputManager == null)
            {
                InputManager = gameObject.AddComponent<InputManager>();
            }
        }

        /// <summary>
        /// Transitions the game to a new state.
        /// Handles input enabling/disabling, cursor locking, and timescale adjustments based on the state.
        /// Publishes the OnGameStateChanged event to notify all listeners.
        /// </summary>
        /// <param name="newState">The GameState to transition to.</param>
        public void SetGameState(GameState newState)
        {
            GameState previousState = CurrentState;
            CurrentState = newState;
            
            EventManager?.Publish(GameEvents.OnGameStateChanged, newState);

            HandleGameStateChange(newState);
        }

        private void HandleGameStateChange(GameState newState)
        {
            switch (newState)
            {
                case GameState.Gameplay:
                    if (InputManager != null)
                    {
                        InputManager.EnablePlayerInput(true);
                        InputManager.LockCursor(true);
                    }
                    break;

                case GameState.Paused:
                    if (InputManager != null)
                    {
                        InputManager.EnablePlayerInput(false);
                        InputManager.LockCursor(false);
                    }
                    break;

                case GameState.MainMenu:
                    if (InputManager != null)
                    {
                        InputManager.EnablePlayerInput(false);
                        InputManager.LockCursor(false);
                    }
                    Time.timeScale = 1f;
                    break;

                case GameState.GameOver:
                case GameState.Victory:
                    if (InputManager != null)
                    {
                        InputManager.EnablePlayerInput(false);
                        InputManager.LockCursor(false);
                    }
                    Time.timeScale = 0f;
                    break;

                case GameState.Loading:
                    if (InputManager != null)
                    {
                        InputManager.EnablePlayerInput(false);
                        InputManager.EnableUIInput(false);
                    }
                    break;

                case GameState.SafeRoom:
                    if (InputManager != null)
                    {
                        InputManager.EnablePlayerInput(true);
                        InputManager.LockCursor(false);
                    }
                    Time.timeScale = 1f;
                    break;
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }

    /// <summary>
    /// Represents all possible game states throughout the application lifecycle.
    /// </summary>
    public enum GameState
    {
        /// <summary>Main menu screen.</summary>
        MainMenu,
        
        /// <summary>Scene loading in progress.</summary>
        Loading,
        
        /// <summary>Active gameplay with player control.</summary>
        Gameplay,
        
        /// <summary>Safe room area with no enemies and relaxed controls.</summary>
        SafeRoom,
        
        /// <summary>Game paused; timescale is 0.</summary>
        Paused,
        
        /// <summary>Player has died; game over state.</summary>
        GameOver,
        
        /// <summary>Player has won; victory state.</summary>
        Victory
    }
}