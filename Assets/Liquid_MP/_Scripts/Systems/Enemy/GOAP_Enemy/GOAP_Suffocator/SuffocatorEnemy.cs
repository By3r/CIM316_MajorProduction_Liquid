using Liquid.AI.GOAP;
using Liquid.Audio;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class SuffocatorEnemy : EnemyBase
{
    #region World-state keys.
    public const string WS_HAS_PLAYER = "hasPlayer";
    public const string WS_PLAYER_INTERESTING = "playerInteresting";
    public const string WS_CAN_REACH_PLAYER = "canReachPlayer";
    public const string WS_CAN_HOLD_PLAYER = "canHoldPlayer";
    public const string WS_HAS_LAST_SEEN = "hasLastSeen";
    public const string WS_AT_LAST_SEEN = "atLastSeen";
    public const string WS_POND_ALIVE = "pondAlive";
    public const string WS_ON_POND = "onPond";
    public const string WS_NEAR_POND = "nearPond";
    public const string WS_SLEEPING = "sleeping";
    public const string WS_SLEEPY = "sleepy";
    public const string WS_CAN_MERGE = "canMerge";
    public const string WS_IS_MERGED = "isMerged";
    public const string WS_HOLDING_PLAYER = "holdingPlayer";
    public const string WS_CALLED_FOR_HELP = "calledForHelp";
    public const string WS_PLAYER_EXTRACTING = "playerExtracting";
    public const string WS_PATROLLED = "patrolled";
    #endregion

    #region Variables.
    [Header("Awareness")]
    [SerializeField] private float sightDistance = 18f;
    [SerializeField] private float sightConeHalfAngle = 60f;
    [SerializeField] private float playerInterestingMemory = 1.25f;

    [Header("Noise Awareness")]
    [Range(0f, 1f)]
    [SerializeField] private float noiseAwarenessThreshold = 0.22f;
    [SerializeField] private float noiseInterestMemory = 2.0f;
    [SerializeField] private float noiseAwarenessDecay = 0.18f;
    [SerializeField] private float noiseAwarenessGainMult = 1.0f;

    [Header("Investigate")]
    [SerializeField] private float lastSeenMemory = 4.0f;
    [SerializeField] private float lastSeenArriveRadius = 1.25f;

    [Header("Hold Player")]
    [SerializeField] private float holdRange = 1.4f;
    [Tooltip("Front-facing half-angle for hold (140 deg total).")]
    [SerializeField] private float holdFovHalfAngle = 70f;
    [SerializeField] private float holdDuration = 3.0f;

    [Header("Merge")]
    [SerializeField] private int maxMergedCount = 3;
    [SerializeField] private float mergeRange = 2.0f;
    [SerializeField] private float demergeHealthThreshold = 20f;

    [Header("Sleepiness")]
    [SerializeField] private float sleepinessPassiveRate = 0.05f;
    [SerializeField] private float sleepinessDamageRate = 0.15f;
    [SerializeField] private float sleepinessCombatDrain = 0.02f;
    [Range(0f, 1f)]
    [SerializeField] private float sleepinessThreshold = 0.7f;

    [Header("Sleep / Pond")]
    [SerializeField] private float pondRadius = 3.0f;
    [SerializeField] private float pondRegenPerSecond = 12f;
    [SerializeField] private float nearPondRegenPerSecond = 3f;
    [SerializeField] private float nearPondSleepRadius = 6.0f;

    [Header("Call For Help")]
    [SerializeField] private float callForHelpBaseCost = 1.5f;
    [SerializeField] private float callForHelpRadius = 20f;

    [Header("Patrol / Roam")]
    [SerializeField] private float roamRadius = 10f;
    [SerializeField] private float roamDurationMin = 2.0f;
    [SerializeField] private float roamDurationMax = 4.0f;
    [SerializeField] private int roamMaxRetries = 5;

    [Header("Reachability")]
    [SerializeField] private float unreachableCooldown = 3f;

    [Header("References")]
    [SerializeField] private Transform pondTransform;
    [SerializeField] private Transform extractionSite;
    #endregion

    #region GOAP Debug.
    [Header("GOAP Debug")]
    [SerializeField] private string currentGoalName;
    [SerializeField] private string currentActionName;
    [TextArea(6, 20)]
    [SerializeField] private string worldStateDump;

    public string DebugCurrentGoalName => currentGoalName;
    public string DebugCurrentActionName => currentActionName;
    #endregion

    #region Runtime.
    private Vector3 _spawnPosition;

    private float _playerInterestingUntilTime;
    private Vector3 _lastSeenPlayerPosition;
    private float _lastSeenTime = -999f;

    private float _noiseAwareness01;
    private float _noiseInterestUntilTime;

    private float _unreachableUntilTime;
    private bool _lastPathWasPlayer;

    private float _sleepiness01;
    private bool _isSleeping;

    private bool _isMerged;
    private int _mergedCount = 1;
    private bool _isHoldingPlayer;
    private float _holdTimer;

    private bool _pondAlive = true;
    private bool _playerExtracting;
    private bool _calledForHelp;

    private bool _inCombat;
    private float _lastDamageTakenTime;
    #endregion

    #region Public accessors.
    public bool HasPlayer => playerTarget != null;
    public Vector3 PlayerPosition => playerTarget != null ? playerTarget.position : transform.position;
    public Vector3 PondPosition => pondTransform != null ? pondTransform.position : _spawnPosition;
    public bool PondAlive => _pondAlive;
    public bool CanReachPlayer => Time.time >= _unreachableUntilTime;
    public bool HasRecentLastSeen => (Time.time - _lastSeenTime) <= lastSeenMemory;
    public Vector3 LastSeenPlayerPosition => _lastSeenPlayerPosition;
    public float LastSeenArriveRadius => lastSeenArriveRadius;
    public float HoldDuration => holdDuration;
    public float HoldRange => holdRange;
    public float MergeRange => mergeRange;
    public bool IsMerged => _isMerged;
    public int MergedCount => _mergedCount;
    public bool IsHoldingPlayer => _isHoldingPlayer;
    public float Sleepiness => _sleepiness01;
    public float PondRadius => pondRadius;
    public float NearPondSleepRadius => nearPondSleepRadius;
    public float PondRegenPerSecond => pondRegenPerSecond;
    public float NearPondRegenPerSecond => nearPondRegenPerSecond;
    public float CallForHelpBaseCost => callForHelpBaseCost;
    public float CallForHelpRadius => callForHelpRadius;
    public bool CalledForHelp => _calledForHelp;
    public bool PlayerExtracting => _playerExtracting;
    public Vector2 RoamDurationRange => new Vector2(roamDurationMin, roamDurationMax);
    public int RoamMaxRetries => roamMaxRetries;

    public bool OnPond => pondTransform != null &&
                            Vector3.Distance(transform.position, PondPosition) <= pondRadius;
    public bool NearPond => pondTransform != null &&
                            Vector3.Distance(transform.position, PondPosition) <= nearPondSleepRadius;

    public bool CanHoldPlayer
    {
        get
        {
            if (!HasPlayer) return false;
            float dist = Vector3.Distance(transform.position, PlayerPosition);
            if (dist > holdRange) return false;
            return HasLineOfSightTo(PlayerPosition, holdRange, holdFovHalfAngle);
        }
    }
    #endregion

    #region EnemyBase overrides.
    protected override void Awake()
    {
        base.Awake();
        _spawnPosition = transform.position;
    }

    protected override List<GoapAction> BuildActions()
    {
        return new List<GoapAction>(SuffocatorGoapActions.CreateAll());
    }

    protected override void OnBeforeTick()
    {
        UpdateNoiseAwarenessDecay();
        UpdateSleepiness();
        UpdateCombatState();
        UpdateSleepRegen();
        UpdateDebugStrings();
    }

    protected override void PopulateWorldState(Dictionary<string, object> ws)
    {
        bool hasPlayer = HasPlayer;
        ws[WS_HAS_PLAYER] = hasPlayer;

        bool inSight = hasPlayer && HasLineOfSightTo(PlayerPosition, sightDistance, sightConeHalfAngle);
        if (inSight)
        {
            _playerInterestingUntilTime = Time.time + playerInterestingMemory;
            _lastSeenPlayerPosition = PlayerPosition;
            _lastSeenTime = Time.time;
        }

        bool heardNoise = Time.time <= _noiseInterestUntilTime;
        bool playerInteresting = inSight || heardNoise || Time.time <= _playerInterestingUntilTime;

        ws[WS_PLAYER_INTERESTING] = playerInteresting;
        ws[WS_CAN_REACH_PLAYER] = CanReachPlayer;
        ws[WS_CAN_HOLD_PLAYER] = CanHoldPlayer;

        ws[WS_HAS_LAST_SEEN] = HasRecentLastSeen;
        ws[WS_AT_LAST_SEEN] = HasRecentLastSeen &&
                               Vector3.Distance(transform.position, _lastSeenPlayerPosition) <= lastSeenArriveRadius;

        ws[WS_POND_ALIVE] = _pondAlive;
        ws[WS_ON_POND] = OnPond;
        ws[WS_NEAR_POND] = NearPond;
        ws[WS_SLEEPING] = _isSleeping;
        ws[WS_SLEEPY] = _sleepiness01 >= sleepinessThreshold;

        ws[WS_CAN_MERGE] = CanMergeWithPeer();
        ws[WS_IS_MERGED] = _isMerged;
        ws[WS_HOLDING_PLAYER] = _isHoldingPlayer;

        ws[WS_CALLED_FOR_HELP] = _calledForHelp;
        ws[WS_PLAYER_EXTRACTING] = _playerExtracting;

        ws[WS_PATROLLED] = false;
    }

    protected override (string key, Dictionary<string, object> goal) SelectGoal()
    {
        if (isDead) return ("Dead", null);

        if (!_pondAlive) return ("Dead", null);

        bool sleepy = _sleepiness01 >= sleepinessThreshold;
        bool playerInterest = HasPlayer && (HasLineOfSightTo(PlayerPosition, sightDistance, sightConeHalfAngle) || Time.time <= _playerInterestingUntilTime || Time.time <= _noiseInterestUntilTime);

        bool canReach = CanReachPlayer;

        if (sleepy && !playerInterest)
        {
            string key = OnPond ? "SleepOnPond" : "SleepNearPond";
            currentGoalName = key;
            return (key, new Dictionary<string, object> { { WS_SLEEPING, true } });
        }

        if (playerInterest && canReach)
        {
            if (CanHoldPlayer && !_isHoldingPlayer)
            {
                currentGoalName = "HoldPlayer";
                return ("HoldPlayer", new Dictionary<string, object> { { WS_HOLDING_PLAYER, true } });
            }

            if (!_isMerged && CanMergeWithPeer())
            {
                currentGoalName = "Merge";
                return ("Merge", new Dictionary<string, object> { { WS_IS_MERGED, true } });
            }

            if (!_calledForHelp && CountOutsidePond() == 0)
            {
                currentGoalName = "CallForHelp";
                return ("CallForHelp", new Dictionary<string, object> { { WS_CALLED_FOR_HELP, true } });
            }

            currentGoalName = "ChasePlayer";
            return ("ChasePlayer", new Dictionary<string, object> { { WS_CAN_HOLD_PLAYER, true } });
        }

        if (HasRecentLastSeen)
        {
            currentGoalName = "InvestigateLastSeen";
            return ("InvestigateLastSeen", new Dictionary<string, object> { { WS_AT_LAST_SEEN, true } });
        }

        currentGoalName = "IdleOnPond";
        return ("IdleOnPond", new Dictionary<string, object> { { WS_PATROLLED, true } });
    }

    protected override void OnNoiseReceived(NoiseEvent noiseEvent)
    {
        float dist = Vector3.Distance(transform.position, noiseEvent.worldPosition);
        float t = noiseEvent.finalRadius > 0.01f ? Mathf.Clamp01(1f - (dist / noiseEvent.finalRadius)) : 1f;

        float level = GetNoiseLevelStrength(noiseEvent.level);
        float category = GetNoiseCategoryStrength(noiseEvent.category);
        float ambient = noiseEvent.environmentProfile != null
            ? Mathf.Clamp01(noiseEvent.environmentProfile.AmbientNoiseLevel) : 0f;
        float intensity = Mathf.Clamp01(noiseEvent.intensity01 <= 0f ? 1f : noiseEvent.intensity01);

        float gain = level * category * intensity * t * (1f - ambient) * noiseAwarenessGainMult;
        _noiseAwareness01 = Mathf.Clamp01(_noiseAwareness01 + gain);

        if (_noiseAwareness01 >= noiseAwarenessThreshold)
        {
            _noiseInterestUntilTime = Time.time + noiseInterestMemory;
            _lastSeenPlayerPosition = noiseEvent.worldPosition;
            _lastSeenTime = Time.time;
            _playerInterestingUntilTime = Mathf.Max(_playerInterestingUntilTime, Time.time + playerInterestingMemory);
        }
    }

    protected override void OnDamaged(float amount)
    {
        _lastDamageTakenTime = Time.time;
        _inCombat = true;
        _sleepiness01 = Mathf.Clamp01(_sleepiness01 + sleepinessDamageRate * amount * 0.01f);

        if (_isMerged && currentHealth <= demergeHealthThreshold)
            ForcedDemerge();
    }

    protected override void OnPathBlocked()
    {
        base.OnPathBlocked();
        if (_lastPathWasPlayer) MarkPlayerUnreachable();
    }

    protected override string GetGizmoLabelText()
        => $"{name}\n{CurrentState}\n{currentGoalName}\n{currentActionName}";

    #endregion

    #region Sensors.
    private void UpdateNoiseAwarenessDecay()
    {
        _noiseAwareness01 = Mathf.Max(0f, _noiseAwareness01 - noiseAwarenessDecay * Time.deltaTime);
    }

    private void UpdateSleepiness()
    {
        if (_isSleeping) return;

        _sleepiness01 = Mathf.Clamp01(_sleepiness01 + sleepinessPassiveRate * Time.deltaTime);

        if (_inCombat && Time.time - _lastDamageTakenTime > 0.5f)
            _sleepiness01 = Mathf.Clamp01(_sleepiness01 - sleepinessCombatDrain * Time.deltaTime);
    }

    private void UpdateCombatState()
    {
        if (_inCombat && Time.time - _lastDamageTakenTime > 3f)
            _inCombat = false;
    }

    private void UpdateSleepRegen()
    {
        if (!_isSleeping) return;

        float regen = OnPond ? pondRegenPerSecond : nearPondRegenPerSecond;
        currentHealth = Mathf.Min(maxHealth, currentHealth + regen * Time.deltaTime);

        if (currentHealth >= maxHealth)
        {
            _isSleeping = false;
            _sleepiness01 = 0f;
        }
    }
    #endregion

    #region Action helpers.
    public void MarkPlayerUnreachable()
    {
        _unreachableUntilTime = Time.time + unreachableCooldown;
    }

    public void SetLastPathWasPlayer(bool v) => _lastPathWasPlayer = v;
    public void SetDebugActionName(string n) => currentActionName = n;

    public void BeginSleep()
    {
        _isSleeping = true;
        SetState(EnemyState.Resting);
    }

    public void EndSleep()
    {
        _isSleeping = false;
        _sleepiness01 = 0f;
    }

    public void BeginHoldPlayer()
    {
        _isHoldingPlayer = true;
        _holdTimer = 0f;
        // TODO: lock player movement via PlayerMovementController
    }

    public void TickHold()
    {
        _holdTimer += Time.deltaTime;
        if (_holdTimer >= holdDuration) ReleasePlayer();
    }

    public void ReleasePlayer()
    {
        _isHoldingPlayer = false;
        // TODO: smooth release via PlayerMovementController
    }

    public void BeginMerge(SuffocatorEnemy other)
    {
        if (_mergedCount >= maxMergedCount) return;
        _isMerged = true;
        _mergedCount = Mathf.Min(_mergedCount + other._mergedCount, maxMergedCount);
        other.AbsorbedByMerge();
    }

    public void AbsorbedByMerge()
    {
        _isMerged = false;
        ReturnToPool();
    }

    public void ForcedDemerge()
    {
        if (!_isMerged) return;
        _isMerged = false;
        _mergedCount = 1;
        // TODO: spawn new SuffocatorEnemy near this position from pool
    }

    public void MarkCalledForHelp() => _calledForHelp = true;
    public void NotifyPlayerExtracting(bool v) => _playerExtracting = v;

    public void NotifyPondDead()
    {
        _pondAlive = false;
        Die();
    }

    public Vector3 GetRoamPoint()
    {
        Vector2 circle = Random.insideUnitCircle * Mathf.Max(0.5f, roamRadius);
        return _spawnPosition + new Vector3(circle.x, 0f, circle.y);
    }

    public float GetCombatCostModifier()
    {
        int outside = CountOutsidePond();
        float healthRatio = currentHealth / Mathf.Max(1f, maxHealth);
        float modifier = 1f;

        if (outside > 1) modifier *= 0.7f;
        if (outside <= 1) modifier *= 1.4f;
        if (healthRatio < 0.25f) modifier *= 1.5f;
        if (_playerExtracting) modifier *= 0.6f;

        return modifier;
    }

    public float GetCallForHelpCost()
    {
        float cost = callForHelpBaseCost;
        int outside = CountOutsidePond();
        int total = CountAllSuffocators();

        if (outside > 1) cost *= 0.6f;
        if (OnPond && outside == 0) cost *= 0.7f;
        if (total > 1) cost *= 0.8f;

        return cost;
    }
    #endregion

    #region Pool wiring.

    private SuffocatorPond _ownedByPond;

    /// <summary>Called by SpawnPoolManager after spawning to wire up the pond.</summary>
    public void SetPond(Transform pond)
    {
        pondTransform = pond;
        _ownedByPond = pond != null ? pond.GetComponent<SuffocatorPond>() : null;
    }

    /// <summary>Called by SpawnPoolManager to wire up the extraction site.</summary>
    public void SetExtractionSite(Transform site)
    {
        extractionSite = site;
    }

    /// <summary>Returns true if this Suffocator belongs to the given pond.</summary>
    public bool OwnedByPond(SuffocatorPond pond) => _ownedByPond == pond;

    /// <summary>Returns the pond this Suffocator belongs to.</summary>
    public SuffocatorPond OwnedPond => _ownedByPond;

    protected override void ReturnToPool()
    {
        SpawnPoolManager.Instance?.NotifySuffocatorDied(this, _ownedByPond);
    }

    #endregion

    #region Group helpers.
    private bool CanMergeWithPeer()
    {
        if (_mergedCount >= maxMergedCount) return false;

        Collider[] hits = Physics.OverlapSphere(transform.position, mergeRange);
        for (int i = 0; i < hits.Length; i++)
        {
            SuffocatorEnemy peer = hits[i].GetComponent<SuffocatorEnemy>();
            if (peer == null || peer == this || !peer.PondAlive) continue;
            return true;
        }
        return false;
    }

    private int CountOutsidePond()
    {
        int count = 0;
        SuffocatorEnemy[] all = FindObjectsOfType<SuffocatorEnemy>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == this || all[i].isDead) continue;
            if (!all[i].OnPond) count++;
        }
        return count;
    }

    private int CountAllSuffocators()
    {
        int count = 0;
        SuffocatorEnemy[] all = FindObjectsOfType<SuffocatorEnemy>();
        for (int i = 0; i < all.Length; i++)
        {
            if (!all[i].isDead) count++;
        }
        return count;
    }
    #endregion

    #region Noise helpers.
    private float GetNoiseLevelStrength(NoiseLevel level)
    {
        switch (level)
        {
            case NoiseLevel.None: return 0f;
            case NoiseLevel.Low: return 0.18f;
            case NoiseLevel.Medium: return 0.35f;
            case NoiseLevel.High: return 0.65f;
            case NoiseLevel.Extreme: return 1.00f;
            default: return 0.20f;
        }
    }

    private float GetNoiseCategoryStrength(NoiseCategory category)
    {
        switch (category)
        {
            case NoiseCategory.Footsteps: return 0.90f;
            case NoiseCategory.Sprint: return 1.05f;
            case NoiseCategory.Jump: return 0.95f;
            case NoiseCategory.ObjectImpact: return 1.15f;
            case NoiseCategory.Gunshot: return 1.35f;
            default: return 1.00f;
        }
    }
    #endregion

    #region Debug.
    private void UpdateDebugStrings()
    {
        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine($"Goal: {currentGoalName}");
        sb.AppendLine($"Action: {currentActionName}");
        sb.AppendLine($"Sleepiness: {_sleepiness01:0.00} sleeping={_isSleeping}");
        sb.AppendLine($"Merged: {_isMerged} count={_mergedCount}");
        sb.AppendLine($"Hold: {_isHoldingPlayer} timer={_holdTimer:0.0}");
        sb.AppendLine($"Pond: alive={_pondAlive} onPond={OnPond} nearPond={NearPond}");
        sb.AppendLine($"Noise: awareness={_noiseAwareness01:0.00} interesting={Time.time <= _noiseInterestUntilTime}");
        sb.AppendLine($"Extract: {_playerExtracting} | CalledHelp: {_calledForHelp}");
        worldStateDump = sb.ToString();
    }

    public string GetDebugText()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine($"Name: {name}");
        sb.AppendLine($"State: {CurrentState}");
        sb.AppendLine($"Goal: {currentGoalName}");
        sb.AppendLine($"Action: {currentActionName}");
        sb.AppendLine(worldStateDump);
        return sb.ToString();
    }
    #endregion
}