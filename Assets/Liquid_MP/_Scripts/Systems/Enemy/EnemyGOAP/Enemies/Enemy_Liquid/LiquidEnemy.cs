using Liquid.AI.GOAP;
using Liquid.Audio;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class LiquidEnemy : EnemyBase, INoiseListener, IEnemyDebugTarget
{
    #region World-state keys
    public const string WS_HAS_PLAYER = "hasPlayer";
    public const string WS_CAN_REACH_PLAYER = "canReachPlayer";
    public const string WS_PLAYER_INTERESTING = "playerInteresting";
    public const string WS_NEAR_PLAYER = "nearPlayer";
    public const string WS_CAN_HOLD = "canHold";
    public const string WS_PLAYER_HELD = "playerHeld";
    public const string WS_IS_MERGED = "isMerged";
    public const string WS_CAN_SWALLOW = "canSwallow";
    public const string WS_PLAYER_SWALLOWED = "playerSwallowed";

    public const string WS_HAS_POND = "hasPond";
    public const string WS_IN_POND = "inPond";
    public const string WS_RELAXED = "relaxed";
    public const string WS_EMERGED = "emerged";

    public const string WS_CAN_DUPLICATE = "canDuplicate";
    public const string WS_DUPLICATED = "duplicated";

    public const string WS_HAS_MERGE_REQUEST = "hasMergeRequest";
    public const string WS_CAN_REQUEST_MERGE = "canRequestMerge";
    public const string WS_MERGE_REQUESTED = "mergeRequested";

    public const string WS_IS_BUSY = "isBusy";
    public const string WS_ACCEPTED_MERGE_REQUEST = "acceptedMergeRequest";
    public const string WS_HELPED_MERGE = "helpedMerge";

    public const string WS_HAS_LAST_SEEN = "hasLastSeen";
    public const string WS_AT_LAST_SEEN = "atLastSeen";
    public const string WS_PATROLLED = "patrolled";

    public const string WS_HEARD_NOISE = "heardNoise";
    public const string WS_NOISE_AWARENESS = "noiseAwareness"; 
    #endregion

    #region Pond / environment
    [Header("Pond Settings")]
    [Tooltip("Where the centre of this Liquid’s pond is in world space.")] //TODO: Separate for multiple ponds, Multiple Liquid homes
    [SerializeField] private Transform pondCenter;

    [Tooltip("Radius around pondCenter that counts as 'in the pond'.")]
    [SerializeField] private float pondRadius = 4f;

    [Tooltip("How long Liquid relaxes in the pond before replanning.")]
    [SerializeField] private float relaxDuration = 6f;

    [Header("Pond Patrol")]
    [Tooltip("How far from pond center the Liquid may wander while 'patrolling' (only used while in pond).")]
    [SerializeField] private float pondPatrolRadius = 2.5f;

    [Tooltip("How close to a patrol point counts as reached.")]
    [SerializeField] private float pondPatrolArriveRadius = 0.6f;

    [SerializeField] private Vector2 pondPatrolDurationRange = new Vector2(2.0f, 4.5f);
    #endregion

    #region Player Awareness
    [Header("Awareness")]
    [Tooltip("Max distance for sight checks.")]
    [SerializeField] private float sightDistance = 18f;

    [Tooltip("If player is within this distance from pondCenter, they are considered 'near pond'.")]
    [SerializeField] private float chaseDistance = 15f;

    [Header("Interest Memory")]
    [Tooltip("How long we keep 'playerInteresting = true' after we last saw them near pond.")]
    [SerializeField] private float playerInterestingMemorySeconds = 1.25f;

    [Header("Investigate")]
    [Tooltip("How long the Liquid remembers the player's last seen position.")]
    [SerializeField] private float lastSeenMemorySeconds = 4.0f;

    [Tooltip("Distance to last seen that counts as 'arrived'.")]
    [SerializeField] private float lastSeenArriveRadius = 1.25f;

    [Header("Noise Awareness")]
    [Tooltip("If noise awareness is below this, we may ignore it.")]
    [Range(0f, 1f)]
    [SerializeField] private float noiseAwarenessThreshold = 0.22f;

    [Tooltip("How long we treat recent noise as 'interesting' once awareness passes threshold.")]
    [SerializeField] private float noiseInterestMemorySeconds = 2.0f;

    [Tooltip("How fast noise awareness decays back to 0.")]
    [SerializeField] private float noiseAwarenessDecayPerSecond = 0.18f;

    [Tooltip("A multiplier to make noises matter more/less for Liquid.")]
    [SerializeField] private float noiseAwarenessGainMultiplier = 1.0f;
    #endregion

    #region Duplication / merge
    [Header("Duplication")]
    [Tooltip("Prefab for a duplicated Liquid.")] // If not assigned, the original prefab will be used!
    [SerializeField] private LiquidEnemy liquidPrefab;

    [Header("Merge")]
    [Tooltip("Distance at which responder can merge into requester.")]
    [SerializeField] private float mergeDistance = 2f;

    [Header("Hold / Swallow Attack")]
    [Tooltip("Distance to the player required to start holding them.")]
    [SerializeField] private float holdDistance = 1.5f;

    [Tooltip("How long Liquid pins the player before replanning.")]
    [SerializeField] private float holdDuration = 2.5f;

    [Tooltip("Distance at which swallow is allowed to succeed.")]
    [SerializeField] private float swallowDistance = 1.1f;

    [Header("Chase Feel")]
    [Tooltip("Adds a small offset while chasing so multiple Liquids don't stack perfectly on player position.")]
    [SerializeField] private float chaseStrafeOffset = 1.25f;

    [Header("Reachability")]
    [Tooltip("If pathfinding to the player fails, we treat them as unreachable for this long to avoid chase spam.")]
    [SerializeField] private float unreachablePlayerCooldownSeconds = 3f;

    private float _playerUnreachableUntilTime;
    #endregion

    #region GOAP Debug
    [Header("GOAP Debug")]
    [SerializeField] private string currentGoalName;
    [SerializeField] private string currentActionName;
    [TextArea(6, 20)]
    [SerializeField] private string worldStateDump;

    public string DebugCurrentGoalName => currentGoalName;
    public string DebugCurrentActionName => currentActionName;
    public Dictionary<string, object> DebugWorldState => _worldState;
    #endregion

    #region Runtime
    private readonly List<GoapAction> _availableActions = new List<GoapAction>();
    private Queue<GoapAction> _currentPlan;

    private Dictionary<string, object> _worldState;
    private Dictionary<string, object> _currentGoal;

    private GoapAction _lastAction;

    private Dictionary<string, object> _lastGoal;

    private bool _requestedMergeHelp;
    private bool _mergedOnce;
    private int _mergedStage = 1;

    private float _playerInterestingUntilTime;
    private Vector3 _lastSeenPlayerPosition;
    private float _lastSeenTime = -999f;

    private float _noiseAwareness01;
    private float _noiseInterestingUntilTime;
    private NoiseEvent? _lastNoiseEvent;

    private bool _lastRequestedPathWasPlayer;
    #endregion

    #region Public actions for outer access.
    public Transform PondCenter => pondCenter;
    public float PondRadius => pondRadius;
    public float RelaxDuration => relaxDuration;
    public LiquidEnemy LiquidPrefab => liquidPrefab;
    public float MergeDistance => mergeDistance;
    public float HoldDuration => holdDuration;

    public float PondPatrolArriveRadius => pondPatrolArriveRadius;
    public Vector2 PondPatrolDurationRange => pondPatrolDurationRange;

    public bool HasPlayer => playerTarget != null;
    public Vector3 PlayerPosition => playerTarget != null ? playerTarget.position : transform.position;

    public bool InPond
    {
        get
        {
            if (pondCenter == null)
            {
                return false;
            }

            return Vector3.Distance(transform.position, pondCenter.position) <= pondRadius;
        }
    }

    public bool IsMerged => _mergedStage >= 2;

    public bool CanHoldPlayer
    {
        get
        {
            if (!HasPlayer)
            {
                return false;
            }

            return Vector3.Distance(transform.position, playerTarget.position) <= holdDistance;
        }
    }

    public bool CanSwallowPlayer
    {
        get
        {
            if (!HasPlayer)
            {
                return false;
            }

            return Vector3.Distance(transform.position, playerTarget.position) <= swallowDistance;
        }
    }

    public bool HasRecentLastSeen => (Time.time - _lastSeenTime) <= lastSeenMemorySeconds;
    public Vector3 LastSeenPlayerPosition => _lastSeenPlayerPosition;

    #endregion

    protected override void Awake()
    {
        base.Awake();

        _availableActions.AddRange(LiquidGoapActions.CreateAll());

        if (LiquidWorldState.Instance != null)
        {
            LiquidWorldState.Instance.Register(this);
        }

        ApplyMergedStageVisual();
    }

    private void OnEnable()
    {
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.RegisterListener(this);
        }

        if (EnemyDebugFocusManager.Instance != null)
        {
            EnemyDebugFocusManager.Instance.Register(this);
        }
    }

    private void OnDisable()
    {
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.UnregisterListener(this);
        }

        if (LiquidWorldState.Instance != null)
        {
            LiquidWorldState.Instance.Unregister(this);
        }

        if (EnemyDebugFocusManager.Instance != null)
        {
            EnemyDebugFocusManager.Instance.Unregister(this);
        }
    }

    public void OnNoiseHeard(NoiseEvent noiseEvent)
    {
        _lastNoiseEvent = noiseEvent;

        float dist = Vector3.Distance(transform.position, noiseEvent.worldPosition);
        float t = 1f;
        if (noiseEvent.finalRadius > 0.01f)
        {
            t = Mathf.Clamp01(1f - (dist / noiseEvent.finalRadius));
        }

        float levelStrength = GetNoiseLevelStrength(noiseEvent.level);
        float categoryStrength = GetNoiseCategoryStrength(noiseEvent.category);

        float ambient = 0f;
        if (noiseEvent.roomContext != null && noiseEvent.roomContext.ActiveProfile != null)
        {
            ambient = Mathf.Clamp01(noiseEvent.roomContext.ActiveProfile.AmbientNoiseLevel);
        }

        float gain = levelStrength * categoryStrength * t * (1f - ambient) * noiseAwarenessGainMultiplier;
        _noiseAwareness01 = Mathf.Clamp01(_noiseAwareness01 + gain);

        if (_noiseAwareness01 >= noiseAwarenessThreshold)
        {
            _noiseInterestingUntilTime = Time.time + noiseInterestMemorySeconds;

            _lastSeenPlayerPosition = noiseEvent.worldPosition;
            _lastSeenTime = Time.time;

            _playerInterestingUntilTime = Mathf.Max(_playerInterestingUntilTime, Time.time + playerInterestingMemorySeconds);
        }
    }

    private float GetNoiseLevelStrength(NoiseLevel level)
    {
        switch (level)
        {
            case NoiseLevel.Low: return 0.18f;
            case NoiseLevel.Medium: return 0.35f;
            case NoiseLevel.High: return 0.65f;
            case NoiseLevel.Maximum: return 1.00f;
            default: return 0.2f;
        }
    }

    private float GetNoiseCategoryStrength(NoiseCategory category)
    {
        switch (category)
        {
            case NoiseCategory.Footsteps: return 0.85f;
            case NoiseCategory.Sprint: return 1.00f;
            case NoiseCategory.Jump: return 0.95f;
            case NoiseCategory.ObjectImpact: return 1.10f;
            case NoiseCategory.Gunshot: return 1.35f;
            default: return 1.0f;
        }
    }

    public bool CanReachPlayer => Time.time >= _playerUnreachableUntilTime;

    public void MarkPlayerUnreachable()
    {
        _playerUnreachableUntilTime = Time.time + unreachablePlayerCooldownSeconds;
    }

    public bool TryGoToPlayer()
    {
        _lastRequestedPathWasPlayer = true;

        bool ok = TryGoTo(PlayerPosition);
        if (!ok)
        {
            MarkPlayerUnreachable();
        }
        return ok;
    }

    public bool TryGoToPlayerSmart()
    {
        if (!HasPlayer)
        {
            return false;
        }

        _lastRequestedPathWasPlayer = true;

        Vector3 target = GetSmartChaseTarget();
        bool ok = TryGoTo(target);
        if (!ok)
        {
            MarkPlayerUnreachable();
        }

        return ok;
    }

    private Vector3 GetSmartChaseTarget()
    {
        if (!HasPlayer)
        {
            return transform.position;
        }

        Vector3 toPlayer = PlayerPosition - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < 0.01f)
        {
            return PlayerPosition;
        }

        Vector3 dir = toPlayer.normalized;
        Vector3 perp = new Vector3(-dir.z, 0f, dir.x);

        float side = (GetInstanceID() & 1) == 0 ? 1f : -1f;
        Vector3 offset = perp * chaseStrafeOffset * side;

        if (toPlayer.magnitude <= holdDistance * 1.1f)
        {
            offset = Vector3.zero;
        }

        return PlayerPosition + offset;
    }

    protected override void Tick()
    {
        _noiseAwareness01 = Mathf.Max(0f, _noiseAwareness01 - noiseAwarenessDecayPerSecond * Time.deltaTime);

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
            if (pondCenter != null)
            {
                TryGoTo(pondCenter.position);
            }

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

        _lastRequestedPathWasPlayer = false;
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

    #region Planning
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
        if (goal == null || worldState == null)
        {
            return false;
        }

        foreach (var kvp in goal)
        {
            if (!worldState.ContainsKey(kvp.Key))
            {
                return false;
            }

            object currentValue = worldState[kvp.Key];
            if (!Equals(currentValue, kvp.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, object> CloneGoal(Dictionary<string, object> src)
    {
        if (src == null)
        {
            return null;
        }

        Dictionary<string, object> clone = new Dictionary<string, object>(src.Count);
        foreach (var kvp in src)
        {
            clone[kvp.Key] = kvp.Value;
        }
        return clone;
    }

    private static bool GoalsEqual(Dictionary<string, object> a, Dictionary<string, object> b)
    {
        if (a == b)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out object valB))
            {
                return false;
            }

            if (!Equals(kvp.Value, valB))
            {
                return false;
            }
        }

        return true;
    }
    #endregion

    #region World state + goal selection

    private Dictionary<string, object> BuildWorldState()
    {
        Dictionary<string, object> ws = new Dictionary<string, object>();

        ws[WS_HAS_POND] = pondCenter != null;
        ws[WS_IN_POND] = InPond;

        bool hasPlayerTarget = playerTarget != null;

        bool playerInteresting = false;
        if (hasPlayerTarget)
        {
            bool inSight = HasLineOfSightTo(playerTarget.position, sightDistance);
            bool nearPond = false;

            if (pondCenter != null)
            {
                float distToPond = Vector3.Distance(playerTarget.position, pondCenter.position);
                nearPond = distToPond <= chaseDistance;
            }

            if (inSight || nearPond)
            {
                _playerInterestingUntilTime = Time.time + playerInterestingMemorySeconds;
                _lastSeenPlayerPosition = playerTarget.position;
                _lastSeenTime = Time.time;
            }

            bool heardNoiseRecently = Time.time <= _noiseInterestingUntilTime;

            playerInteresting = inSight || nearPond || heardNoiseRecently || Time.time <= _playerInterestingUntilTime;
        }

        ws[WS_HAS_PLAYER] = hasPlayerTarget;
        ws[WS_PLAYER_INTERESTING] = playerInteresting;

        ws[WS_CAN_REACH_PLAYER] = CanReachPlayer;

        ws[WS_CAN_HOLD] = CanHoldPlayer;
        ws[WS_NEAR_PLAYER] = CanHoldPlayer;

        ws[WS_IS_MERGED] = IsMerged;
        ws[WS_CAN_SWALLOW] = CanSwallowPlayer;

        ws[WS_CAN_DUPLICATE] = LiquidWorldState.Instance != null && LiquidWorldState.Instance.CanDuplicateNow();

        bool hasMergeRequest = LiquidWorldState.Instance != null && LiquidWorldState.Instance.HasMergeRequest;
        if (LiquidWorldState.Instance != null && LiquidWorldState.Instance.IsMergeRequestExpired())
        {
            hasMergeRequest = false;
        }

        ws[WS_HAS_MERGE_REQUEST] = hasMergeRequest;

        bool isBusy = HasPlayer && CanHoldPlayer;
        ws[WS_IS_BUSY] = isBusy;

        bool canRequestMerge = false;
        if (LiquidWorldState.Instance != null)
        {
            canRequestMerge = LiquidWorldState.Instance.CanRequestMerge(this) && !_requestedMergeHelp;
        }

        ws[WS_CAN_REQUEST_MERGE] = canRequestMerge;

        ws[WS_HAS_LAST_SEEN] = HasRecentLastSeen;

        bool atLastSeen = false;
        if (HasRecentLastSeen)
        {
            float d = Vector3.Distance(transform.position, _lastSeenPlayerPosition);
            atLastSeen = d <= lastSeenArriveRadius;
        }
        ws[WS_AT_LAST_SEEN] = atLastSeen;

        bool heardNoise = Time.time <= _noiseInterestingUntilTime;
        ws[WS_HEARD_NOISE] = heardNoise;
        ws[WS_NOISE_AWARENESS] = _noiseAwareness01;

        ws[WS_PLAYER_HELD] = false;
        ws[WS_MERGE_REQUESTED] = _requestedMergeHelp;
        ws[WS_ACCEPTED_MERGE_REQUEST] = false;
        ws[WS_HELPED_MERGE] = false;
        ws[WS_DUPLICATED] = false;
        ws[WS_RELAXED] = false;
        ws[WS_EMERGED] = false;
        ws[WS_PLAYER_SWALLOWED] = false;
        ws[WS_PATROLLED] = false;

        return ws;
    }

    private Dictionary<string, object> ChooseGoal(Dictionary<string, object> ws)
    {
        Dictionary<string, object> goal = new Dictionary<string, object>();

        bool hasPlayer = ws.ContainsKey(WS_HAS_PLAYER) && (bool)ws[WS_HAS_PLAYER];
        bool playerInteresting = ws.ContainsKey(WS_PLAYER_INTERESTING) && (bool)ws[WS_PLAYER_INTERESTING];
        bool canReachPlayer = ws.ContainsKey(WS_CAN_REACH_PLAYER) && (bool)ws[WS_CAN_REACH_PLAYER];

        bool hasPond = ws.ContainsKey(WS_HAS_POND) && (bool)ws[WS_HAS_POND];
        bool inPond = ws.ContainsKey(WS_IN_POND) && (bool)ws[WS_IN_POND];

        bool hasMergeRequest = ws.ContainsKey(WS_HAS_MERGE_REQUEST) && (bool)ws[WS_HAS_MERGE_REQUEST];
        bool isBusy = ws.ContainsKey(WS_IS_BUSY) && (bool)ws[WS_IS_BUSY];
        bool isMerged = ws.ContainsKey(WS_IS_MERGED) && (bool)ws[WS_IS_MERGED];

        if (hasMergeRequest && !isBusy && !isMerged)
        {
            goal[WS_HELPED_MERGE] = true;
            currentGoalName = "HelpMerge";
            return goal;
        }

        bool canHold = ws.ContainsKey(WS_CAN_HOLD) && (bool)ws[WS_CAN_HOLD];
        if (canHold && !isMerged && !_mergedOnce)
        {
            goal[WS_MERGE_REQUESTED] = true;
            currentGoalName = "RequestMerge";
            return goal;
        }

        bool canSwallow = ws.ContainsKey(WS_CAN_SWALLOW) && (bool)ws[WS_CAN_SWALLOW];
        if (isMerged && canSwallow)
        {
            goal[WS_PLAYER_SWALLOWED] = true;
            currentGoalName = "Swallow";
            return goal;
        }

        bool canDuplicate = ws.ContainsKey(WS_CAN_DUPLICATE) && (bool)ws[WS_CAN_DUPLICATE];
        if (playerInteresting && canReachPlayer && inPond && canDuplicate &&
            LiquidWorldState.Instance != null &&
            LiquidWorldState.Instance.CurrentLiquidCount < LiquidWorldState.Instance.MaxLiquidCount)
        {
            goal[WS_DUPLICATED] = true;
            currentGoalName = "Duplicate";
            return goal;
        }

        if (canHold)
        {
            goal[WS_PLAYER_HELD] = true;
            currentGoalName = "Hold";
            return goal;
        }

        if (hasPlayer && playerInteresting && !canReachPlayer)
        {
            if (hasPond && !inPond)
            {
                goal[WS_IN_POND] = true;
                currentGoalName = "ReturnToPond_Unreachable";
                return goal;
            }

            goal[WS_PATROLLED] = true;
            currentGoalName = "Patrol_Unreachable";
            return goal;
        }

        if (hasPlayer && playerInteresting && canReachPlayer)
        {
            goal[WS_NEAR_PLAYER] = true;
            currentGoalName = "Chase";
            return goal;
        }

        bool hasLastSeen = ws.ContainsKey(WS_HAS_LAST_SEEN) && (bool)ws[WS_HAS_LAST_SEEN];
        if (hasLastSeen)
        {
            goal[WS_AT_LAST_SEEN] = true;
            currentGoalName = "InvestigateLastSeen";
            return goal;
        }

        if (!hasPlayer)
        {
            if (hasPond && !inPond)
            {
                goal[WS_IN_POND] = true;
                currentGoalName = "ReturnToPond";
                return goal;
            }

            if (hasPond && inPond)
            {
                goal[WS_PATROLLED] = true;
                currentGoalName = "PatrolPond";
                return goal;
            }

            goal[WS_RELAXED] = true;
            currentGoalName = "Relax_NoPond";
            return goal;
        }

        if (hasPond && !inPond)
        {
            goal[WS_IN_POND] = true;
            currentGoalName = "ReturnToPond_Fallback";
            return goal;
        }

        goal[WS_RELAXED] = true;
        currentGoalName = "FallbackRelax";
        return goal;
    }

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

        if (_lastNoiseEvent.HasValue)
        {
            NoiseEvent e = _lastNoiseEvent.Value;
            sb.AppendLine("");
            sb.AppendLine($"LastNoise: {e.level} {e.category} awareness={_noiseAwareness01:0.00}");
            sb.AppendLine($"  pos={e.worldPosition} finalRadius={e.finalRadius:0.0}");
        }

        worldStateDump = sb.ToString();
    }

    #endregion

    #region Helper Functions used by actions.
    /// <summary>
    /// Move toward a target using EnemyBase pathfinding.
    /// </summary>
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
        if (targetObj == null)
        {
            return false;
        }

        return Vector3.Distance(transform.position, targetObj.transform.position) <= 1.25f;
    }

    public void SoftSnapToward(Vector3 targetPosition, float backOffDistance)
    {
        Vector3 dir = (targetPosition - transform.position);
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
        {
            return;
        }

        dir.Normalize();
        Vector3 desired = targetPosition - dir * backOffDistance;

        Vector3 moveDir = desired - transform.position;
        moveDir.y = 0f; // Horizontal only; gravity handled by ApplyGravity()

        // Clamp step to preserve the original smooth approach speed
        Vector3 step = moveDir.normalized * Mathf.Min(moveDir.magnitude, 3f * Time.deltaTime);
        characterController.Move(step);
    }

    public Vector3 GetPondSpawnPosition()
    {
        if (pondCenter == null)
        {
            return transform.position;
        }

        Vector2 circle = Random.insideUnitCircle.normalized * Mathf.Max(0.5f, pondRadius * 0.6f);
        return pondCenter.position + new Vector3(circle.x, 0f, circle.y);
    }

    public Vector3 GetPondPatrolPosition()
    {
        if (pondCenter == null)
        {
            return transform.position;
        }

        Vector2 circle = Random.insideUnitCircle * Mathf.Max(0.25f, pondPatrolRadius);
        return pondCenter.position + new Vector3(circle.x, 0f, circle.y);
    }

    public void MarkRequestedMerge()
    {
        _requestedMergeHelp = true;
    }

    public void BecomeMerged()
    {
        if (_mergedOnce)
        {
            return;
        }

        _mergedOnce = true;
        _mergedStage = 2;
        ApplyMergedStageVisual();
    }

    private void ApplyMergedStageVisual()
    {
        float scale = _mergedStage;
        transform.localScale = new Vector3(scale, scale, scale);
    }

    #endregion

    #region Path blocked hook
    protected override void OnPathBlocked()
    {
        base.OnPathBlocked();

        if (_lastRequestedPathWasPlayer)
        {
            MarkPlayerUnreachable();
        }
    }
    #endregion

    #region Focused debug.

    public string DebugDisplayName => name;
    public Transform DebugTransform => transform;

    public string GetDebugText()
    {
        StringBuilder stringBuilder = new StringBuilder(256);

        stringBuilder.AppendLine($"Name: {name}");
        stringBuilder.AppendLine($"State: {CurrentState}");
        stringBuilder.AppendLine($"Goal: {currentGoalName}");
        stringBuilder.AppendLine($"Action: {currentActionName}");
        stringBuilder.AppendLine("");
        stringBuilder.AppendLine(worldStateDump);

        return stringBuilder.ToString();
    }

    #endregion
}