using System.Collections.Generic;
using System.Text;
using UnityEngine;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Player;
using Liquid.AI.GOAP;

/// <summary>
/// GOAP-driven Lurker enemy.
/// Reactive-light implementation (#3): lights damage the lurker and trigger freak-out -> retreat logic.
/// Works great with procedural generation by attaching LurkerLightHazard to spawned light prefabs.
/// </summary>
public class LurkerEnemy : EnemyBase
{
    #region World-state keys
    public const string WS_HAS_PLAYER = "hasPlayer";
    public const string WS_CAN_SEE_PLAYER = "canSeePlayer";
    public const string WS_PLAYER_IN_RANGE = "playerInRange";
    public const string WS_CLOSE_TO_PLAYER = "closeToPlayer";
    public const string WS_WITHIN_STEAL_RANGE = "withinStealRange";

    public const string WS_PLAYER_IS_AWARE = "playerIsAware";
    public const string WS_PLAYER_IS_QUIET = "playerIsQuiet";
    public const string WS_PLAYER_HAS_ITEMS = "playerHasItems";

    public const string WS_IN_BRIGHT_LIGHT = "inBrightLight";
    public const string WS_FREAKED_OUT = "freakedOut";

    // Achieved flags
    public const string WS_HIDING = "hiding";
    public const string WS_OBSERVED_PLAYER = "observedPlayer";
    public const string WS_HAS_STOLEN = "hasStolen";
    public const string WS_REPOSITIONED = "repositioned";
    #endregion

    #region Tunables
    [Header("Perception")]
    [SerializeField] private float sightDistance = 16f;

    [Header("Distances")]
    [SerializeField] private float playerInRangeDistance = 14f;
    [SerializeField] private float closeToPlayerDistance = 4.0f;
    [SerializeField] private float stealRangeDistance = 1.6f;

    [Header("Player Quiet Detection")]
    [Tooltip("If player's estimated speed is below this, they are considered quiet.")]
    [SerializeField] private float playerQuietSpeedThreshold = 1.25f;

    [Header("Awareness")]
    [Tooltip("How long the player stays 'aware' after the lurker steals (seconds).")]
    [SerializeField] private float stealAwarenessSeconds = 3.5f;

    [Header("Light Reaction")]
    [Range(0f, 1f)]
    [SerializeField] private float brightLightThreshold01 = 0.5f;

    [Tooltip("Seconds after last light hit to still consider ourselves 'in bright light'.")]
    [SerializeField] private float lightHitMemorySeconds = 0.15f;

    [Tooltip("How long the lurker freaks out when hit by light before retreating.")]
    [SerializeField] private float freakOutDuration = 0.9f;

    [Header("Retreat")]
    [Tooltip("How far to retreat away from the light source.")]
    [SerializeField] private float retreatDistance = 10f;

    [Tooltip("Random angle variation (degrees) when retreating, to avoid always running in a straight line.")]
    [SerializeField] private float retreatAngleJitterDeg = 25f;

    [Tooltip("If we fail to path to a retreat point, try up to this many alternatives.")]
    [SerializeField] private int retreatRetries = 5;

    [Header("Vanish Visual")]
    [Tooltip("Optional: while hiding, disable model root (visual vanish).")]
    [SerializeField] private bool disableModelWhileHiding = true;
    #endregion

    #region GOAP debug
    [Header("GOAP Debug")]
    [SerializeField] private string currentGoalName;
    [SerializeField] private string currentActionName;

    [TextArea(6, 20)]
    [SerializeField] private string worldStateDump;
    #endregion

    #region Runtime
    private readonly List<GoapAction> _availableActions = new List<GoapAction>();
    private Queue<GoapAction> _currentPlan;

    private Dictionary<string, object> _worldState;
    private Dictionary<string, object> _currentGoal;
    private Dictionary<string, object> _lastGoal;
    private GoapAction _lastAction;

    // Player tracking
    private float _playerAwareUntilTime;
    private Vector3 _lastPlayerPosition;
    private float _lastPlayerSpeed;

    // Light hit tracking (reactive hazard writes these)
    private float _lastLightHitTime;
    private float _lastLightIntensity01;
    private Vector3 _lastLightSourcePos;
    private float _pendingLightDamageThisFrame;

    // Achieved state runtime backing
    private bool _isHiding;
    private bool _isFreakedOut;
    private bool _hasStolen;
    private bool _observedPlayer;
    private bool _repositioned;

    // Retreat target cached for actions
    private Vector3 _cachedRetreatTarget;
    private bool _hasRetreatTarget;
    #endregion

    #region Public accessors used by actions
    public bool HasPlayer => playerTarget != null;
    public Vector3 PlayerPosition => playerTarget != null ? playerTarget.position : transform.position;

    public float FreakOutDuration => freakOutDuration;

    public bool InBrightLight
    {
        get
        {
            bool recentlyHit = (Time.time - _lastLightHitTime) <= lightHitMemorySeconds;
            return recentlyHit && _lastLightIntensity01 >= brightLightThreshold01;
        }
    }

    public bool HasRetreatTarget => _hasRetreatTarget;
    public Vector3 RetreatTarget => _cachedRetreatTarget;
    #endregion

    protected override void Awake()
    {
        base.Awake();

        // Auto-bind player if not set.
        if (playerTarget == null && PlayerManager.Instance != null && PlayerManager.Instance.CurrentPlayer != null)
        {
            playerTarget = PlayerManager.Instance.CurrentPlayer.transform;
        }

        _availableActions.AddRange(LurkerGoapActions.CreateAll());

        if (modelRoot != null && disableModelWhileHiding)
        {
            modelRoot.gameObject.SetActive(true);
        }
    }

    protected override void Tick()
    {
        // Apply light damage accumulated by hazards this frame.
        if (_pendingLightDamageThisFrame > 0f)
        {
            TakeDamage(_pendingLightDamageThisFrame);
            _pendingLightDamageThisFrame = 0f;
        }

        UpdatePlayerSpeedEstimate();

        // Recompute retreat target when we are hit by light (or still in memory window).
        UpdateRetreatTargetIfNeeded();

        _worldState = BuildWorldState();
        _currentGoal = ChooseGoal(_worldState);

        if (!GoalsEqual(_lastGoal, _currentGoal))
        {
            _currentPlan?.Clear();
            _lastAction = null;
            BuildNewPlan();
            _lastGoal = CloneGoal(_currentGoal);
        }

        UpdateDebugStrings(_worldState, _currentGoal);

        if (_currentPlan == null || _currentPlan.Count == 0)
        {
            _lastAction = null;
            BuildNewPlan();
        }

        if (_currentPlan == null || _currentPlan.Count == 0)
        {
            SetState(EnemyState.Idle);
            return;
        }

        GoapAction action = _currentPlan.Peek();
        currentActionName = action.GetType().Name;

        if (action != _lastAction)
        {
            if (!action.CheckProceduralPrecondition(gameObject))
            {
                ForceReplan();
                return;
            }

            _lastAction = action;
        }

        if (action.RequiresInRange())
        {
            if (action.Target == null)
            {
                _lastAction = null;
                BuildNewPlan();
                return;
            }

            action.InRange = IsInRange(action.Target);
        }

        bool success = action.Perform(gameObject);

        if (!success)
        {
            ForceReplan();
            return;
        }

        if (action.IsDone(gameObject))
        {
            _currentPlan.Dequeue();
            _lastAction = null;
        }

        if (GoalAchieved(_currentGoal, _worldState))
        {
            _currentPlan?.Clear();
            _lastAction = null;
        }
    }

    #region Called by light hazards
    /// <summary>
    /// Called by LurkerLightHazard while the lurker is inside a bright light trigger.
    /// This is the reactive-light pipeline (#3).
    /// </summary>
    public void NotifyHitByLight(Vector3 lightSourceWorldPos, float intensity01, float damagePerSecond)
    {
        if (intensity01 < brightLightThreshold01)
        {
            return;
        }

        _lastLightHitTime = Time.time;
        _lastLightIntensity01 = intensity01;
        _lastLightSourcePos = lightSourceWorldPos;

        // Accumulate damage for this frame (hazard calls us during OnTriggerStay).
        _pendingLightDamageThisFrame += Mathf.Max(0f, damagePerSecond) * Time.deltaTime;
    }
    #endregion

    #region World state + goal selection

    private Dictionary<string, object> BuildWorldState()
    {
        Dictionary<string, object> ws = new Dictionary<string, object>();

        bool hasPlayer = HasPlayer;
        ws[WS_HAS_PLAYER] = hasPlayer;

        bool canSeePlayer = false;
        if (hasPlayer)
        {
            canSeePlayer = HasLineOfSightTo(PlayerPosition, sightDistance);
        }
        ws[WS_CAN_SEE_PLAYER] = canSeePlayer;

        bool playerInRange = false;
        bool closeToPlayer = false;
        bool withinStealRange = false;

        if (hasPlayer)
        {
            float d = Vector3.Distance(transform.position, PlayerPosition);
            playerInRange = d <= playerInRangeDistance;
            closeToPlayer = d <= closeToPlayerDistance;
            withinStealRange = d <= stealRangeDistance;
        }

        ws[WS_PLAYER_IN_RANGE] = playerInRange;
        ws[WS_CLOSE_TO_PLAYER] = closeToPlayer;
        ws[WS_WITHIN_STEAL_RANGE] = withinStealRange;

        bool playerIsQuiet = _lastPlayerSpeed <= playerQuietSpeedThreshold;
        ws[WS_PLAYER_IS_QUIET] = playerIsQuiet;

        bool playerIsAware = Time.time <= _playerAwareUntilTime;
        ws[WS_PLAYER_IS_AWARE] = playerIsAware;

        ws[WS_PLAYER_HAS_ITEMS] = HasStealableItems();

        ws[WS_IN_BRIGHT_LIGHT] = InBrightLight;

        ws[WS_HIDING] = _isHiding;
        ws[WS_FREAKED_OUT] = _isFreakedOut;

        ws[WS_HAS_STOLEN] = _hasStolen;
        ws[WS_OBSERVED_PLAYER] = _observedPlayer;
        ws[WS_REPOSITIONED] = _repositioned;

        return ws;
    }

    private Dictionary<string, object> ChooseGoal(Dictionary<string, object> ws)
    {
        Dictionary<string, object> goal = new Dictionary<string, object>();

        bool inBrightLight = ws.ContainsKey(WS_IN_BRIGHT_LIGHT) && (bool)ws[WS_IN_BRIGHT_LIGHT];
        bool playerAware = ws.ContainsKey(WS_PLAYER_IS_AWARE) && (bool)ws[WS_PLAYER_IS_AWARE];

        bool hasPlayer = ws.ContainsKey(WS_HAS_PLAYER) && (bool)ws[WS_HAS_PLAYER];
        bool playerInRange = ws.ContainsKey(WS_PLAYER_IN_RANGE) && (bool)ws[WS_PLAYER_IN_RANGE];

        bool closeToPlayer = ws.ContainsKey(WS_CLOSE_TO_PLAYER) && (bool)ws[WS_CLOSE_TO_PLAYER];
        bool withinStealRange = ws.ContainsKey(WS_WITHIN_STEAL_RANGE) && (bool)ws[WS_WITHIN_STEAL_RANGE];
        bool playerHasItems = ws.ContainsKey(WS_PLAYER_HAS_ITEMS) && (bool)ws[WS_PLAYER_HAS_ITEMS];

        bool playerQuiet = ws.ContainsKey(WS_PLAYER_IS_QUIET) && (bool)ws[WS_PLAYER_IS_QUIET];
        bool canSeePlayer = ws.ContainsKey(WS_CAN_SEE_PLAYER) && (bool)ws[WS_CAN_SEE_PLAYER];

        // 0) Highest priority: if hit by light, escape it (freak out then retreat).
        if (inBrightLight && HasRetreatTarget)
        {
            goal[WS_IN_BRIGHT_LIGHT] = false;
            currentGoalName = "EscapeLight";
            return goal;
        }

        // 1) If spotted and close: steal first if possible, then hide.
        if (hasPlayer && playerInRange && playerAware && closeToPlayer && withinStealRange && playerHasItems && !inBrightLight)
        {
            goal[WS_HIDING] = true;
            currentGoalName = "StealThenVanish";
            return goal;
        }

        // 2) If spotted but no loot: vanish anyway.
        if (playerAware && !inBrightLight)
        {
            goal[WS_HIDING] = true;
            currentGoalName = "VanishWhenSpotted";
            return goal;
        }

        // 3) Opportunistic steal (quiet + unaware).
        if (hasPlayer && playerInRange && !playerAware && playerHasItems && playerQuiet && canSeePlayer && !inBrightLight)
        {
            goal[WS_HAS_STOLEN] = true;
            currentGoalName = "StealOpportunistically";
            return goal;
        }

        // 4) Observe (stare).
        if (hasPlayer && playerInRange && !playerAware && playerQuiet && canSeePlayer && !inBrightLight)
        {
            goal[WS_OBSERVED_PLAYER] = true;
            currentGoalName = "Observe";
            return goal;
        }

        // 5) Lurk / reposition.
        goal[WS_REPOSITIONED] = true;
        currentGoalName = "Lurk";
        return goal;
    }

    #endregion

    #region Retreat target selection (no ShadowSafeSpot needed)

    private void UpdateRetreatTargetIfNeeded()
    {
        if (!InBrightLight)
        {
            _hasRetreatTarget = false;
            return;
        }

        // Build a retreat direction away from the light source.
        Vector3 away = (transform.position - _lastLightSourcePos);
        away.y = 0f;

        if (away.sqrMagnitude < 0.01f)
        {
            away = -transform.forward;
            away.y = 0f;
        }

        away.Normalize();

        // Try a few jittered directions until we get a target inside the grid and pathable.
        _hasRetreatTarget = false;
        Vector3 bestTarget = transform.position;

        for (int i = 0; i < Mathf.Max(1, retreatRetries); i++)
        {
            float jitter = Random.Range(-retreatAngleJitterDeg, retreatAngleJitterDeg);
            Vector3 dir = Quaternion.Euler(0f, jitter, 0f) * away;

            Vector3 candidate = transform.position + dir * retreatDistance;

            // Ask the pathfinder indirectly via RequestPath (through TryGoTo usage).
            // We don't want to actually start moving here; we only validate.
            // So we call GridPathfinder directly (same layers as EnemyBase uses).
            if (GridPathfinder.Instance != null)
            {
                var path = GridPathfinder.Instance.FindPath(transform.position, candidate, walkableLayers);
                if (path != null && path.Count > 0)
                {
                    bestTarget = candidate;
                    _hasRetreatTarget = true;
                    break;
                }
            }
            else
            {
                // If no grid exists for some reason, still set a target.
                bestTarget = candidate;
                _hasRetreatTarget = true;
                break;
            }
        }

        _cachedRetreatTarget = bestTarget;
    }

    #endregion

    #region Planning (same as your pattern)

    private void BuildNewPlan()
    {
        for (int i = 0; i < _availableActions.Count; i++)
        {
            _availableActions[i].Reset();
        }

        Queue<GoapAction> plan;
        bool hasPlan = GoapPlanner.Plan(_availableActions, _worldState, _currentGoal, out plan);
        _currentPlan = hasPlan ? plan : null;

        if (_currentPlan == null)
        {
            currentGoalName = "No Plan";
        }
    }

    private void ForceReplan()
    {
        _currentPlan?.Clear();
        _lastAction = null;
        BuildNewPlan();
    }

    private bool GoalAchieved(Dictionary<string, object> goal, Dictionary<string, object> worldState)
    {
        if (goal == null || worldState == null) return false;

        foreach (var kvp in goal)
        {
            if (!worldState.ContainsKey(kvp.Key)) return false;
            if (!Equals(worldState[kvp.Key], kvp.Value)) return false;
        }

        return true;
    }

    private static Dictionary<string, object> CloneGoal(Dictionary<string, object> src)
    {
        if (src == null) return null;

        Dictionary<string, object> clone = new Dictionary<string, object>(src.Count);
        foreach (var kvp in src)
        {
            clone[kvp.Key] = kvp.Value;
        }
        return clone;
    }

    private static bool GoalsEqual(Dictionary<string, object> a, Dictionary<string, object> b)
    {
        if (a == b) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;

        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out object valB)) return false;
            if (!Equals(kvp.Value, valB)) return false;
        }

        return true;
    }

    #endregion

    #region Helpers used by actions

    public bool TryGoTo(Vector3 target)
    {
        if (!DebugHasValidPath || ShouldRecalculatePath(target))
        {
            bool success = RequestPath(target);
            if (!success)
            {
                return false;
            }
        }

        if (!DebugHasValidPath)
        {
            return false;
        }

        FollowPath();
        return true;
    }

    public bool IsInRange(GameObject targetObj)
    {
        if (targetObj == null) return false;
        return Vector3.Distance(transform.position, targetObj.transform.position) <= 1.25f;
    }

    public void SetHidden(bool hidden)
    {
        _isHiding = hidden;

        if (disableModelWhileHiding && modelRoot != null)
        {
            modelRoot.gameObject.SetActive(!hidden);
        }
    }

    public void SetFreakedOut(bool freakedOut)
    {
        _isFreakedOut = freakedOut;
    }

    public void MarkObserved()
    {
        _observedPlayer = true;
    }

    public void MarkRepositioned()
    {
        _repositioned = true;
    }

    public void MarkStolen()
    {
        _hasStolen = true;
        _playerAwareUntilTime = Time.time + stealAwarenessSeconds;
    }

    private bool HasStealableItems()
    {
        if (PlayerInventory.Instance == null) return false;

        for (int i = 0; i < PlayerInventory.Instance.SlotCount; i++)
        {
            var slot = PlayerInventory.Instance.GetSlot(i);
            if (slot != null && !slot.IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdatePlayerSpeedEstimate()
    {
        if (!HasPlayer)
        {
            _lastPlayerSpeed = 0f;
            return;
        }

        Vector3 pos = PlayerPosition;
        Vector3 delta = (pos - _lastPlayerPosition);
        float dt = Mathf.Max(0.0001f, Time.deltaTime);

        _lastPlayerSpeed = delta.magnitude / dt;
        _lastPlayerPosition = pos;
    }

    #endregion

    #region Debug dump
    private void UpdateDebugStrings(Dictionary<string, object> ws, Dictionary<string, object> goal)
    {
        StringBuilder sb = new StringBuilder(512);

        sb.AppendLine($"GoalLabel: {currentGoalName}");
        sb.AppendLine($"Action: {currentActionName}");
        sb.AppendLine("");

        sb.AppendLine("GoalKeyValues:");
        if (goal != null)
        {
            foreach (var kvp in goal)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        sb.AppendLine("WorldState:");
        if (ws != null)
        {
            foreach (var kvp in ws)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        // Light debug
        sb.AppendLine("");
        sb.AppendLine($"Light: inBright={InBrightLight} intensity={_lastLightIntensity01:0.00} lastHit={_lastLightHitTime:0.00}");
        sb.AppendLine($"Retreat: hasTarget={_hasRetreatTarget} target={_cachedRetreatTarget}");

        worldStateDump = sb.ToString();
    }
    #endregion
}