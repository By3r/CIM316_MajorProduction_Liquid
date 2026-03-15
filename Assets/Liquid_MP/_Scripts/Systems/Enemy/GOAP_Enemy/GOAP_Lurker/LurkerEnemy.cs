using System.Collections.Generic;
using System.Text;
using UnityEngine;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Player;
using Liquid.AI.GOAP;
using Liquid.Audio;

/// <summary>
/// Lurker enemy — single instance, ground only, does not damage the player.
/// Active in dark rooms. Triggered by low noise. Steals items then vanishes.
/// </summary>
[DisallowMultipleComponent]
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

    public const string WS_ROOM_IS_DARK = "roomIsDark";
    public const string WS_IN_BRIGHT_LIGHT = "inBrightLight";
    public const string WS_FREAKED_OUT = "freakedOut";

    public const string WS_FLASHLIGHT_ON_ME = "flashlightOnMe";
    public const string WS_FLASHLIGHT_PANICKING = "flashlightPanicking";

    public const string WS_JUMPSCARE_TRIGGERED = "jumpscareTriggered";
    public const string WS_JUMPSCARE_DONE = "jumpscareDone";

    // Achieved / state flags.
    public const string WS_HIDING = "hiding";
    public const string WS_OBSERVED_PLAYER = "observedPlayer";
    public const string WS_HAS_STOLEN = "hasStolen";
    public const string WS_REPOSITIONED = "repositioned";
    #endregion

    #region Variables
    [Header("Perception")]
    [SerializeField] private float sightDistance = 16f;

    [Header("Distances")]
    [SerializeField] private float playerInRangeDistance = 14f;
    [SerializeField] private float closeToPlayerDistance = 4.0f;
    [SerializeField] private float stealRangeDistance = 1.6f;

    [Header("Room / Darkness")]
    [Tooltip("If true, Lurker only activates when the current room is flagged dark. " +
             "Wire RoomDarknessChecker to provide this flag.")]
    [SerializeField] private bool requireDarkRoom = true;

    [Header("Player Quiet Detection")]
    [Tooltip("Player speed below this is considered quiet.")]
    [SerializeField] private float playerQuietSpeedThreshold = 1.25f;

    [Header("Awareness")]
    [Tooltip("How long the player stays 'aware' after the steal.")]
    [SerializeField] private float stealAwarenessSeconds = 3.5f;

    [Header("Flashlight")]
    [Tooltip("How long the Lurker attempts to disable the flashlight when lit.")]
    [SerializeField] private float flashlightPanicDuration = 2.0f;
    [Tooltip("Damage per second from flashlight while shining on Lurker.")]
    [SerializeField] private float flashlightDamagePerSecond = 15f;
    [Tooltip("How many seconds of flashlight shine before the Lurker attempts to flicker it.")]
    [SerializeField] private float flashlightFlickerThreshold = 0.5f;

    [Header("Jumpscare")]
    [Tooltip("Duration of the uninterruptible jumpscare sequence.")]
    [SerializeField] private float jumpscareDuration = 1.5f;
    [Tooltip("Range within which the jumpscare triggers.")]
    [SerializeField] private float jumpscareRange = 1.8f;

    [Header("Light Reaction")]
    [Range(0f, 1f)]
    [SerializeField] private float brightLightThreshold01 = 0.5f;
    [SerializeField] private float lightHitMemorySeconds = 0.15f;
    [SerializeField] private float freakOutDuration = 0.9f;

    [Header("Retreat")]
    [SerializeField] private float retreatDistance = 10f;
    [SerializeField] private float retreatRepathInterval = 0.35f;
    [SerializeField] private float retreatAngleJitterDeg = 25f;
    [SerializeField] private int retreatRetries = 5;

    [Header("Cooldowns")]
    [Tooltip("Cooldown after death before respawn is possible. Shortened by low noise (minimum > 0).")]
    [SerializeField] private float deathCooldown = 60f;
    [Tooltip("Cooldown after disappearing before reappearing. Shortened by low noise (minimum > 0).")]
    [SerializeField] private float disappearCooldown = 8f;
    [Tooltip("Noise loudness below which cooldowns are shortened.")]
    [SerializeField] private float lowNoiseShortenThreshold = 0.15f;
    [Tooltip("Multiplier applied to cooldowns when noise is low (0..1, never 0).")]
    [Range(0.01f, 1f)]
    [SerializeField] private float noiseCooldownMultiplier = 0.5f;

    [Header("Hide / Reappear")]
    [SerializeField] private bool disableModelWhileHiding = true;
    [SerializeField] private float autoReappearAfterSeconds = 2.0f;
    #endregion

    #region GOAP debug
    [Header("GOAP Debug")]
    [SerializeField] private string currentGoalName;
    [SerializeField] private string currentActionName;
    [TextArea(6, 20)]
    [SerializeField] private string worldStateDump;
    #endregion

    // Player tracking.
    private float _playerAwareUntilTime;
    private Vector3 _lastPlayerPosition;
    private float _lastPlayerSpeed;

    // Room darkness.
    private bool _roomIsDark = true;

    // Light hit tracking.
    private float _lastLightHitTime;
    private float _lastLightIntensity01;
    private Vector3 _lastLightSourcePos;
    private float _pendingLightDamageThisFrame;

    // Flashlight tracking.
    private float _flashlightOnMeStartTime;
    private bool _flashlightOnMe;
    private bool _flashlightPanicking;
    private float _flashlightPanicEndTime;

    // Jumpscare.
    private bool _jumpscareTriggered;
    private bool _jumpscareDone;

    // Achieved flags.
    private bool _isHiding;
    private bool _isFreakedOut;
    private bool _hasStolen;
    private bool _observedPlayer;
    private bool _repositioned;

    // Hide timer.
    private float _hideUntilTime;

    // Retreat.
    private Vector3 _cachedRetreatTarget;
    private bool _hasRetreatTarget;
    private float _lastRetreatSolveTime;

    public bool HasPlayer => playerTarget != null;
    public Vector3 PlayerPosition => playerTarget != null ? playerTarget.position : transform.position;
    public float SightDistance => sightDistance;
    public float CloseToPlayerDistance => closeToPlayerDistance;
    public float StealRangeDistance => stealRangeDistance;
    public float FreakOutDuration => freakOutDuration;
    public float JumpscareDuration => jumpscareDuration;
    public float JumpscareRange => jumpscareRange;
    public float FlashlightPanicDuration => flashlightPanicDuration;
    public float FlashlightDamagePerSecond => flashlightDamagePerSecond;
    public float FlashlightFlickerThreshold => flashlightFlickerThreshold;

    public bool InBrightLight =>
        (Time.time - _lastLightHitTime) <= lightHitMemorySeconds &&
        _lastLightIntensity01 >= brightLightThreshold01;

    public bool HasRetreatTarget => _hasRetreatTarget;
    public Vector3 RetreatTarget => _cachedRetreatTarget;
    public bool IsHiding => _isHiding;
    public bool FlashlightOnMe => _flashlightOnMe;
    public bool FlashlightPanicking => _flashlightPanicking;
    public bool JumpscareTriggered => _jumpscareTriggered;
    public bool JumpscareDone => _jumpscareDone;

    #region EnemyBase overrides.
    protected override void Awake()
    {
        base.Awake();

        if (playerTarget == null && PlayerManager.Instance?.CurrentPlayer != null)
            playerTarget = PlayerManager.Instance.CurrentPlayer.transform;

        if (HasPlayer)
        {
            _lastPlayerPosition = PlayerPosition;
            _lastPlayerSpeed = 0f;
        }

        if (modelRoot != null && disableModelWhileHiding)
            modelRoot.gameObject.SetActive(true);
    }

    protected override List<GoapAction> BuildActions()
    {
        return new List<GoapAction>(LurkerGoapActions.CreateAll());
    }

    protected override void OnBeforeTick()
    {
        // Apply light damage accumulated by hazards this frame.
        if (_pendingLightDamageThisFrame > 0f)
        {
            TakeDamage(_pendingLightDamageThisFrame);
            _pendingLightDamageThisFrame = 0f;
        }

        UpdatePlayerSpeedEstimate();
        UpdateRetreatTargetIfNeeded();
        UpdateAutoReappear();
        UpdateFlashlightTracking();
        UpdateDebugStrings();
    }

    protected override void PopulateWorldState(Dictionary<string, object> ws)
    {
        bool hasPlayer = HasPlayer;
        ws[WS_HAS_PLAYER] = hasPlayer;

        bool canSeePlayer = hasPlayer && HasLineOfSightTo(PlayerPosition, sightDistance);
        ws[WS_CAN_SEE_PLAYER] = canSeePlayer;

        float dist = hasPlayer ? Vector3.Distance(transform.position, PlayerPosition) : float.MaxValue;
        ws[WS_PLAYER_IN_RANGE] = dist <= playerInRangeDistance;
        ws[WS_CLOSE_TO_PLAYER] = dist <= closeToPlayerDistance;
        ws[WS_WITHIN_STEAL_RANGE] = dist <= stealRangeDistance;

        ws[WS_PLAYER_IS_QUIET] = _lastPlayerSpeed <= playerQuietSpeedThreshold;
        ws[WS_PLAYER_IS_AWARE] = Time.time <= _playerAwareUntilTime;
        ws[WS_PLAYER_HAS_ITEMS] = HasStealableItems();

        ws[WS_ROOM_IS_DARK] = _roomIsDark;
        ws[WS_IN_BRIGHT_LIGHT] = InBrightLight;
        ws[WS_FREAKED_OUT] = _isFreakedOut;

        ws[WS_FLASHLIGHT_ON_ME] = _flashlightOnMe;
        ws[WS_FLASHLIGHT_PANICKING] = _flashlightPanicking;

        ws[WS_JUMPSCARE_TRIGGERED] = _jumpscareTriggered;
        ws[WS_JUMPSCARE_DONE] = _jumpscareDone;

        ws[WS_HIDING] = _isHiding;
        ws[WS_HAS_STOLEN] = _hasStolen;
        ws[WS_OBSERVED_PLAYER] = _observedPlayer;
        ws[WS_REPOSITIONED] = _repositioned;
    }

    protected override (string key, Dictionary<string, object> goal) SelectGoal()
    {
        if (isDead) return ("Dead", null);

        _observedPlayer = false;
        _repositioned = false;

        bool inBrightLight = InBrightLight;
        bool playerAware = Time.time <= _playerAwareUntilTime;
        bool hasPlayer = HasPlayer;
        bool playerInRange = hasPlayer && Vector3.Distance(transform.position, PlayerPosition) <= playerInRangeDistance;
        bool closeToPlayer = hasPlayer && Vector3.Distance(transform.position, PlayerPosition) <= closeToPlayerDistance;
        bool withinStealRange = hasPlayer && Vector3.Distance(transform.position, PlayerPosition) <= stealRangeDistance;
        bool playerHasItems = HasStealableItems();
        bool playerQuiet = _lastPlayerSpeed <= playerQuietSpeedThreshold;
        bool canSeePlayer = hasPlayer && HasLineOfSightTo(PlayerPosition, sightDistance);
        bool roomDark = _roomIsDark;

        // 0. Light escape.
        if (inBrightLight && _hasRetreatTarget)
        {
            currentGoalName = "EscapeLight";
            return ("EscapeLight", new Dictionary<string, object> { { WS_IN_BRIGHT_LIGHT, false } });
        }

        // 1. Flashlight on me -> panic.
        if (_flashlightOnMe && !_flashlightPanicking)
        {
            currentGoalName = "FlashlightPanic";
            return ("FlashlightPanic", new Dictionary<string, object> { { WS_FLASHLIGHT_PANICKING, true } });
        }

        // 2. If hiding -> reappear.
        if (_isHiding && autoReappearAfterSeconds > 0f)
        {
            currentGoalName = "Reappear";
            return ("Reappear", new Dictionary<string, object> { { WS_HIDING, false } });
        }

        // 3. Jumpscare if close and not yet done.
        if (_jumpscareTriggered && !_jumpscareDone)
        {
            currentGoalName = "Jumpscare";
            return ("Jumpscare", new Dictionary<string, object> { { WS_JUMPSCARE_DONE, true } });
        }

        // 4. Post-steal context routing.
        if (_hasStolen)
        {
            if (_flashlightOnMe)
            {
                // Flashlight on us after steal -> panic + retreat.
                currentGoalName = "PanicRetreatAfterSteal";
                return ("PanicRetreatAfterSteal", new Dictionary<string, object>
                    { { WS_HIDING, true }, { WS_FLASHLIGHT_PANICKING, false } });
            }

            if (playerAware)
            {
                // Spotted after steal -> panic vanish.
                currentGoalName = "PanicVanishAfterSteal";
                return ("PanicVanishAfterSteal", new Dictionary<string, object> { { WS_HIDING, true } });
            }

            // Unaware player after steal -> clean disappear.
            currentGoalName = "DisappearAfterSteal";
            return ("DisappearAfterSteal", new Dictionary<string, object> { { WS_HIDING, true } });
        }

        if (playerAware && !inBrightLight && !_isHiding)
        {
            currentGoalName = "VanishWhenSpotted";
            return ("VanishWhenSpotted", new Dictionary<string, object> { { WS_HIDING, true } });
        }

        // 6. Only activate in dark rooms.
        if (requireDarkRoom && !roomDark)
        {
            currentGoalName = "Lurk";
            return ("Lurk", new Dictionary<string, object> { { WS_REPOSITIONED, true } });
        }

        // 7. Opportunistic steal.
        if (hasPlayer && playerInRange && !playerAware && playerHasItems && playerQuiet && canSeePlayer && !inBrightLight)
        {
            // If close enough -> trigger jumpscare first, then steal.
            if (closeToPlayer && !_jumpscareTriggered)
            {
                currentGoalName = "TriggerJumpscare";
                return ("TriggerJumpscare", new Dictionary<string, object> { { WS_JUMPSCARE_TRIGGERED, true } });
            }

            currentGoalName = "StealOpportunistically";
            return ("StealOpportunistically", new Dictionary<string, object> { { WS_HAS_STOLEN, true } });
        }

        if (hasPlayer && playerInRange && !playerAware && playerQuiet && canSeePlayer && !inBrightLight)
        {
            currentGoalName = "Observe";
            return ("Observe", new Dictionary<string, object> { { WS_OBSERVED_PLAYER, true } });
        }

        currentGoalName = "Lurk";
        return ("Lurk", new Dictionary<string, object> { { WS_REPOSITIONED, true } });
    }

    /// <summary>
    /// Jumpscare must never be interrupted once triggered.
    /// </summary>
    protected override bool ActionStillValid(GoapAction action)
    {
        if (_jumpscareTriggered && !_jumpscareDone)
            return action.ActionName == "JumpscareAction";

        return true;
    }

    protected override void OnNoiseReceived(NoiseEvent noiseEvent)
    {
        // Lurker is triggered by low noise — any noise keeps it active.
    }

    protected override void OnDeath()
    {
        // Death cooldown >> disappear cooldown.
        // The spawn pool will read DeathCooldown to know when to allow respawn.
    }

    protected override void ReturnToPool()
    {
        SpawnPoolManager.Instance?.NotifyLurkerDied(LastPerceivedLoudness);
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    protected override string GetGizmoLabelText()
        => $"{name}\n{CurrentState}\n{currentGoalName}\n{currentActionName}";
#endif
    #endregion

    #region Public Functions.
    /// <summary>
    /// Called by LurkerLightHazard while the lurker is inside a bright light trigger.
    /// </summary>
    public void NotifyHitByLight(Vector3 lightSourceWorldPos, float intensity01, float damagePerSecond)
    {
        if (intensity01 < brightLightThreshold01) return;

        _lastLightHitTime = Time.time;
        _lastLightIntensity01 = intensity01;
        _lastLightSourcePos = lightSourceWorldPos;
        _pendingLightDamageThisFrame += Mathf.Max(0f, damagePerSecond) * Time.deltaTime;
    }

    /// <summary>
    /// Called by the flashlight system each frame while the flashlight cone
    /// intersects this enemy.
    /// </summary>
    public void NotifyFlashlightHit(float intensity01)
    {
        _flashlightOnMe = true;
        _flashlightOnMeStartTime = Mathf.Min(_flashlightOnMeStartTime == 0f
            ? Time.time : _flashlightOnMeStartTime, Time.time);

        _pendingLightDamageThisFrame += flashlightDamagePerSecond * intensity01 * Time.deltaTime;
    }

    /// <summary>
    /// Called by the room system when darkness state changes.
    /// </summary>
    public void SetRoomDark(bool isDark) => _roomIsDark = isDark;
    #endregion

    #region Sensors.
    private void UpdateFlashlightTracking()
    {
        bool wasOnMe = _flashlightOnMe;
        _flashlightOnMe = false;

        if (!wasOnMe)
        {
            _flashlightOnMeStartTime = 0f;
            if (_flashlightPanicking && Time.time >= _flashlightPanicEndTime)
                _flashlightPanicking = false;
        }
    }

    private void UpdatePlayerSpeedEstimate()
    {
        if (!HasPlayer) { _lastPlayerSpeed = 0f; return; }

        Vector3 delta = PlayerPosition - _lastPlayerPosition;
        _lastPlayerSpeed = delta.magnitude / Mathf.Max(0.0001f, Time.deltaTime);
        _lastPlayerPosition = PlayerPosition;
    }

    private void UpdateRetreatTargetIfNeeded()
    {
        if (!InBrightLight) { _hasRetreatTarget = false; return; }

        if (_hasRetreatTarget && Time.time < _lastRetreatSolveTime + retreatRepathInterval) return;

        _lastRetreatSolveTime = Time.time;

        Vector3 away = transform.position - _lastLightSourcePos;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away.Normalize();

        _hasRetreatTarget = false;
        Vector3 bestTarget = transform.position;

        for (int i = 0; i < Mathf.Max(1, retreatRetries); i++)
        {
            float jitter = Random.Range(-retreatAngleJitterDeg, retreatAngleJitterDeg);
            Vector3 dir = Quaternion.Euler(0f, jitter, 0f) * away;
            Vector3 candidate = transform.position + dir * retreatDistance;

            if (GridPathfinder.Instance != null)
            {
                var path = GridPathfinder.Instance.FindPath(transform.position, candidate, walkableLayers);
                if (path != null && path.Count > 0) { bestTarget = candidate; _hasRetreatTarget = true; break; }
            }
            else { bestTarget = candidate; _hasRetreatTarget = true; break; }
        }

        _cachedRetreatTarget = bestTarget;
    }

    private void UpdateAutoReappear()
    {
        if (!_isHiding || autoReappearAfterSeconds <= 0f) return;

        if (_hideUntilTime > 0f && Time.time >= _hideUntilTime)
            SetHidden(false);
    }

    #endregion

    #region Action helpers.
    public void SetHidden(bool hidden)
    {
        if (_isHiding == hidden) return;

        _isHiding = hidden;

        if (hidden && autoReappearAfterSeconds > 0f)
        {
            // Disappear cooldown shortened by low noise but never 0.
            float cd = disappearCooldown;
            if (LastPerceivedLoudness < lowNoiseShortenThreshold)
                cd *= Mathf.Max(0.01f, noiseCooldownMultiplier);
            _hideUntilTime = Time.time + cd;
        }
        else
        {
            _hideUntilTime = 0f;

            _playerAwareUntilTime = 0f;
        }

        if (disableModelWhileHiding && modelRoot != null)
            modelRoot.gameObject.SetActive(!hidden);
    }

    public void SetFreakedOut(bool v) => _isFreakedOut = v;
    public void MarkObserved() => _observedPlayer = true;
    public void MarkRepositioned() => _repositioned = true;
    public void MarkJumpscareTriggered() => _jumpscareTriggered = true;
    public void MarkJumpscareDone() => _jumpscareDone = true;
    public void SetDebugActionName(string n) => currentActionName = n;

    public void MarkStolen()
    {
        _hasStolen = true;
        _playerAwareUntilTime = Time.time + stealAwarenessSeconds;
    }

    public void NotifyPlayerAware(float lingerSeconds = 1.5f)
    {
        float proposed = Time.time + lingerSeconds;
        if (proposed > _playerAwareUntilTime)
            _playerAwareUntilTime = proposed;
    }

    public void BeginFlashlightPanic()
    {
        _flashlightPanicking = true;
        _flashlightPanicEndTime = Time.time + flashlightPanicDuration;
        // TODO: trigger flashlight flicker/disable attempt via FlashlightManager
        // FlashlightManager.Instance?.AttemptDisable(flashlightFlickerThreshold);
    }

    public void EndFlashlightPanic()
    {
        _flashlightPanicking = false;
    }

    public bool IsInRange(GameObject targetObj)
    {
        if (targetObj == null) return false;
        return Vector3.Distance(transform.position, targetObj.transform.position) <= 1.25f;
    }

    public bool ReachedPoint(Vector3 point, float radius)
        => Vector3.Distance(transform.position, point) <= radius;

    public LayerMask WalkableLayers => walkableLayers;

    private bool HasStealableItems()
    {
        if (PlayerInventory.Instance == null) return false;
        for (int i = 0; i < PlayerInventory.Instance.SlotCount; i++)
        {
            var slot = PlayerInventory.Instance.GetSlot(i);
            if (slot != null && !slot.IsEmpty) return true;
        }
        return false;
    }
    #endregion

    #region Debug.
    private void UpdateDebugStrings()
    {
        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine($"Goal: {currentGoalName}");
        sb.AppendLine($"Action: {currentActionName}");
        sb.AppendLine($"Dark: {_roomIsDark} | Light: {InBrightLight} intensity={_lastLightIntensity01:0.00}");
        sb.AppendLine($"Flashlight: onMe={_flashlightOnMe} panicking={_flashlightPanicking}");
        sb.AppendLine($"Jumpscare: triggered={_jumpscareTriggered} done={_jumpscareDone}");
        sb.AppendLine($"Steal: {_hasStolen} | Hiding: {_isHiding} until={_hideUntilTime:0.0}");
        sb.AppendLine($"Player: aware={Time.time <= _playerAwareUntilTime} speed={_lastPlayerSpeed:0.0}");
        worldStateDump = sb.ToString();
    }
    #endregion
}