using _Scripts.Core.Managers;
using UnityEngine.SceneManagement;

namespace _Scripts.Systems.DebugConsole.Commands
{
    /// <summary>
    /// Debug commands for world seed management.
    /// </summary>
    public static class SeedCommands
    {
        [DebugCommand("seed get", "Displays the world seed and current floor seed.", "seed get")]
        public static string SeedGet(string[] args)
        {
            if (FloorStateManager.Instance == null)
                return "<color=red>FloorStateManager not found.</color>";

            if (!FloorStateManager.Instance.IsInitialized)
                return "<color=red>FloorStateManager is not initialized.</color>";

            int worldSeed = FloorStateManager.Instance.WorldSeed;
            int floor = FloorStateManager.Instance.CurrentFloorNumber;
            int floorSeed = FloorStateManager.Instance.GetFloorSeed(floor);

            return $"World Seed: {worldSeed}\nFloor {floor} Seed: {floorSeed}";
        }

        [DebugCommand("seed set", "Sets the world seed and reloads the current scene.", "seed set <number>")]
        public static string SeedSet(string[] args)
        {
            if (args.Length == 0)
                return "Usage: seed set <number>";

            if (!int.TryParse(args[0], out int seed))
                return $"<color=red>Invalid seed: '{args[0]}'. Must be an integer.</color>";

            if (FloorStateManager.Instance == null)
                return "<color=red>FloorStateManager not found.</color>";

            FloorStateManager.Instance.SetSpecificSeed(seed);

            // Reload the current scene to apply the new seed
            string currentScene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(currentScene);

            return $"<color=green>Seed set to {seed}. Reloading scene...</color>";
        }
    }
}
