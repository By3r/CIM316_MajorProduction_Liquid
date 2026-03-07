using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace _Scripts.Core.Persistence
{
    public enum StoryStage
    {
        Tutorial,
        IntroRockyPlanet,
        MainGame
    }

    [Serializable]
    public class GameSaveData
    {
        public string PlayerName;
        public bool HasCompletedTutorial;
        public StoryStage CurrentStoryStage;
        public string SaveCreatedAt;

        public GameSaveData(string playerName, bool hasCompletedTutorial = false, StoryStage currentStoryStage = StoryStage.Tutorial)
        {
            PlayerName = playerName;
            HasCompletedTutorial = hasCompletedTutorial;
            CurrentStoryStage = currentStoryStage;
            SaveCreatedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }

        public string GetDisplayLocationName()
        {
            switch (CurrentStoryStage)
            {
                case StoryStage.Tutorial:
                    return "Tutorial";

                case StoryStage.IntroRockyPlanet:
                    return "(Intro)\nRocky Planet";

                case StoryStage.MainGame:
                    return "Main Game";

                default:
                    return "Unknown";
            }
        }
    }

    public static class SaveSystem
    {
        private const int MaxSaveSlots = 3;

        private static string GetSaveFilePath(int slotIndex)
        {
            return Path.Combine(Application.persistentDataPath, $"liquid_save_slot_{slotIndex}.json");
        }

        public static int GetMaxSaveSlots()
        {
            return MaxSaveSlots;
        }

        public static bool SaveExists(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                return false;
            }

            return File.Exists(GetSaveFilePath(slotIndex));
        }

        public static bool AnySaveExists()
        {
            for (int i = 0; i < MaxSaveSlots; i++)
            {
                if (SaveExists(i))
                {
                    return true;
                }
            }

            return false;
        }

        public static void SaveGame(GameSaveData data, int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                Debug.LogError($"Invalid save slot index: {slotIndex}");
                return;
            }

            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(GetSaveFilePath(slotIndex), json);
                Debug.Log($"Saved game to slot {slotIndex}: {GetSaveFilePath(slotIndex)}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save game in slot {slotIndex}: {ex}");
            }
        }

        public static GameSaveData LoadGame(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                Debug.LogError($"Invalid save slot index: {slotIndex}");
                return null;
            }

            try
            {
                string path = GetSaveFilePath(slotIndex);

                if (!File.Exists(path))
                {
                    return null;
                }

                string json = File.ReadAllText(path);
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load game from slot {slotIndex}: {ex}");
                return null;
            }
        }

        public static List<GameSaveData> LoadAllSaves()
        {
            List<GameSaveData> saves = new List<GameSaveData>();

            for (int i = 0; i < MaxSaveSlots; i++)
            {
                saves.Add(LoadGame(i));
            }

            return saves;
        }

        public static int GetFirstEmptySlotIndex()
        {
            for (int i = 0; i < MaxSaveSlots; i++)
            {
                if (!SaveExists(i))
                {
                    return i;
                }
            }

            return -1;
        }

        public static int GetMostRecentSaveSlotIndex()
        {
            int mostRecentSlot = -1;
            DateTime mostRecentTime = DateTime.MinValue;

            for (int i = 0; i < MaxSaveSlots; i++)
            {
                string path = GetSaveFilePath(i);

                if (!File.Exists(path))
                {
                    continue;
                }

                DateTime lastWriteTime = File.GetLastWriteTime(path);

                if (lastWriteTime > mostRecentTime)
                {
                    mostRecentTime = lastWriteTime;
                    mostRecentSlot = i;
                }
            }

            return mostRecentSlot;
        }

        public static void DeleteSave(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                Debug.LogError($"Invalid save slot index: {slotIndex}");
                return;
            }

            try
            {
                string path = GetSaveFilePath(slotIndex);

                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log($"Deleted save slot {slotIndex}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete save slot {slotIndex}: {ex}");
            }
        }

        public static void DeleteAllSaves()
        {
            for (int i = 0; i < MaxSaveSlots; i++)
            {
                DeleteSave(i);
            }
        }

        private static bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < MaxSaveSlots;
        }
    }
}