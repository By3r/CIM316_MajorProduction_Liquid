using System.Text;
using _Scripts.Systems.ProceduralGeneration.Enemies;
using UnityEngine;

namespace _Scripts.Systems.DebugConsole.Commands
{
    /// <summary>
    /// Debug commands for enemy system diagnostics and testing.
    /// </summary>
    public static class EnemyCommands
    {
        [DebugCommand("enemy diag", "Runs full enemy integration diagnostics.", "enemy diag")]
        public static string EnemyDiag(string[] args)
        {
            if (EnemyFloorManager.Instance == null)
                return "<color=red>EnemyFloorManager not found in scene. Add it to a GameObject (same one as FloorGenerator works).</color>";

            return EnemyFloorManager.Instance.GetDiagnosticReport();
        }

        [DebugCommand("enemy count", "Shows count of all enemies on the current floor.", "enemy count")]
        public static string EnemyCount(string[] args)
        {
            EnemyBase[] allEnemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);

            if (allEnemies.Length == 0)
                return "No enemies found in scene.";

            int alive = 0;
            int dead = 0;
            int frozen = 0;

            foreach (EnemyBase enemy in allEnemies)
            {
                if (enemy == null) continue;
                if (enemy.CurrentState == EnemyState.Dead) dead++;
                else alive++;
                if (!enemy.enabled) frozen++;
            }

            return $"Enemies: {allEnemies.Length} total | {alive} alive | {dead} dead | {frozen} frozen";
        }

        [DebugCommand("enemy list", "Lists all enemies with their state and position.", "enemy list")]
        public static string EnemyList(string[] args)
        {
            EnemyBase[] allEnemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);

            if (allEnemies.Length == 0)
                return "No enemies found in scene.";

            var sb = new StringBuilder();
            sb.AppendLine($"=== Enemies ({allEnemies.Length}) ===");

            foreach (EnemyBase enemy in allEnemies)
            {
                if (enemy == null) continue;

                string stateColor = enemy.CurrentState == EnemyState.Dead ? "red" :
                                   enemy.CurrentState == EnemyState.Chasing ? "orange" :
                                   enemy.CurrentState == EnemyState.Idle ? "white" : "yellow";

                string frozenTag = !enemy.enabled ? " <color=cyan>[FROZEN]</color>" : "";
                string targetTag = enemy.PlayerTarget != null ? "" : " <color=red>[NO TARGET]</color>";
                Vector3 pos = enemy.transform.position;

                sb.AppendLine($"  <color={stateColor}>{enemy.name}</color> — {enemy.CurrentState}{frozenTag}{targetTag} @ ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
            }

            return sb.ToString();
        }

        [DebugCommand("enemy freeze", "Freezes or unfreezes all enemies.", "enemy freeze [on|off]")]
        public static string EnemyFreeze(string[] args)
        {
            if (EnemyFloorManager.Instance == null)
                return "<color=red>EnemyFloorManager not found in scene.</color>";

            if (args.Length == 0)
            {
                return $"Enemies frozen: {(EnemyFloorManager.Instance.EnemiesFrozen ? "YES" : "NO")}. Use 'enemy freeze on' or 'enemy freeze off'.";
            }

            string toggle = args[0].ToLower();
            if (toggle == "on" || toggle == "true")
            {
                EnemyFloorManager.Instance.FreezeAllEnemies();
                return "<color=cyan>All enemies frozen.</color>";
            }
            else if (toggle == "off" || toggle == "false")
            {
                EnemyFloorManager.Instance.UnfreezeAllEnemies();
                return "<color=green>All enemies unfrozen.</color>";
            }

            return "Usage: enemy freeze [on|off]";
        }

        [DebugCommand("enemy kill", "Kills all enemies on the current floor.", "enemy kill")]
        public static string EnemyKillAll(string[] args)
        {
            EnemyBase[] allEnemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);

            if (allEnemies.Length == 0)
                return "No enemies to kill.";

            int killed = 0;
            foreach (EnemyBase enemy in allEnemies)
            {
                if (enemy != null && enemy.CurrentState != EnemyState.Dead)
                {
                    enemy.TakeDamage(9999f);
                    killed++;
                }
            }

            return $"<color=green>Killed {killed} enemies.</color>";
        }
    }
}
