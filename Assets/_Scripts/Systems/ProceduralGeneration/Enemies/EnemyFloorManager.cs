using _Scripts.Core.Managers;
using Liquid.Audio;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration.Enemies
{
    /// <summary>
    /// Singleton that manages floor-wide enemy lifecycle during procedural generation.
    /// Freezes enemies when generation starts, unfreezes when complete.
    /// Provides diagnostic reporting for enemy integration health checks.
    /// </summary>
    public class EnemyFloorManager : MonoBehaviour
    {
        #region Singleton

        public static EnemyFloorManager Instance { get; private set; }

        #endregion

        #region Private Fields

        private bool _enemiesFrozen;

        #endregion

        #region Properties

        public bool EnemiesFrozen => _enemiesFrozen;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            var em = GameManager.Instance?.EventManager;
            if (em != null)
            {
                em.Subscribe("OnFloorGenerationStarted", OnFloorGenerationStarted);
                em.Subscribe("OnFloorGenerationComplete", OnFloorGenerationComplete);
            }
        }

        private void OnDisable()
        {
            var em = GameManager.Instance?.EventManager;
            if (em != null)
            {
                em.Unsubscribe("OnFloorGenerationStarted", OnFloorGenerationStarted);
                em.Unsubscribe("OnFloorGenerationComplete", OnFloorGenerationComplete);
            }
        }

        #endregion

        #region Event Handlers

        private void OnFloorGenerationStarted()
        {
            FreezeAllEnemies();
        }

        private void OnFloorGenerationComplete()
        {
            UnfreezeAllEnemies();
        }

        #endregion

        #region Freeze / Unfreeze

        public void FreezeAllEnemies()
        {
            _enemiesFrozen = true;

            EnemyBase[] enemies = FindObjectsOfType<EnemyBase>();
            foreach (EnemyBase enemy in enemies)
            {
                enemy.enabled = false;
            }

            Debug.Log($"[EnemyFloorManager] Froze {enemies.Length} enemies.");
        }

        public void UnfreezeAllEnemies()
        {
            _enemiesFrozen = false;

            EnemyBase[] enemies = FindObjectsOfType<EnemyBase>();
            foreach (EnemyBase enemy in enemies)
            {
                enemy.enabled = true;
            }

            Debug.Log($"[EnemyFloorManager] Unfroze {enemies.Length} enemies.");
        }

        #endregion

        #region Diagnostics

        public string GetDiagnosticReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Enemy Integration Diagnostics ===");

            // --- Core Systems ---
            sb.AppendLine("--- Core Systems ---");
            sb.AppendLine(Instance != null
                ? "<color=green>✓</color> EnemyFloorManager singleton"
                : "<color=red>✗</color> EnemyFloorManager singleton MISSING");

            sb.AppendLine(GameManager.Instance != null
                ? "<color=green>✓</color> GameManager"
                : "<color=red>✗</color> GameManager MISSING");

            sb.AppendLine(GameManager.Instance?.EventManager != null
                ? "<color=green>✓</color> EventManager"
                : "<color=red>✗</color> EventManager MISSING");

            var pathfinder = FindObjectOfType<GridPathfinder>();
            sb.AppendLine(pathfinder != null
                ? "<color=green>✓</color> GridPathfinder"
                : "<color=red>✗</color> GridPathfinder MISSING");

            sb.AppendLine(FloorStateManager.Instance != null
                ? "<color=green>✓</color> FloorStateManager"
                : "<color=red>✗</color> FloorStateManager MISSING");

            // --- Enemy Container ---
            sb.AppendLine("--- Enemy Container ---");
            GameObject container = GameObject.Find("--- ENEMIES ---");
            if (container != null)
            {
                sb.AppendLine("<color=green>✓</color> '--- ENEMIES ---' container exists");

                int childCount = container.transform.childCount;
                sb.AppendLine($"  Enemies in container: {childCount}");

                int alive = 0, frozen = 0;
                foreach (Transform child in container.transform)
                {
                    EnemyBase enemy = child.GetComponent<EnemyBase>();
                    if (enemy == null) continue;

                    if (!enemy.enabled) frozen++;
                    else alive++;
                }

                sb.AppendLine($"  Alive: {alive} | Dead: 0 | Frozen: {frozen}");

                EnemyBase[] allEnemies = FindObjectsOfType<EnemyBase>(true);
                sb.AppendLine($"  Total enemies in scene: {allEnemies.Length}");
            }
            else
            {
                sb.AppendLine("<color=red>✗</color> '--- ENEMIES ---' container NOT FOUND");
            }

            // --- Floor State ---
            sb.AppendLine("--- Floor State ---");
            sb.AppendLine($"  Enemies frozen: {(_enemiesFrozen ? "YES" : "NO")}");

            // --- Spawn Points ---
            sb.AppendLine("--- Spawn Points (Current Floor) ---");
            RoomEnemySpawner[] spawners = FindObjectsOfType<RoomEnemySpawner>();
            sb.AppendLine($"  Rooms with RoomEnemySpawner: {spawners.Length}");

            int totalStatic = 0, totalVent = 0;
            foreach (RoomEnemySpawner spawner in spawners)
            {
                EnemySpawnPoint[] points = spawner.GetComponentsInChildren<EnemySpawnPoint>();
                foreach (EnemySpawnPoint point in points)
                {
                    if (point.IsVent) totalVent++;
                    else totalStatic++;
                }
            }

            sb.AppendLine($"  Total static spawn points: {totalStatic}");
            sb.AppendLine($"  Total vent points: {totalVent}");

            // --- Player Target ---
            sb.AppendLine("--- Player Target ---");
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Player");

            if (player != null)
            {
                sb.AppendLine("<color=green>✓</color> Player found in scene");

                EnemyBase[] allEnemies = FindObjectsOfType<EnemyBase>(true);
                int withTarget = 0, withoutTarget = 0;

                foreach (EnemyBase enemy in allEnemies)
                {
                    var field = typeof(EnemyBase).GetField("playerTarget",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (field != null)
                    {
                        var target = field.GetValue(enemy) as Transform;
                        if (target != null) withTarget++;
                        else withoutTarget++;
                    }
                }

                sb.AppendLine($"  Enemies with player target: {withTarget}");

                if (withoutTarget > 0)
                    sb.AppendLine($"  <color=red>✗</color> Enemies MISSING player target: {withoutTarget}");
                else
                    sb.AppendLine($"  <color=green>✓</color> All enemies have player target");
            }
            else
            {
                sb.AppendLine("<color=red>✗</color> No Player found — enemies cannot detect/chase");
            }

            // --- Navigation ---
            sb.AppendLine("--- Navigation ---");
            if (pathfinder != null)
            {
                sb.AppendLine("<color=green>✓</color> GridPathfinder active");

                Vector3 testStart = pathfinder.transform.position;
                Vector3 testEnd = testStart + Vector3.forward * 2f;
                var testPath = pathfinder.FindPath(testStart, testEnd);
                sb.AppendLine(testPath != null && testPath.Count > 0
                    ? "<color=green>✓</color> Nav grid responsive (test pathfind)"
                    : "<color=yellow>⚠</color> Nav grid returned empty path (may need rebuild)");
            }
            else
            {
                sb.AppendLine("<color=red>✗</color> GridPathfinder not found");
            }

            // --- Detection Systems ---
            sb.AppendLine("--- Detection Systems ---");
            var noiseManager = FindObjectOfType<NoiseManager>();
            sb.AppendLine(noiseManager != null
                ? "<color=green>✓</color> NoiseManager active"
                : "<color=red>✗</color> NoiseManager MISSING — enemies cannot hear");

            // Count INoiseListener registrations
            GenericGoapEnemy[] goapEnemies = FindObjectsOfType<GenericGoapEnemy>(true);
            sb.AppendLine($"  GenericGoapEnemy instances: {goapEnemies.Length}");

            if (goapEnemies.Length > 0)
            {
                int inSight = 0, chasing = 0, wandering = 0, idle = 0, spotted = 0, noPath = 0;
                int hasWanderTarget = 0, wanderWaiting = 0;

                foreach (GenericGoapEnemy enemy in goapEnemies)
                {
                    if (enemy == null || !enemy.enabled) continue;

                    if (enemy.DebugPlayerInSight) inSight++;
                    if (enemy.CurrentGoal == EnemyGoalType.ChasePlayer) chasing++;
                    if (enemy.CurrentGoal == EnemyGoalType.WanderArea) wandering++;
                    if (enemy.CurrentGoal == EnemyGoalType.None) idle++;
                    if (enemy.DebugHasEverSpottedPlayer) spotted++;
                    if (!enemy.DebugHasValidPath) noPath++;
                    if (enemy.DebugHasWanderTarget) hasWanderTarget++;
                    if (enemy.DebugIsWanderWaiting) wanderWaiting++;
                }

                sb.AppendLine($"  Player in sight of: {inSight}");
                sb.AppendLine($"  Currently chasing: {chasing}");
                sb.AppendLine($"  Currently wandering: {wandering} (has target: {hasWanderTarget}, waiting: {wanderWaiting})");
                sb.AppendLine($"  Idle/None goal: {idle}");
                sb.AppendLine($"  Have ever spotted player: {spotted}");
                sb.AppendLine($"  <color={(noPath > goapEnemies.Length / 2 ? "yellow" : "white")}>No valid path: {noPath}/{goapEnemies.Length}</color>");

                // Sample first enemy for detailed debug
                GenericGoapEnemy sample = null;
                foreach (GenericGoapEnemy e in goapEnemies)
                {
                    if (e != null && e.enabled) { sample = e; break; }
                }

                if (sample != null)
                {
                    sb.AppendLine($"  --- Sample Enemy: {sample.name} ---");
                    sb.AppendLine($"    Goal: {sample.CurrentGoal}  State: {sample.CurrentState}");
                    sb.AppendLine($"    Reason: {sample.DebugLastDecisionReason}");
                    sb.AppendLine($"    Dist to player: {sample.DebugDistanceToPlayer:F1}m");
                    sb.AppendLine($"    In sight: {sample.DebugPlayerInSight}  Noise audible: {sample.DebugPlayerNoiseAudible}");
                    sb.AppendLine($"    Chase persist: spotted={sample.DebugHasEverSpottedPlayer} sinceSeen={sample.DebugTimeSinceLastSpotted:F1}s/{sample.DebugChasePersistDuration:F1}s");
                    sb.AppendLine($"    Wander: hasTarget={sample.DebugHasWanderTarget} waiting={sample.DebugIsWanderWaiting} timer={sample.DebugWanderWaitTimer:F1}");
                    sb.AppendLine($"    Path valid: {sample.DebugHasValidPath}  Stamina: {sample.CurrentStamina:F0}");
                }
            }

            // --- GridPathfinder Details ---
            sb.AppendLine("--- GridPathfinder Details ---");
            if (pathfinder != null)
            {
                sb.AppendLine("<color=green>✓</color> GridPathfinder active");

                // Test pathfinding from a real enemy position if possible
                EnemyBase[] allEnemiesNav = FindObjectsOfType<EnemyBase>(true);
                if (allEnemiesNav.Length > 0 && allEnemiesNav[0] != null)
                {
                    Vector3 enemyPos = allEnemiesNav[0].transform.position;
                    Vector3 testEnd = enemyPos + Vector3.forward * 3f;
                    var testPath = pathfinder.FindPath(enemyPos, testEnd);
                    sb.AppendLine(testPath != null && testPath.Count > 0
                        ? $"<color=green>✓</color> Path from enemy pos works ({testPath.Count} nodes)"
                        : $"<color=red>✗</color> Path from enemy pos FAILED — enemy at {enemyPos:F1}, grid may not cover this area");
                }
                else
                {
                    Vector3 testStart = pathfinder.transform.position;
                    Vector3 testEnd = testStart + Vector3.forward * 2f;
                    var testPath = pathfinder.FindPath(testStart, testEnd);
                    sb.AppendLine(testPath != null && testPath.Count > 0
                        ? "<color=green>✓</color> Nav grid responsive (generic test)"
                        : "<color=yellow>⚠</color> Nav grid returned empty path (may need rebuild)");
                }
            }
            else
            {
                sb.AppendLine("<color=red>✗</color> GridPathfinder not found");
            }

            // --- Potential Issues ---
            sb.AppendLine("--- Potential Issues ---");
            bool hasIssues = false;

            if (player == null)
            {
                sb.AppendLine("<color=red>✗</color> No Player found — enemies cannot detect/chase");
                hasIssues = true;
            }

            if (container == null)
            {
                sb.AppendLine("<color=red>✗</color> Enemy container missing — spawning will fail");
                hasIssues = true;
            }

            if (pathfinder == null)
            {
                sb.AppendLine("<color=red>✗</color> No pathfinder — enemies cannot navigate");
                hasIssues = true;
            }

            if (noiseManager == null)
            {
                sb.AppendLine("<color=yellow>⚠</color> No NoiseManager — enemies cannot hear player");
                hasIssues = true;
            }

            if (goapEnemies.Length > 0)
            {
                int noPathCount = 0;
                foreach (GenericGoapEnemy e in goapEnemies)
                {
                    if (e != null && e.enabled && !e.DebugHasValidPath) noPathCount++;
                }

                if (noPathCount == goapEnemies.Length)
                {
                    sb.AppendLine("<color=red>✗</color> ALL enemies have no valid path — GridPathfinder grid likely doesn't cover enemy spawn positions");
                    hasIssues = true;
                }
            }

            if (!hasIssues)
            {
                sb.AppendLine("<color=green>✓</color> No issues detected");
            }

            return sb.ToString();
        }

        #endregion
    }
}
