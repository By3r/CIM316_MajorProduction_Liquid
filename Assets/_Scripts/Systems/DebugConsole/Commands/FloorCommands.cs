using System.Text;
using _Scripts.Core.Managers;

namespace _Scripts.Systems.DebugConsole.Commands
{
    /// <summary>
    /// Debug commands for floor navigation and inspection.
    /// </summary>
    public static class FloorCommands
    {
        [DebugCommand("floor goto", "Transitions to the specified floor number.", "floor goto <number>")]
        public static string FloorGoto(string[] args)
        {
            if (args.Length == 0)
                return "Usage: floor goto <number>";

            if (!int.TryParse(args[0], out int targetFloor))
                return $"<color=red>Invalid floor number: '{args[0]}'.</color>";

            if (targetFloor < 1)
                return "<color=red>Floor number must be 1 or greater.</color>";

            if (GameManager.Instance == null)
                return "<color=red>GameManager not found.</color>";

            if (GameManager.Instance.EventManager == null)
                return "<color=red>EventManager not found.</color>";

            // Close the console before transitioning
            if (DebugConsole.Instance != null && DebugConsole.Instance.IsOpen)
            {
                DebugConsole.Instance.Close();
            }

            // Use the same event the Elevator uses
            GameManager.Instance.EventManager.Publish("OnFloorTransitionRequested", targetFloor);

            return $"<color=green>Transitioning to floor {targetFloor}...</color>";
        }

        [DebugCommand("floor info", "Displays current floor state and session stats.", "floor info")]
        public static string FloorInfo(string[] args)
        {
            if (FloorStateManager.Instance == null)
                return "<color=red>FloorStateManager not found.</color>";

            if (!FloorStateManager.Instance.IsInitialized)
                return "<color=red>FloorStateManager is not initialized.</color>";

            var sb = new StringBuilder();
            sb.AppendLine("=== Floor Info ===");
            sb.AppendLine(FloorStateManager.Instance.GetSessionStats());

            return sb.ToString();
        }
    }
}
