using _Scripts.Core.Managers;
using _Scripts.Core.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Scripts.Core.SceneFlow
{
    /// <summary>
    /// Global singleton responsible for scene transitions.
    /// ++ Decides whether a loaded save should go to the Tutorial or Game scene.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("Scene Names")]
        [SerializeField] private string menuSceneName = "Menu";
        [SerializeField] private string tutorialSceneName = "Tutorial";
        [SerializeField] private string gameSceneName = "Game";

        private void Awake()
        {
            InitializeSingleton();
        }

        private void InitializeSingleton()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Loads the main menu scene.
        /// </summary>
        public void LoadMenuScene()
        {
            SetLoadingState();
            SceneManager.LoadScene(menuSceneName);
            SetMainMenuState();
        }

        /// <summary>
        /// Loads the tutorial scene directly.
        /// </summary>
        public void LoadTutorialScene()
        {
            SetLoadingState();
            SceneManager.LoadScene(tutorialSceneName);
            SetGameplayState();
        }

        /// <summary>
        /// Loads the main gameplay scene directly.
        /// </summary>
        public void LoadGameScene()
        {
            SetLoadingState();
            SceneManager.LoadScene(gameSceneName);
            SetGameplayState();
        }

        /// <summary>
        /// Creates a new save and starts from the tutorial.
        /// </summary>
        public void StartNewGame()
        {
            GameSaveData newSave = new GameSaveData("Player", false);
            SaveSystem.SaveGame(newSave);
            LoadTutorialScene();
        }

        /// <summary>
        /// Loads the current save and routes to Tutorial or Game depending on tutorial completion.
        /// </summary>
        public void ContinueFromMostRecentSave()
        {
            if (!SaveSystem.SaveExists())
            {
                Debug.LogWarning("Continue requested but no save file exists.");
                return;
            }

            GameSaveData saveData = SaveSystem.LoadGame();

            if (saveData == null)
            {
                Debug.LogWarning("Continue requested but save data failed to load.");
                return;
            }

            if (saveData.HasCompletedTutorial)
            {
                LoadGameScene();
            }
            else
            {
                LoadTutorialScene();
            }
        }

        /// <summary>
        /// For now it routes the same way as Continue,
        /// but this is where the slot-based loading will branch out.
        /// </summary>
        public void LoadFromSave()
        {
            ContinueFromMostRecentSave();
        }

        private void SetLoadingState()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Loading);
            }
        }

        private void SetMainMenuState()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.MainMenu);
            }
        }

        private void SetGameplayState()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Gameplay);
            }
        }
    }
}