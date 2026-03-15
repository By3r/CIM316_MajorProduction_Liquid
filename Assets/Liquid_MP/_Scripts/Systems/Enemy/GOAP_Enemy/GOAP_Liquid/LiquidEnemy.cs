using Liquid.AI.GOAP;
using Liquid.Audio;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Liquid enemy. Single pursuit enemy that spawns only at extreme noise.
/// </summary>
[DisallowMultipleComponent]
public class LiquidEnemy : EnemyBase
{
    #region World-state keys

    public const string WS_HAS_PLAYER = "hasPlayer";
    public const string WS_PLAYER_IN_RANGE = "playerInRange";
    public const string WS_PURSUING = "pursuing";
    public const string WS_LUNGE_BLOCKED = "lungeBlocked";
    public const string WS_SHOULD_LEAVE = "shouldLeave";
    public const string WS_LEFT = "left";

    #endregion

    #region Inspector

    [Header("Lunge Movement")]
    [Tooltip("Speed of each lunge in units/s.")]
    [SerializeField] private float lungeSpeed = 9f;
    [Tooltip("Distance to maintain ahead of current position when checking for walls.")]
    [SerializeField] private float wallCheckDistance = 0.6f;
    [Tooltip("Layers counted as walls that block a lunge.")]
    [SerializeField] private LayerMask wallMask;
    [Tooltip("How long to wait after hitting a wall before reorienting (seconds).")]
    [SerializeField] private float reorientDelay = 0.2f;
    [Tooltip("How long before the Liquid gives up on a failed lunge attempt and counts it.")]
    [SerializeField] private float lungeAttemptTimeout = 3.0f;

    [Header("Detection")]
    [Tooltip("Radius within which the Liquid can 'sense' the player for pursuit.")]
    [SerializeField] private float pursuitRadius = 20f;
    [Tooltip("Player within this distance is considered in range for instant kill.")]
    [SerializeField] private float killRange = 0.9f;

    [Header("GOAP Cost")]
    [Tooltip("Noise loudness at or above this is considered extreme. Pursue is cheap.")]
    [Range(0f, 1f)]
    [SerializeField] private float extremeNoiseLoudness = 0.8f;
    [Tooltip("Noise loudness at or above this (below extreme) is considered moderate.")]
    [Range(0f, 1f)]
    [SerializeField] private float moderateNoiseLoudness = 0.4f;
    [Tooltip("How many failed lunge attempts at moderate noise before disabling.")]
    [SerializeField] private int failedAttemptsLimit = 2;
    [Tooltip("How long after all noise fades before the Liquid leaves.")]
    [SerializeField] private float silenceLeaveDelay = 2.0f;

    [Header("Disable / Leave")]
    [Tooltip("Time to spend retreating before fully disabling the component.")]
    [SerializeField] private float leaveAnimationDuration = 1.0f;
    #endregion

    #region GOAP Debug.
    [Header("GOAP Debug")]
    [SerializeField] private string currentGoalName;
    [SerializeField] private string currentActionName;
    [TextArea(6, 20)]
    [SerializeField] private string worldStateDump;

    #endregion

    #region Runtime.
    private LungeAxis _currentAxis;
    private int _currentAxisSign;
    private bool _isLunging;
    private bool _lungeBlocked;
    private float _reorientUntil;
    private float _lungeAttemptStart;
    private int _failedLungeAttempts;
    private bool _shouldLeave;
    private bool _hasLeft;
    private float _lastNoiseLoudness;
    private float _noiseReceivedTime;
    private float _silenceStartTime = float.MaxValue;

    private enum LungeAxis { North, South, East, West }
    #endregion

    #region Public accessors
    public bool HasPlayer => playerTarget != null;
    public float KillRange => killRange;
    public bool ShouldLeave => _shouldLeave;
    public bool HasLeft => _hasLeft;
    public bool LungeBlocked => _lungeBlocked;
    public int FailedAttempts => _failedLungeAttempts;
    public float LastNoiseLoudness => _lastNoiseLoudness;

    public Vector3 PlayerPosition =>
        playerTarget != null ? playerTarget.position : transform.position;

    public bool PlayerInRange =>
        HasPlayer && Vector3.Distance(transform.position, PlayerPosition) <= killRange;

    #endregion

    #region EnemyBase overrides

    protected override List<GoapAction> BuildActions()
    {
        return new List<GoapAction>(LiquidGoapActions.CreateAll());
    }

    protected override void OnBeforeTick()
    {
        UpdateSilenceTimer();
        EvaluateShouldLeave();
        UpdateDebugStrings();
    }

    protected override void PopulateWorldState(Dictionary<string, object> ws)
    {
        ws[WS_HAS_PLAYER] = HasPlayer;
        ws[WS_PLAYER_IN_RANGE] = PlayerInRange;
        ws[WS_PURSUING] = _isLunging;
        ws[WS_LUNGE_BLOCKED] = _lungeBlocked;
        ws[WS_SHOULD_LEAVE] = _shouldLeave;
        ws[WS_LEFT] = _hasLeft;
    }

    protected override (string key, Dictionary<string, object> goal) SelectGoal()
    {
        if (isDead) return ("Dead", null);

        if (_hasLeft) return ("Left", null);

        if (_shouldLeave)
        {
            currentGoalName = "Leave";
            return ("Leave", new Dictionary<string, object> { { WS_LEFT, true } });
        }

        if (HasPlayer)
        {
            currentGoalName = "PursuePlayer";
            return ("PursuePlayer", new Dictionary<string, object> { { WS_PLAYER_IN_RANGE, true } });
        }

        currentGoalName = "Leave";
        return ("Leave", new Dictionary<string, object> { { WS_LEFT, true } });
    }

    protected override void OnNoiseReceived(NoiseEvent noiseEvent)
    {
        _lastNoiseLoudness = noiseEvent.perceivedLoudness01;
        _noiseReceivedTime = Time.time;
        _silenceStartTime = float.MaxValue;

        if (_lastNoiseLoudness < moderateNoiseLoudness)
            _shouldLeave = true;
    }

    protected override void OnDamaged(float amount)
    {
        // Liquid cannot be damaged. TakeDamage is ignored.
        currentHealth = maxHealth;
    }

    protected override void ReturnToPool()
    {
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    protected override string GetGizmoLabelText()
        => $"{name}\n{CurrentState}\n{currentGoalName}\n{currentActionName}";
#endif

    #endregion

    #region Sensors

    private void UpdateSilenceTimer()
    {
        float timeSinceNoise = Time.time - _noiseReceivedTime;
        bool silence = timeSinceNoise >= silenceLeaveDelay;

        if (silence && _silenceStartTime == float.MaxValue)
            _silenceStartTime = Time.time;
    }

    private void EvaluateShouldLeave()
    {
        if (_shouldLeave) return;

        bool silenceTriggered = Time.time >= _silenceStartTime + silenceLeaveDelay;
        bool failedTooMuch = _failedLungeAttempts >= failedAttemptsLimit &&
                                _lastNoiseLoudness < extremeNoiseLoudness;

        if (silenceTriggered || failedTooMuch)
            _shouldLeave = true;
    }
    #endregion

    #region Lunge physics.
    /// <summary>
    /// Selects the cardinal lunge axis that points closest to the player.
    /// Never diagonal.
    /// </summary>
    public void ChooseLungeAxis()
    {
        if (!HasPlayer) return;

        Vector3 toPlayer = PlayerPosition - transform.position;
        toPlayer.y = 0f;

        float absX = Mathf.Abs(toPlayer.x);
        float absZ = Mathf.Abs(toPlayer.z);

        if (absX >= absZ)
        {
            _currentAxis = toPlayer.x >= 0f ? LungeAxis.East : LungeAxis.West;
            _currentAxisSign = toPlayer.x >= 0f ? 1 : -1;
        }
        else
        {
            _currentAxis = toPlayer.z >= 0f ? LungeAxis.North : LungeAxis.South;
            _currentAxisSign = toPlayer.z >= 0f ? 1 : -1;
        }

        _lungeBlocked = false;
        _isLunging = true;
        _lungeAttemptStart = Time.time;
    }

    /// <summary>
    /// Executes one frame of lunge movement along the chosen axis.
    /// Returns true while still lunging. Returns false when blocked or arrived.
    /// </summary>
    public bool TickLunge()
    {
        if (!_isLunging) return false;

        // Timeout counts as a failed attempt.
        if (Time.time - _lungeAttemptStart >= lungeAttemptTimeout)
        {
            RegisterBlockedLunge();
            return false;
        }

        Vector3 direction = GetLungeDirection();
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        // Wall check ahead.
        if (Physics.Raycast(origin, direction, wallCheckDistance, wallMask, QueryTriggerInteraction.Ignore))
        {
            RegisterBlockedLunge();
            return false;
        }

        // Move.
        characterController.Move(direction * (lungeSpeed * Time.deltaTime));

        // Orient to lunge direction.
        if (direction.sqrMagnitude > 0.001f)
            transform.forward = direction;

        return true;
    }

    /// <summary>True when the reorient pause after hitting a wall is over.</summary>
    public bool ReorientComplete => Time.time >= _reorientUntil;

    public void SetDebugActionName(string n) => currentActionName = n;

    public void MarkLeft()
    {
        _hasLeft = true;
        _isLunging = false;
        ReturnToPool();
    }

    private void RegisterBlockedLunge()
    {
        _lungeBlocked = true;
        _isLunging = false;
        _reorientUntil = Time.time + reorientDelay;
        _failedLungeAttempts++;
    }

    private Vector3 GetLungeDirection()
    {
        switch (_currentAxis)
        {
            case LungeAxis.North: return new Vector3(0f, 0f, _currentAxisSign);
            case LungeAxis.South: return new Vector3(0f, 0f, -_currentAxisSign);
            case LungeAxis.East: return new Vector3(_currentAxisSign, 0f, 0f);
            case LungeAxis.West: return new Vector3(-_currentAxisSign, 0f, 0f);
            default: return Vector3.forward;
        }
    }

    /// <summary>
    /// Pursuit cost at planning time based on last perceived noise.
    /// Extreme -> cheap. Moderate -> moderate. Silence -> expensive.
    /// </summary>
    public float GetPursuitCost()
    {
        if (_lastNoiseLoudness >= extremeNoiseLoudness) return 0.5f;
        if (_lastNoiseLoudness >= moderateNoiseLoudness) return 1.5f;
        return 4.0f;
    }

    /// <summary>
    /// Leave cost at planning time. Silence makes leaving cheap.
    /// Extreme noise makes leaving expensive (suppresses the leave goal).
    /// </summary>
    public float GetLeaveCost()
    {
        if (_lastNoiseLoudness >= extremeNoiseLoudness) return 3.5f;
        if (_lastNoiseLoudness >= moderateNoiseLoudness) return 1.2f;
        return 0.4f;
    }
    #endregion

    #region Debug.
    private void UpdateDebugStrings()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine($"Goal: {currentGoalName}");
        sb.AppendLine($"Action: {currentActionName}");
        sb.AppendLine($"Axis: {_currentAxis} sign={_currentAxisSign} lunging={_isLunging}");
        sb.AppendLine($"Blocked: {_lungeBlocked} fails={_failedLungeAttempts}");
        sb.AppendLine($"Noise: {_lastNoiseLoudness:0.00} shouldLeave={_shouldLeave}");
        sb.AppendLine($"Silence: {(_silenceStartTime == float.MaxValue ? "none" : $"{Time.time - _silenceStartTime:0.0}s")}");
        worldStateDump = sb.ToString();
    }
    #endregion
}