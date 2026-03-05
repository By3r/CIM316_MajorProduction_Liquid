using UnityEngine;
using _Scripts.Core.Persistence;

public class GameSaveDebugPrinter : MonoBehaviour
{
    private void Start()
    {
        GameSaveData data = SaveSystem.LoadGame();
        if (data != null)
        {
        }
        else
        {
        }
    }
}