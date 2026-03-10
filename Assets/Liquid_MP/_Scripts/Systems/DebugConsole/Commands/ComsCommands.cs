using System.Text;
using _Scripts.Systems.DebugConsole;
using _Scripts.Systems.Player;
using Liquid_MP._Scripts.Systems.Coms;
using UnityEngine;

namespace Liquid_MP._Scripts.Systems.DebugConsole.Commands
{
    /// <summary>
    /// Debug console commands for the COMS device and call system.
    /// </summary>
    public static class ComsCommands
    {
        [DebugCommand("coms status", "Shows COMS device and call state.", "coms status")]
        public static string ComsStatus(string[] args)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== COMS Status ===");

            // Device state
            var controller = Object.FindFirstObjectByType<ComsDeviceController>();
            if (controller == null)
            {
                sb.AppendLine("ComsDeviceController: <color=red>not found</color>");
            }
            else
            {
                sb.AppendLine($"  Equipped: {(controller.IsEquipped ? "<color=green>yes</color>" : "no")}");
                sb.AppendLine($"  Active: {(controller.IsActive ? "<color=green>yes</color>" : "no")}");
            }

            // Call state
            var mgr = ComsCallManager.Instance;
            if (mgr == null)
            {
                sb.AppendLine("ComsCallManager: <color=red>not found in scene</color>");
            }
            else
            {
                sb.AppendLine($"  Call state: <color=cyan>{mgr.CurrentState}</color>");

                if (mgr.CurrentCall != null)
                {
                    sb.AppendLine($"  Caller: {mgr.CurrentCall.callerName}");
                    sb.AppendLine($"  Lines: {mgr.CurrentCall.lines?.Length ?? 0}");
                    sb.AppendLine($"  Current line: {mgr.CurrentLineIndex}");
                }

                // Registry
                if (mgr.CallRegistry != null && mgr.CallRegistry.Length > 0)
                {
                    sb.AppendLine($"  Registry: {mgr.CallRegistry.Length} call(s) available");
                }
                else
                {
                    sb.AppendLine(
                        "  Registry: <color=yellow>empty (assign CallDataSO assets to ComsCallManager)</color>");
                }
            }

            return sb.ToString();
        }

        [DebugCommand("coms call", "Triggers an incoming call. Use 'coms call list' to see available calls.",
            "coms call [name|index|list]")]
        public static string ComsCall(string[] args)
        {
            var mgr = ComsCallManager.Instance;
            if (mgr == null)
                return "<color=red>ComsCallManager not found in scene.</color>";

            if (mgr.CallRegistry == null || mgr.CallRegistry.Length == 0)
                return
                    "<color=yellow>No calls in registry. Assign CallDataSO assets to ComsCallManager._callRegistry.</color>";

            // "coms call list" — show available calls
            if (args.Length == 0 || (args.Length == 1 && args[0].ToLower() == "list"))
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Available Calls ===");
                for (int i = 0; i < mgr.CallRegistry.Length; i++)
                {
                    var call = mgr.CallRegistry[i];
                    if (call == null) continue;
                    sb.AppendLine($"  [{i}] {call.callerName} — {call.lines?.Length ?? 0} line(s) — \"{call.name}\"");
                }

                sb.AppendLine("\nUsage: coms call <index> or coms call <name>");
                return sb.ToString();
            }

            // Try to find call by index or name
            string query = string.Join(" ", args).ToLower();
            CallDataSO target = null;

            // Try index first
            if (int.TryParse(args[0], out int index))
            {
                if (index >= 0 && index < mgr.CallRegistry.Length)
                    target = mgr.CallRegistry[index];
                else
                    return $"<color=red>Index {index} out of range (0-{mgr.CallRegistry.Length - 1}).</color>";
            }
            else
            {
                // Search by caller name (partial match)
                foreach (var call in mgr.CallRegistry)
                {
                    if (call == null) continue;
                    if (call.callerName.ToLower().Contains(query) ||
                        call.name.ToLower().Contains(query))
                    {
                        target = call;
                        break;
                    }
                }
            }

            if (target == null)
                return $"<color=red>No call found matching '{query}'.</color>";

            bool success = mgr.TriggerCall(target);
            if (success)
                return $"<color=green>Incoming call from '{target.callerName}' triggered. " +
                       $"Pull out COMS (press 3) to answer.</color>";
            else
                return $"<color=yellow>Cannot trigger call — already in state {mgr.CurrentState}.</color>";
        }

        [DebugCommand("coms hangup", "Ends the current COMS call.", "coms hangup")]
        public static string ComsHangup(string[] args)
        {
            var mgr = ComsCallManager.Instance;
            if (mgr == null)
                return "<color=red>ComsCallManager not found in scene.</color>";

            if (mgr.CurrentState == ComsCallState.Idle)
                return "<color=yellow>No active call to hang up.</color>";

            string caller = mgr.CurrentCall?.callerName ?? "unknown";
            mgr.EndCall();
            return $"<color=green>Call from '{caller}' ended.</color>";
        }

        [DebugCommand("coms answer", "Answers the current ringing call.", "coms answer")]
        public static string ComsAnswer(string[] args)
        {
            var mgr = ComsCallManager.Instance;
            if (mgr == null)
                return "<color=red>ComsCallManager not found in scene.</color>";

            if (mgr.CurrentState != ComsCallState.Ringing)
                return $"<color=yellow>No ringing call to answer (state: {mgr.CurrentState}).</color>";

            mgr.AnswerCall();
            return $"<color=green>Call from '{mgr.CurrentCall?.callerName}' answered.</color>";
        }
    }
}