using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Liquid.AI.GOAP;
using Liquid.Audio;
using Liquid.Damage;

/// <summary>
/// Crawler enemy — ambient patrol creature that hunts by sight and noise.
/// </summary>
[DisallowMultipleComponent]
public class CrawlerEnemy : EnemyBase
{
    #region World-state keys
    public const string WS_HAS_PLAYER = "hasPlayer";
    public const string WS_CAN_SEE_PLAYER = "canSeePlayer";
    public const string WS_PLAYER_INTERESTING = "playerInteresting";
    public const string WS_CAN_REACH_PLAYER = "canReachPlayer";

    public const string WS_HAS_NOISE_INTEREST = "hasNoiseInterest";
    public const string WS_AT_INVESTIGATE_POINT = "atInvestigatePoint";

    public const string WS_IN_HOME = "inHome";
    public const string WS_RETURNED_HOME = "returnedHome";
    public const string WS_ROAMED = "roamed";
    public const string WS_IDLED = "idled";

    public const string WS_IN_CHAT = "inChat";
    public const string WS_CHAT_COMPLETE = "chatComplete";

    public const string WS_IN_LEAP_RANGE = "inLeapRange";
    public const string WS_ATTACKED = "attacked";

    public const string WS_SHOULD_RETREAT = "shouldRetreat";
    public const string WS_RETREATING = "retreating";

    public const string WS_IS_DEAD = "isDead";
    #endregion

    #region Inspector Variables.
    [Header("Home / Roam")]
    [Tooltip("Home center transform. If null, uses spawn position.")]
    [SerializeField] private Transform homeCenter;
    [SerializeField] private float homeRadius = 6f;
    [SerializeField] private float roamArriveRadius = 0.5f;
    [SerializeField] private Vector2 idleDurationRange = new Vector2(0.75f, 1.75f);
    [SerializeField] private Vector2 roamDurationRange = new Vector2(1.25f, 2.75f);

    [Header("Perception")]
    [SerializeField] private float sightDistance = 12f;
    [Tooltip("Seconds we remember the player after losing LOS.")]
    [SerializeField] private float lostSightMemorySeconds = 1.25f;

    [Header("Noise")]
    [Tooltip("Minimum noise level that triggers investigation.")]
    [SerializeField] private NoiseLevel minimumInterestNoiseLevel = NoiseLevel.Medium;
    [Tooltip("How long a noise point stays interesting.")]
    [SerializeField] private float noiseInterestMemorySeconds = 2.0f;
    [Tooltip("Arrival radius for noise investigate point.")]
    [SerializeField] private float investigateArriveRadius = 1.0f;

    [Header("Chat")]
    [Tooltip("How close another Crawler must be to trigger peer-chat.")]
    [SerializeField] private float chatRadius = 4f;
    [Tooltip("How long a chat session lasts (seconds).")]
    [SerializeField] private Vector2 chatDurationRange = new Vector2(1.5f, 3.5f);
    [Tooltip("Max group size before a Crawler stops seeking to join.")]
    [SerializeField] private int maxGroupSize = 4;
    [Tooltip("Disturbance during chat reduces attack cost by this multiplier (0..1).")]
    [SerializeField] private float chatDisturbanceAttackCostMultiplier = 0.6f;

    [Header("Chase / Leap Attack")]
    [SerializeField] private float leapRange = 2.25f;
    [SerializeField] private float leapForwardDistance = 2.0f;
    [SerializeField] private float leapDuration = 0.28f;
    [SerializeField] private float leapCooldownSeconds = 1.1f;
    [SerializeField] private float leapDamage = 8f;

    [Tooltip("Base leap cost. Reduced when close, increased when far, further reduced after chat disturbance.")]
    [SerializeField] private float baseLeapCost = 0.6f;

    [Header("Retreat")]
    [Tooltip("Nest transform that crawlers retreat toward. Shared across the group.")]
    [SerializeField] private Transform nestTransform;
    [Tooltip("Crawlers despawn this many path nodes after retreat begins.")]
    [SerializeField] private int retreatDespawnNodeCount = 10;
    [Tooltip("Player health threshold below which retreat becomes more expensive.")]
    [SerializeField] private float retreatHealthBias = 30f;

    [Header("Reachability")]
    [SerializeField] private float unreachableCooldownSeconds = 2.0f;

    [Header("Damage feedback")]
    [SerializeField] private ParticleSystem hitFx;
    [SerializeField] private ParticleSystem deathFx;
    #endregion

    #region GOAP debug fields
    [Header("GOAP Debug")]
    [SerializeField] private string currentGoalName;
    [SerializeField] private string currentActionName;

    [TextArea(6, 20)]
    [SerializeField] private string worldStateDump;
    #endregion


    #region Runtime Stats
    private Vector3 _spawnHomePosition;
    private Vector3 _investigatePoint;

    private float _noiseInterestingUntilTime;
    private float _lostSightUntilTime;
    private float _unreachableUntilTime;
    private float _nextLeapAllowedTime;

    // Chat state.
    private bool _inChat;
    private float _chatEndTime;
    private bool _chatDisturbedThisSession;
    private List<CrawlerEnemy> _chatGroup = new List<CrawlerEnemy>(4);

    // Retreat state.
    private bool _retreating;
    private int _retreatNodesTravelled;

    // Transient achievement flags.
    private bool _roamed;
    private bool _idled;
    private bool _returnedHome;
    private bool _attacked;
    private bool _chatComplete;

    // TODO: Replace with actual player health.
    private float _lastKnownPlayerHealth = 100f;
    #endregion

    #region Public accessors

    public bool HasPlayer => playerTarget != null;
    public Vector3 PlayerPosition => playerTarget != null ? playerTarget.position : transform.position;

    public Vector3 HomePosition => homeCenter != null ? homeCenter.position : _spawnHomePosition;
    public float HomeRadius => homeRadius;
    public float SightDistance => sightDistance;
    public bool CanReachPlayer => Time.time >= _unreachableUntilTime;

    public bool InHome => Vector3.Distance(transform.position, HomePosition) <= homeRadius;

    public Vector2 IdleDurationRange => idleDurationRange;
    public Vector2 RoamDurationRange => roamDurationRange;
    public float RoamArriveRadius => roamArriveRadius;

    public bool HasNoiseInterest => Time.time <= _noiseInterestingUntilTime;
    public Vector3 InvestigatePoint => _investigatePoint;

    public bool InLeapRange =>
        HasPlayer && Vector3.Distance(transform.position, PlayerPosition) <= leapRange;

    public float LeapDuration => leapDuration;
    public float LeapForwardDistance => leapForwardDistance;
    public float LeapCooldownSeconds => leapCooldownSeconds;
    public float LeapDamage => leapDamage;
    public float BaseLeapCost => baseLeapCost;

    // Chat.
    public bool InChat => _inChat;
    public float ChatDisturbanceAttackCostMultiplier => chatDisturbanceAttackCostMultiplier;
    public bool ChatDisturbedThisSession => _chatDisturbedThisSession;

    // Nest / retreat.
    public Vector3 NestPosition => nestTransform != null ? nestTransform.position : HomePosition;
    public bool IsRetreating => _retreating;
    public int RetreatNodesTravelled => _retreatNodesTravelled;
    public int RetreatDespawnNodeCount => retreatDespawnNodeCount;

    public float LastKnownPlayerHealth => _lastKnownPlayerHealth;
    #endregion

    #region EnemyBase overrides.
    protected override void Awake()
    {
        base.Awake();
        _spawnHomePosition = transform.position;
    }

    protected override List<GoapAction> BuildActions()
    {
        return new List<GoapAction>(CrawlerGoapActions.CreateAll());
    }

    protected override void OnBeforeTick()
    {
        UpdateLOSMemory();
        UpdateDebugStrings();
    }

    /// <summary>
    /// Fills the GOAP world state dict.
    /// </summary>
    protected override void PopulateWorldState(Dictionary<string, object> ws)
    {
        ws[WS_IS_DEAD] = isDead;
        ws[WS_HAS_PLAYER] = HasPlayer;
        ws[WS_CAN_SEE_PLAYER] = HasPlayer && HasLineOfSightTo(PlayerPosition, sightDistance);
        ws[WS_PLAYER_INTERESTING] = HasPlayer && (HasLineOfSightTo(PlayerPosition, sightDistance)
                                                  || Time.time <= _lostSightUntilTime);
        ws[WS_CAN_REACH_PLAYER] = CanReachPlayer;

        ws[WS_HAS_NOISE_INTEREST] = HasNoiseInterest;
        ws[WS_AT_INVESTIGATE_POINT] = HasNoiseInterest &&
                                      Vector3.Distance(transform.position, _investigatePoint) <= investigateArriveRadius;

        ws[WS_IN_HOME] = InHome;
        ws[WS_RETURNED_HOME] = _returnedHome;
        ws[WS_ROAMED] = _roamed;
        ws[WS_IDLED] = _idled;

        ws[WS_IN_CHAT] = _inChat;
        ws[WS_CHAT_COMPLETE] = _chatComplete;

        ws[WS_IN_LEAP_RANGE] = InLeapRange;
        ws[WS_ATTACKED] = _attacked;

        ws[WS_SHOULD_RETREAT] = ShouldRetreat();
        ws[WS_RETREATING] = _retreating;
    }

    protected override (string key, Dictionary<string, object> goal) SelectGoal()
    {
        if (isDead)
            return ("Dead", null);

        bool playerInteresting = HasPlayer &&
                                 (HasLineOfSightTo(PlayerPosition, sightDistance) || Time.time <= _lostSightUntilTime);
        bool canReachPlayer = CanReachPlayer;
        bool inLeapRange = InLeapRange;
        bool hasNoise = HasNoiseInterest;
        bool atInvestigate = hasNoise &&
                                 Vector3.Distance(transform.position, _investigatePoint) <= investigateArriveRadius;
        bool inHome = InHome;
        bool shouldRetreat = ShouldRetreat();

        // 1. Retreat takes priority over everything when triggered.
        if (shouldRetreat || _retreating)
        {
            currentGoalName = "Retreat";
            return ("Retreat", new Dictionary<string, object> { { WS_RETREATING, true } });
        }

        // 2. Chase and attack if player is interesting and reachable.
        if (HasPlayer && playerInteresting && canReachPlayer)
        {
            if (inLeapRange && Time.time >= _nextLeapAllowedTime)
            {
                currentGoalName = "LeapAttack";
                return ("LeapAttack", new Dictionary<string, object> { { WS_ATTACKED, true } });
            }

            currentGoalName = "ChasePlayer";
            return ("ChasePlayer", new Dictionary<string, object> { { WS_IN_LEAP_RANGE, true } });
        }

        // 3. Investigate noise.
        if (hasNoise && !atInvestigate)
        {
            currentGoalName = "InvestigateNoise";
            return ("InvestigateNoise", new Dictionary<string, object> { { WS_AT_INVESTIGATE_POINT, true } });
        }

        // 4. Chat with nearby peers. (Only when calm and in home.)
        if (inHome && !_inChat && HasNearbyPeerForChat())
        {
            currentGoalName = "Chat";
            return ("Chat", new Dictionary<string, object> { { WS_CHAT_COMPLETE, true } });
        }

        // 5. Return home if outside.
        if (!inHome)
        {
            currentGoalName = "ReturnHome";
            return ("ReturnHome", new Dictionary<string, object> { { WS_RETURNED_HOME, true } });
        }

        // 6. Idle cycling.
        if (!_roamed)
        {
            currentGoalName = "RoamHome";
            return ("RoamHome", new Dictionary<string, object> { { WS_ROAMED, true } });
        }

        currentGoalName = "IdleHome";
        return ("IdleHome", new Dictionary<string, object> { { WS_IDLED, true } });
    }

    /// <summary>
    /// Interrupt active action if the world has changed significantly enough.
    /// </summary>
    protected override bool ActionStillValid(GoapAction action)
    {
        if (_retreating && action.ActionName != "RetreatToNestAction")
            return false;

        return true;
    }

    protected override void OnNoiseReceived(NoiseEvent noiseEvent)
    {
        if (isDead) return;
        if ((int)noiseEvent.level < (int)minimumInterestNoiseLevel) return;

        _investigatePoint = noiseEvent.worldPosition;
        _noiseInterestingUntilTime = Time.time + noiseInterestMemorySeconds;

        // If in a chat group, flag the whole group as disturbed.
        if (_inChat)
        {
            _chatDisturbedThisSession = true;
            for (int i = 0; i < _chatGroup.Count; i++)
            {
                if (_chatGroup[i] != null)
                    _chatGroup[i].NotifyChatDisturbed();
            }
        }
    }

    protected override void OnDamaged(float amount)
    {
        if (hitFx != null) hitFx.Play(true);

        if (HasPlayer)
            _lostSightUntilTime = Time.time + Mathf.Max(lostSightMemorySeconds, 0.5f);
    }

    protected override void OnDamagedDetailed(DamageInfo damageInfo) { }

    protected override void OnDeath()
    {
        if (deathFx != null)
        {
            deathFx.transform.parent = null;
            deathFx.Play(true);
        }
    }

    protected override void OnPathBlocked()
    {
        base.OnPathBlocked();
        if (HasPlayer) MarkPlayerUnreachable();
    }

    protected override string GetGizmoLabelText()
    {
        return $"{name}\n{CurrentState}\n{currentGoalName}\n{currentActionName}";
    }
    #endregion

    #region Sensors
    private void UpdateLOSMemory()
    {
        if (!HasPlayer) return;

        if (HasLineOfSightTo(PlayerPosition, sightDistance))
            _lostSightUntilTime = Time.time + lostSightMemorySeconds;
    }
    #endregion

    #region Retreat.
    private bool ShouldRetreat()
    {
        return _retreating;
    }

    public void IncrementRetreatNodes()
    {
        _retreatNodesTravelled++;
        if (_retreatNodesTravelled >= retreatDespawnNodeCount)
            Die();
    }

    public void BeginRetreat()
    {
        _retreating = true;
        _retreatNodesTravelled = 0;
    }
    #endregion

    #region Chat.
    private bool HasNearbyPeerForChat()
    {
        // Find a peer within chat radius that is also calm.
        Collider[] hits = Physics.OverlapSphere(transform.position, chatRadius);
        int count = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            CrawlerEnemy peer = hits[i].GetComponent<CrawlerEnemy>();
            if (peer == null || peer == this) continue;
            if (peer.CurrentState == EnemyState.Chasing || peer.CurrentState == EnemyState.Attacking) continue;
            if (peer.InChat) continue;
            count++;
        }

        // Respect max group size.
        return count > 0 && _chatGroup.Count < maxGroupSize;
    }

    public void BeginChat(List<CrawlerEnemy> group)
    {
        _inChat = true;
        _chatDisturbedThisSession = false;
        _chatEndTime = Time.time + Random.Range(chatDurationRange.x, chatDurationRange.y);
        _chatGroup = group ?? new List<CrawlerEnemy>();
    }

    public void EndChat()
    {
        _inChat = false;
        _chatComplete = true;
        _chatGroup.Clear();
    }

    public bool IsChatExpired() => Time.time >= _chatEndTime;

    public void NotifyChatDisturbed()
    {
        _chatDisturbedThisSession = true;
    }
    #endregion

    #region Action helpers.
    public bool IsInRange(GameObject targetObj)
    {
        if (targetObj == null) return false;
        return Vector3.Distance(transform.position, targetObj.transform.position) <= 1.25f;
    }

    public Vector3 GetRandomHomePoint()
    {
        Vector2 circle = Random.insideUnitCircle * Mathf.Max(0.25f, homeRadius);
        return HomePosition + new Vector3(circle.x, 0f, circle.y);
    }

    public bool ReachedPoint(Vector3 point, float radius)
    {
        return Vector3.Distance(transform.position, point) <= radius;
    }

    public void MarkRoamed() => _roamed = true;
    public void MarkIdled() => _idled = true;
    public void MarkReturnedHome() => _returnedHome = true;
    public void MarkChatComplete() => _chatComplete = true;

    public void MarkAttacked()
    {
        _attacked = true;
        _nextLeapAllowedTime = Time.time + leapCooldownSeconds;
    }

    public void MarkPlayerUnreachable()
    {
        _unreachableUntilTime = Time.time + unreachableCooldownSeconds;
    }

    /// <summary>
    /// Returns the dynamic attack cost modifier based on distance to player.
    /// Closer = cheaper, farther = more expensive.
    /// Further reduced if the chat group was disturbed.
    /// </summary>
    public float GetAttackCostModifier()
    {
        if (!HasPlayer) return 1f;

        float dist = Vector3.Distance(transform.position, PlayerPosition);
        float distFactor = Mathf.Clamp(dist / Mathf.Max(0.1f, leapRange), 0.5f, 2f);
        float chatFactor = _chatDisturbedThisSession ? chatDisturbanceAttackCostMultiplier : 1f;
        return distFactor * chatFactor;
    }

    /// <summary>
    /// Retreat cost = distance to nest normalised + player health bias.
    /// </summary>
    public float GetRetreatCost()
    {
        float distToNest = Vector3.Distance(transform.position, NestPosition);
        float distFactor = Mathf.Clamp01(distToNest / Mathf.Max(1f, homeRadius * 3f));
        float healthFactor = Mathf.Clamp01(_lastKnownPlayerHealth / retreatHealthBias);
        return 1f + distFactor + healthFactor;
    }
    #endregion

    #region Debug.
    private void UpdateDebugStrings()
    {
        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine($"Goal: {currentGoalName}");
        sb.AppendLine($"Action: {currentActionName}");
        sb.AppendLine($"Home: pos={HomePosition} inHome={InHome}");
        sb.AppendLine($"Noise: active={HasNoiseInterest} point={_investigatePoint}");
        sb.AppendLine($"Chat: inChat={_inChat} disturbed={_chatDisturbedThisSession}");
        sb.AppendLine($"Retreat: {_retreating} nodes={_retreatNodesTravelled}");
        sb.AppendLine($"Leap: nextAllowed={_nextLeapAllowedTime:0.0} range={InLeapRange}");
        worldStateDump = sb.ToString();
    }

    /// <summary>Called by actions to update the inspector debug label.</summary>
    public void SetDebugActionName(string name) => currentActionName = name;
    #endregion
}