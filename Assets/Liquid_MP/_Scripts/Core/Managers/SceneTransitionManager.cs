using _Scripts.Core.Managers;
using _Scripts.Core.Persistence;
using _Scripts.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Scripts.Core.SceneFlow
{
    /// <summary>
    /// Global singleton responsible for scene transitions.
    /// ++ Decides whether a loaded save should go to the Tutorial or Game scene.
    /// ++ Sets ActiveSaveSlotBridge.ActiveSlot so the tutorial knows which save to read.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        #region Variables
        public static SceneTransitionManager Instance { get; private set; }

        [Header("Scene Names")]
        [SerializeField] private string menuSceneName = "Menu";
        [SerializeField] private string tutorialSceneName = "Tutorial";
        [SerializeField] private string gameSceneName = "Game";
        #endregion

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

        public void LoadMenuScene()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Loading);
            }

            ActiveSaveSlotBridge.Clear();
            SceneManager.LoadScene(menuSceneName);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.MainMenu);
            }
        }

        public void StartNewGameWithName(string playerName)
        {
            int slotIndex = SaveSystem.GetFirstEmptySlotIndex();

            if (slotIndex == -1)
            {
                slotIndex = 0;
            }

            GameSaveData data = new GameSaveData(playerName, false, StoryStage.Tutorial);
            SaveSystem.SaveGame(data, slotIndex);

            ActiveSaveSlotBridge.Set(slotIndex);

            LoadTutorialScene();
        }

        public void ContinueFromSave()
        {
            int mostRecentSlot = SaveSystem.GetMostRecentSaveSlotIndex();

            if (mostRecentSlot == -1)
            {
                Debug.LogWarning("Continue requested but no save exists.");
                return;
            }

            ContinueFromSaveSlot(mostRecentSlot);
        }

        public void ContinueFromSaveSlot(int slotIndex)
        {
            GameSaveData data = SaveSystem.LoadGame(slotIndex);

            if (data == null)
            {
                Debug.LogWarning($"Continue requested but slot {slotIndex} could not be loaded.");
                return;
            }

            ActiveSaveSlotBridge.Set(slotIndex);

            if (data.HasCompletedTutorial)
            {
                LoadGameScene();
            }
            else
            {
                LoadTutorialScene();
            }
        }

        public void LoadTutorialScene()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Loading);
            }

            SceneManager.LoadScene(tutorialSceneName);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Gameplay);
            }
        }

        public void LoadGameScene()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Loading);
            }

            SceneManager.LoadScene(gameSceneName);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Gameplay);
            }
        }
    }
}