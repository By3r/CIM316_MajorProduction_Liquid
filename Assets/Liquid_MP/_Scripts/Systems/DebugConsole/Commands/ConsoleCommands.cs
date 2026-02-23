using System.Linq;
using System.Text;
using UnityEngine;

namespace _Scripts.Systems.DebugConsole.Commands
{
    /// <summary>
    /// Built-in console commands: help, clear, copy.
    /// </summary>
    public static class ConsoleCommands
    {
        [DebugCommand("help", "Lists all available commands, or shows details for a specific command.", "help [command]")]
        public static string Help(string[] args)
        {
            var commands = DebugCommandRegistry.GetAllCommands();

            // Help for a specific command
            if (args.Length > 0)
            {
                string query = string.Join(" ", args).ToLower();

                if (commands.TryGetValue(query, out var entry))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"{entry.Name} — {entry.Description}");
                    sb.AppendLine($"  Usage: {entry.Usage}");
                    return sb.ToString();
                }

                return $"<color=red>Unknown command: '{query}'. Type 'help' for all commands.</color>";
            }

            // List all commands
            var sorted = commands.Values.OrderBy(c => c.Name).ToList();
            var output = new StringBuilder();
            output.AppendLine("=== Available Commands ===");

            foreach (var cmd in sorted)
            {
                output.AppendLine($"  {cmd.Name} — {cmd.Description}");
            }

            output.AppendLine($"\nType 'help <command>' for detailed usage.");
            return output.ToString();
        }

        [DebugCommand("clear", "Clears all console output.")]
        public static string Clear(string[] args)
        {
            if (DebugConsole.Instance != null)
            {
                DebugConsole.Instance.ClearOutput();
            }

            return null; // No output after clearing
        }

        [DebugCommand("copy", "Copies all console output to the system clipboard.", "copy")]
        public static string Copy(string[] args)
        {
            if (DebugConsole.Instance == null)
                return "<color=red>Console not available.</color>";

            string log = DebugConsole.Instance.GetFullLog();
            if (string.IsNullOrEmpty(log))
                return "Nothing to copy.";

            GUIUtility.systemCopyBuffer = log;
            return "<color=green>Console output copied to clipboard.</color>";
        }
    }
}
