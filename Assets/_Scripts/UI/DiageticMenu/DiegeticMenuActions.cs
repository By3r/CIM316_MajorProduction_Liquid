using UnityEngine;
using UnityEngine.SceneManagement;

public class DiegeticMenuActions : MonoBehaviour
{
    #region Variables
    [SerializeField] private DiegeticMenuController menuController;
    [SerializeField] private string gameSceneName = "GameScene";
    #endregion

    public void OnNewGame()
    {
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            Time.timeScale = 1.0f;
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogWarning("Game scene name is not found.");
        }
    }

    public void OnContinue()
    {
        Debug.Log("Placeholder for Load system here...");
    }

    public void OnOpenSettings()
    {
        Debug.Log("Open settings.");
    }

    public void OnQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}