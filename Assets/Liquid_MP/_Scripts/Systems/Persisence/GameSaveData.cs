using System;
using System.IO;
using UnityEngine;

namespace _Scripts.Core.Persistence
{
    [Serializable]
    public class GameSaveData
    {
        public string playerName;

        public GameSaveData(string playerName)
        {
            this.playerName = playerName;
        }
    }

    public static class SaveSystem
    {
        private const string SaveFileName = "liquid_save.json";

        private static string SaveFilePath =>
            Path.Combine(Application.persistentDataPath, SaveFileName);

        #region Returns true if a save file exists.
        public static bool SaveExists()
        {
            return File.Exists(SaveFilePath);
        }
        #endregion

        #region Creates or overwrites the save file with the given data.
        public static void SaveGame(GameSaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SaveFilePath, json);
                Debug.Log($"Saved game to: {SaveFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save game: {ex}");
            }
        }
        #endregion

        #region Loads the save file if it exists.
        public static GameSaveData LoadGame()
        {
            try
            {
                if (!File.Exists(SaveFilePath))
                {
                    Debug.LogWarning("No save file found when trying to load.");
                    return null;
                }

                string json = File.ReadAllText(SaveFilePath);
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load game: {ex}");
                return null;
            }
        }
        #endregion

        #region Deletes the existing save file if present.
        public static void DeleteSave()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                    Debug.Log("Deleted save file.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete save file: {ex}");
            }
        }
        #endregion
    }
}