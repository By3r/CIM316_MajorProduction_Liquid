using System.Collections.Generic;
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

        [DebugCommand("enemy list", "Lists all enemies with their state, goal, and position.", "enemy list")]
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
                                   enemy.CurrentState == EnemyState.Roaming ? "green" :
                                   enemy.CurrentState == EnemyState.Idle ? "white" : "yellow";

                string frozenTag = !enemy.enabled ? " <color=cyan>[FROZEN]</color>" : "";
                string targetTag = enemy.PlayerTarget != null ? "" : " <color=red>[NO TARGET]</color>";
                Vector3 pos = enemy.transform.position;

                // Show GOAP goal if it's a GenericGoapEnemy
                string goalInfo = "";
                GenericGoapEnemy goap = enemy as GenericGoapEnemy;
                if (goap != null)
                {
                    goalInfo = $" [{goap.CurrentGoal}]";
                    if (goap.DebugPlayerInSight)
                        goalInfo += " <color=red>[SEES PLAYER]</color>";
                }

                sb.AppendLine($"  <color={stateColor}>{enemy.name}</color> — {enemy.CurrentState}{goalInfo}{frozenTag}{targetTag} @ ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
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

        [DebugCommand("enemy pathtest", "Tests pathfinding from every enemy to the player. Shows grid coverage and path results.", "enemy pathtest")]
        public static string EnemyPathTest(string[] args)
        {
            var sb = new StringBuilder();

            // Grid info
            GridPathfinder pf = GridPathfinder.Instance;
            if (pf == null)
            {
                return "<color=red>GridPathfinder not found in scene.</color>";
            }

            sb.AppendLine("=== Pathfinding Test ===");
            sb.AppendLine(pf.GridStatsString);

            // Find player
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Player");

            if (player == null)
            {
                sb.AppendLine("<color=red>No player found — cannot test paths.</color>");
                return sb.ToString();
            }

            Vector3 playerPos = player.transform.position;
            sb.AppendLine($"Player at: ({playerPos.x:F1}, {playerPos.y:F1}, {playerPos.z:F1}) — inside grid: {pf.IsInsideGrid(playerPos)}");

            // Test each enemy
            EnemyBase[] allEnemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);

            if (allEnemies.Length == 0)
            {
                sb.AppendLine("No enemies in scene.");
                return sb.ToString();
            }

            int passed = 0, failed = 0, outsideGrid = 0;

            foreach (EnemyBase enemy in allEnemies)
            {
                if (enemy == null) continue;
                Vector3 ePos = enemy.transform.position;

                bool insideGrid = pf.IsInsideGrid(ePos);
                if (!insideGrid)
                {
                    outsideGrid++;
                    sb.AppendLine($"  <color=red>✗ OUTSIDE GRID</color> {enemy.name} @ ({ePos.x:F1}, {ePos.y:F1}, {ePos.z:F1})");
                    continue;
                }

                List<Vector3> path = pf.FindPath(ePos, playerPos);
                if (path != null && path.Count > 0)
                {
                    passed++;
                    sb.AppendLine($"  <color=green>✓</color> {enemy.name} → player ({path.Count} nodes, ~{path.Count * 1f:F0}m)");
                }
                else
                {
                    failed++;
                    sb.AppendLine($"  <color=red>✗ NO PATH</color> {enemy.name} @ ({ePos.x:F1}, {ePos.y:F1}, {ePos.z:F1})");
                }
            }

            sb.AppendLine($"--- Results: {passed} passed, {failed} failed, {outsideGrid} outside grid ---");

            if (outsideGrid > 0)
                sb.AppendLine("<color=yellow>⚠ Enemies outside grid cannot pathfind. Grid may need to be larger or rebuilt.</color>");
            if (failed > 0 && outsideGrid == 0)
                sb.AppendLine("<color=yellow>⚠ Enemies inside grid but no path found. Check ground validation or obstacle layers.</color>");
            if (passed == allEnemies.Length)
                sb.AppendLine("<color=green>✓ All enemies can reach the player!</color>");

            return sb.ToString();
        }
    }
}
