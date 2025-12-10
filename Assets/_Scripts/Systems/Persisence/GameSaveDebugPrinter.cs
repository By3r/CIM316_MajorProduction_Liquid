using UnityEngine;
using _Scripts.Core.Persistence;

public class GameSaveDebugPrinter : MonoBehaviour
{
    private void Start()
    {
        GameSaveData data = SaveSystem.LoadGame();
        if (data != null)
        {
            Debug.Log($"Loaded save. Player name: {data.playerName}");
        }
        else
        {
            Debug.Log("No save file found in game scene.");
        }
    }
}