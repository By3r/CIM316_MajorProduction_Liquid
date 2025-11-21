using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuUIFunctionality : MonoBehaviour
{
    public void LoadScene(int sceneID)
    {
        SceneManager.LoadScene(sceneID);
    }
}