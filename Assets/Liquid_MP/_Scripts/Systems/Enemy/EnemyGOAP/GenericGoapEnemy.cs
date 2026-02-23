using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Liquid.Audio;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Basic GOAP style enemy that uses the Noise system and grid based AStar.
/// </summary>
public class GenericGoapEnemy : EnemyBase, INoiseListener, IEnemyDebugTarget
{
    [Header("GOAP Settings")]
    [SerializeField] private float nearbyPlayerDistance = 3f;
    [SerializeField] private float sightDistance = 18f;

    [Header("Combat")]
    [Tooltip("Distance at which the genericEnemy is allowed to perform KillPlayer.")]
    [SerializeField] private float killDistance = 1f;

    [Header("Stamina")]
    [SerializeField] private float staminaMax = 100f;
    [Tooltip("How fast stamina drains whenever the genericEnemy is moving (patrol or chase).")]
    [SerializeField] private float staminaDrainPerSecondMove = 5f;
    [Tooltip("How fast stamina drains whenever the genericEnemy is threatening (roaring, posturing).")]
    [SerializeField] private float staminaDrainPerSecondThreaten = 30f;
    [Tooltip("How fast stamina regenerates while resting.")]
    [SerializeField] private float staminaRegenPerSecondRest = 20f;
    [Tooltip("When stamina reaches or exceeds this, HasStamina becomes true again.")]
    [SerializeField] private float staminaRecoveredThreshold = 99f;

    [Header("Patrol")]
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();
    [SerializeField] private float patrolPointReachRadius = 0.75f;
    [Tooltip("If true, choose a random next patrol point instead of a simple sequence.")]
    [SerializeField] private bool randomPatrolOrder = false;

    [Header("Wander")]
    [Tooltip("Max distance from current position to pick random wander destinations.")]
    [SerializeField] private float wanderRadius = 8f;
    [Tooltip("Min seconds to pause after reaching a wander point.")]
    [SerializeField] private float wanderWaitMin = 2f;
    [Tooltip("Max seconds to pause before picking the next wander point.")]
    [SerializeField] private float wanderWaitMax = 5f;
    [Tooltip("Max random point attempts before giving up for this cycle.")]
    [SerializeField] private int wanderMaxRetries = 5;

    [Tooltip("How long after hearing a noise we still consider it 'audible' for GOAP decisions.")]
    [SerializeField] private float noiseMemoryDuration = 3f;

    [Header("Chase Persistence")]
    [Tooltip("After losing sight of the player, keep chasing for this many seconds.")]
    [SerializeField] private float chasePersistDuration = 5f;

    [Header("Sight Cone")]
    [Tooltip("Half-angle of the FOV cone in degrees (e.g. 60 = 120° total). Enemy can only SEE the player within this cone. Hearing is still omni-directional.")]
    [SerializeField] private float sightConeHalfAngle = 60f;
    [SerializeField] private bool drawSightCone = true;
    [SerializeField] private Color sightConeColor = new Color(1f, 1f, 0f, 0.15f);

    [Header("Threaten Goal")]
    [SerializeField] private float threatenCooldown = 3f;
    [SerializeField] private TMP_Text threatenMessage;
    [SerializeField]
    private string[] threatenMessages =
    {
        "You cannot hide forever.",
        "I can hear you in the dark.",
        "The mines will be your grave."
    };

    [Header("Threaten Movement")]
    [Tooltip("When threatening, the enemy will try to orbit around the player instead of standing still.")]
    [SerializeField] private float threatenOrbitRadius = 2.5f;
    [SerializeField] private float threatenOrbitAngularSpeed = 1.2f;
    [SerializeField] private float threatenOrbitRepathInterval = 0.35f;

    private EnemyGoalType _currentGoal = EnemyGoalType.None;
    private float _stamina;
    private int _patrolIndex;

    // Wander state
    private Vector3 _wanderTarget;
    private bool _hasWanderTarget;
    private float _wanderWaitTimer;
    private bool _isWanderWaiting;

    private NoiseLevel _lastNoiseLevel = NoiseLevel.Low;
    private float _lastHeardNoiseTime;
    private Vector3 _lastHeardNoisePosition;

    // Chase persistence — remembers having seen the player
    private float _lastPlayerSpottedTime;
    private Vector3 _lastKnownPlayerPosition;
    private bool _hasEverSpottedPlayer;

    #region Properties for UI / debug
    [SerializeField] private string _debugLastDecisionReason;
    public EnemyGoalType CurrentGoal => _currentGoal;
    public float CurrentStamina => _stamina;
    public NoiseLevel LastNoiseLevel => _lastNoiseLevel;
    public float DebugDistanceToPlayer => DistanceToPlayer;
    public bool DebugLastPathToPlayerFailed => _lastPathToPlayerFailed;
    [SerializeField] private bool _hasStaminaFlag = true;
    public bool DebugHasStaminaFlag => _hasStaminaFlag;
    public string DebugLastDecisionReason => _debugLastDecisionReason;

    private float _lastThreatenTime;
    private bool _lastPathToPlayerFailed;
    #endregion

    private float _threatenOrbitAngle;
    private float _nextThreatenOrbitRepathTime;

    protected override void Awake()
    {
        base.Awake();
        _stamina = staminaMax;
        _hasStaminaFlag = true;

        if (patrolPoints.Count == 0)
        {
            GameObject[] taggedPoints = GameObject.FindGameObjectsWithTag("PatrolPoint");
            foreach (GameObject go in taggedPoints)
            {
                patrolPoints.Add(go.transform);
            }
        }
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

        if (EnemyDebugFocusManager.Instance != null)
        {
            EnemyDebugFocusManager.Instance.Unregister(this);
        }
    }

    /// <summary>
    /// Called by NoiseManager when any noise event reaches this enemy.
    /// </summary>
    public void OnNoiseHeard(NoiseEvent noiseEvent)
    {
        _lastNoiseLevel = noiseEvent.level;
        _lastHeardNoiseTime = Time.time;
        _lastHeardNoisePosition = noiseEvent.worldPosition;
    }

    protected override void Tick()
    {
        UpdateStamina(Time.deltaTime);
        EvaluateAndSetGoal();
        ExecuteCurrentGoal();
    }

    /// <summary>
    /// TODO: Will be its own class later on.
    /// </summary>
    #region World state helpers

    private bool PlayerAlive => playerTarget != null;

    private float DistanceToPlayer
    {
        get
        {
            if (playerTarget == null)
            {
                return float.MaxValue;
            }

            return Vector3.Distance(transform.position, playerTarget.position);
        }
    }

    private bool HasStamina => _hasStaminaFlag;

    private bool PlayerNearby => DistanceToPlayer <= nearbyPlayerDistance;

    private bool PlayerInSight
    {
        get
        {
            if (playerTarget == null)
            {
                return false;
            }

            // Directional sight — enemy must be facing the player within the FOV cone
            return HasLineOfSightTo(playerTarget.position, sightDistance, sightConeHalfAngle);
        }
    }

    private bool PathValidToPlayer => DebugHasValidPath && PlayerAlive;

    private bool CanKillPlayer => PlayerAlive && DistanceToPlayer <= killDistance && PathValidToPlayer;

    private bool PlayerNoiseIsMedium => _lastNoiseLevel == NoiseLevel.Medium;

    private bool PlayerIsLoud => _lastNoiseLevel == NoiseLevel.High || _lastNoiseLevel == NoiseLevel.Maximum;

    private bool PlayerNoiseAudible
    {
        get
        {
            if (!PlayerAlive)
            {
                return false;
            }

            if (_lastHeardNoiseTime <= 0f)
            {
                return false;
            }

            return (Time.time - _lastHeardNoiseTime) <= noiseMemoryDuration;
        }
    }

    private bool HasAnyPatrolPoint => patrolPoints != null && patrolPoints.Count > 0;
    #endregion

    #region Stamina
    private void UpdateStamina(float deltaTime)
    {
        if (_currentGoal == EnemyGoalType.PatrolArea ||
            _currentGoal == EnemyGoalType.ChasePlayer ||
            _currentGoal == EnemyGoalType.WanderArea)
        {
            _stamina -= staminaDrainPerSecondMove * deltaTime;
        }
        else if (_currentGoal == EnemyGoalType.ThreatenPlayer)
        {
            _stamina -= staminaDrainPerSecondThreaten * deltaTime;
        }
        else if (_currentGoal == EnemyGoalType.TakeRest)
        {
            _stamina += staminaRegenPerSecondRest * deltaTime;
        }

        if (PlayerIsLoud)
        {
            float bonusRegen = 0f;

            if (_lastNoiseLevel == NoiseLevel.High)
            {
                bonusRegen = staminaRegenPerSecondRest * 0.5f;
            }
            else if (_lastNoiseLevel == NoiseLevel.Maximum)
            {
                bonusRegen = staminaRegenPerSecondRest;
            }

            _stamina += bonusRegen * deltaTime;
        }

        _stamina = Mathf.Clamp(_stamina, 0f, staminaMax);

        if (_stamina <= 1f)
        {
            _hasStaminaFlag = false;
        }
        else if (_stamina >= staminaRecoveredThreshold)
        {
            _hasStaminaFlag = true;
        }
    }

    #endregion

    #region GOAP evaluation
    private void EvaluateAndSetGoal()
    {
        EnemyGoalType previousGoal = _currentGoal;
        EnemyGoalType newGoal = SelectGoal();

        if (newGoal != previousGoal)
        {
            _currentGoal = newGoal;
            OnGoalChanged(previousGoal, newGoal);
        }
    }

    private EnemyGoalType SelectGoal()
    {
        _debugLastDecisionReason = string.Empty;

        if (!HasStamina)
        {
            _debugLastDecisionReason = "No stamina";
            return EnemyGoalType.TakeRest;
        }

        if (!PlayerAlive)
        {
            _debugLastDecisionReason = "Player dead/missing";
            if (HasAnyPatrolPoint)
            {
                return EnemyGoalType.PatrolArea;
            }

            return EnemyGoalType.WanderArea;
        }

        bool canSeePlayer = PlayerInSight;
        bool mediumNoiseAndAudible = PlayerNoiseIsMedium && PlayerNoiseAudible;

        // Update chase persistence when we can see the player
        if (canSeePlayer)
        {
            _lastPlayerSpottedTime = Time.time;
            _lastKnownPlayerPosition = playerTarget.position;
            _hasEverSpottedPlayer = true;
        }

        // Are we still within chase persistence window?
        bool recentlySawPlayer = _hasEverSpottedPlayer &&
                                 (Time.time - _lastPlayerSpottedTime) <= chasePersistDuration;

        if (CanKillPlayer && HasStamina)
        {
            _debugLastDecisionReason = "In kill range";
            return EnemyGoalType.KillPlayer;
        }

        // Chase if we can currently see OR recently saw the player
        if (canSeePlayer || recentlySawPlayer)
        {
            if (DebugHasValidPath && !CanKillPlayer)
            {
                _debugLastDecisionReason = canSeePlayer ? "Player in sight → chase" : "Recently saw player → pursuing";
                return EnemyGoalType.ChasePlayer;
            }

            if (!DebugHasValidPath && canSeePlayer)
            {
                _debugLastDecisionReason = "Player visible but no path → threaten";
                return EnemyGoalType.ThreatenPlayer;
            }
        }

        if (mediumNoiseAndAudible)
        {
            _debugLastDecisionReason = "Heard medium noise → threaten";
            return EnemyGoalType.ThreatenPlayer;
        }

        _debugLastDecisionReason = "No stimulus → fallback";
        if (HasAnyPatrolPoint)
        {
            return EnemyGoalType.PatrolArea;
        }

        return EnemyGoalType.WanderArea;
    }

    private void OnGoalChanged(EnemyGoalType previousGoal, EnemyGoalType newGoal)
    {
        if (newGoal == EnemyGoalType.PatrolArea && HasAnyPatrolPoint)
        {
            _patrolIndex = FindNearestPatrolPointIndex(transform.position);
            currentPath = null;
            currentPathIndex = 0;
        }

        if (newGoal == EnemyGoalType.WanderArea)
        {
            _hasWanderTarget = false;
            _isWanderWaiting = false;
            _wanderWaitTimer = 0f;
            currentPath = null;
            currentPathIndex = 0;
        }

        if (newGoal == EnemyGoalType.ThreatenPlayer)
        {
            _threatenOrbitAngle = 0f;
            _nextThreatenOrbitRepathTime = 0f;
        }
    }
    #endregion

    /// <summary>
    /// This will be in its own enum 'class' later.
    /// </summary>
    #region Goal execution
    private void ExecuteCurrentGoal()
    {
        switch (_currentGoal)
        {
            case EnemyGoalType.KillPlayer:
                ExecuteKillPlayer();
                break;
            case EnemyGoalType.ChasePlayer:
                ExecuteChasePlayer();
                break;
            case EnemyGoalType.PatrolArea:
                ExecutePatrolArea();
                break;
            case EnemyGoalType.TakeRest:
                ExecuteTakeRest();
                break;
            case EnemyGoalType.ThreatenPlayer:
                ExecuteThreatenPlayer();
                break;
            case EnemyGoalType.WanderArea:
                ExecuteWanderArea();
                break;
            case EnemyGoalType.None:
                SetState(EnemyState.Idle);
                break;
        }
    }

    private void ExecuteKillPlayer()
    {
        SetState(EnemyState.Attacking);

        if (!PlayerAlive)
        {
            return;
        }

        if (!CanKillPlayer)
        {
            return;
        }

        // TODO: Actual player death logic
    }

    private void ExecuteChasePlayer()
    {
        SetState(EnemyState.Chasing);

        if (!PlayerAlive)
        {
            return;
        }

        if (!HasStamina)
        {
            return;
        }

        // If we can see the player, chase their actual position.
        // Otherwise, chase the last known position (persistence).
        Vector3 targetPos;
        if (PlayerInSight)
        {
            targetPos = playerTarget.position;
            _lastKnownPlayerPosition = targetPos;
        }
        else
        {
            targetPos = _lastKnownPlayerPosition;
        }

        bool needNewPath = !DebugHasValidPath || ShouldRecalculatePath(targetPos);
        if (needNewPath)
        {
            bool success = RequestPath(targetPos);
            _lastPathToPlayerFailed = !success;
        }

        if (DebugHasValidPath)
        {
            FollowPath();
        }
    }

    private void ExecutePatrolArea()
    {
        SetState(EnemyState.Moving);

        if (!HasAnyPatrolPoint)
        {
            return;
        }

        Transform targetPoint = patrolPoints[_patrolIndex];
        Vector3 targetPos = targetPoint.position;

        bool needNewPath = !DebugHasValidPath || ShouldRecalculatePath(targetPos);
        if (needNewPath)
        {
            bool success = RequestPath(targetPos);
            if (!success)
            {
                AdvancePatrolIndex();
                currentPath = null;
                currentPathIndex = 0;
                return;
            }
        }

        if (DebugHasValidPath)
        {
            FollowPath();
        }

        float sqrDistance = (transform.position - targetPos).sqrMagnitude;
        float sqrRadius = patrolPointReachRadius * patrolPointReachRadius;

        if (sqrDistance <= sqrRadius)
        {
            AdvancePatrolIndex();
            currentPath = null;
            currentPathIndex = 0;
        }
    }

    private void ExecuteWanderArea()
    {
        // Pause between wander destinations
        if (_isWanderWaiting)
        {
            SetState(EnemyState.Idle);
            _wanderWaitTimer -= Time.deltaTime;

            if (_wanderWaitTimer <= 0f)
            {
                _isWanderWaiting = false;
                _hasWanderTarget = false;
            }

            return;
        }

        SetState(EnemyState.Roaming);

        // Pick a new destination if we don't have one
        if (!_hasWanderTarget)
        {
            PickRandomWanderTarget();

            if (!_hasWanderTarget)
            {
                return; // All retries failed, try again next frame
            }
        }

        // Request path if needed
        bool needNewPath = !DebugHasValidPath || ShouldRecalculatePath(_wanderTarget);
        if (needNewPath)
        {
            bool success = RequestPath(_wanderTarget);
            if (!success)
            {
                // Can't reach target, pick new one next frame
                _hasWanderTarget = false;
                return;
            }
        }

        // Follow the path
        if (DebugHasValidPath)
        {
            FollowPath();
        }

        // Check if we've arrived
        float sqrDistance = (transform.position - _wanderTarget).sqrMagnitude;
        float sqrRadius = patrolPointReachRadius * patrolPointReachRadius;

        if (sqrDistance <= sqrRadius)
        {
            _hasWanderTarget = false;
            _isWanderWaiting = true;
            _wanderWaitTimer = Random.Range(wanderWaitMin, wanderWaitMax);
            currentPath = null;
            currentPathIndex = 0;
        }
    }

    private void PickRandomWanderTarget()
    {
        if (GridPathfinder.Instance == null)
        {
            _hasWanderTarget = false;
            return;
        }

        for (int i = 0; i < wanderMaxRetries; i++)
        {
            // Pick random point within wander radius
            Vector3 randomOffset = Random.insideUnitSphere * wanderRadius;
            randomOffset.y = 0f; // Keep on the same floor plane

            Vector3 candidateTarget = transform.position + randomOffset;

            // Validate directly via GridPathfinder (bypasses RequestPath's 0.25s cooldown)
            List<Vector3> testPath = GridPathfinder.Instance.FindPath(
                transform.position, candidateTarget, walkableLayers);

            if (testPath != null && testPath.Count > 0)
            {
                _wanderTarget = candidateTarget;
                _hasWanderTarget = true;

                // Store the validated path so we don't re-request it
                currentPath = testPath;
                currentPathIndex = 0;
                lastPathTarget = candidateTarget;
                return;
            }
        }

        // All retries failed — no reachable wander point found this frame
        _hasWanderTarget = false;
    }

    private void ExecuteTakeRest()
    {
        SetState(EnemyState.Resting);
    }

    private void ExecuteThreatenPlayer()
    {
        SetState(EnemyState.Threatening);

        if (PlayerAlive)
        {
            _threatenOrbitAngle += threatenOrbitAngularSpeed * Time.deltaTime;

            Vector3 toEnemy = (transform.position - playerTarget.position);
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 0.001f)
            {
                toEnemy = Vector3.forward;
            }
            toEnemy.Normalize();

            Vector3 orbitOffset = Quaternion.Euler(0f, _threatenOrbitAngle * Mathf.Rad2Deg, 0f) * (toEnemy * threatenOrbitRadius);
            Vector3 orbitTarget = playerTarget.position + orbitOffset;

            if (Time.time >= _nextThreatenOrbitRepathTime)
            {
                _nextThreatenOrbitRepathTime = Time.time + threatenOrbitRepathInterval;
                RequestPath(orbitTarget);
            }

            if (DebugHasValidPath)
            {
                FollowPath();
            }
        }

        if (Time.time < _lastThreatenTime + threatenCooldown)
        {
            return;
        }

        _lastThreatenTime = Time.time;

        if (threatenMessages == null || threatenMessages.Length == 0)
        {
            return;
        }

        int index = Random.Range(0, threatenMessages.Length);
        if (threatenMessage != null)
        {
            threatenMessage.text = threatenMessages[index];
        }
    }

    #endregion

    #region Patrol helpers
    private int FindNearestPatrolPointIndex(Vector3 fromPosition)
    {
        int bestIndex = 0;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < patrolPoints.Count; i++)
        {
            Transform pt = patrolPoints[i];
            if (pt == null)
            {
                continue;
            }

            float sqr = (pt.position - fromPosition).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void AdvancePatrolIndex()
    {
        if (patrolPoints.Count <= 1)
        {
            return;
        }

        if (randomPatrolOrder)
        {
            int nextIndex = _patrolIndex;
            int safety = 0;

            while (nextIndex == _patrolIndex && safety < 10)
            {
                nextIndex = Random.Range(0, patrolPoints.Count);
                safety++;
            }

            _patrolIndex = nextIndex;
        }
        else
        {
            _patrolIndex = (_patrolIndex + 1) % patrolPoints.Count;
        }
    }

    #endregion

    #region Focused debug.
    public string DebugDisplayName => name;
    public Transform DebugTransform => transform;

    public string GetDebugText()
    {
        StringBuilder sb = new StringBuilder(256);

        sb.AppendLine($"Name: {name}");
        sb.AppendLine($"State: {CurrentState}");
        sb.AppendLine($"Goal: {CurrentGoal}");
        sb.AppendLine($"Stamina: {_stamina:F1}/{staminaMax:F1}  HasStamina={HasStamina}");
        sb.AppendLine($"PlayerDist: {DebugDistanceToPlayer:F2}  InSight={PlayerInSight}  PathValid={DebugHasValidPath}");
        sb.AppendLine($"Noise: {_lastNoiseLevel}  Audible={PlayerNoiseAudible}");
        sb.AppendLine($"ChasePersist: spotted={_hasEverSpottedPlayer}  sinceSeen={(Time.time - _lastPlayerSpottedTime):F1}s/{chasePersistDuration:F1}s");
        sb.AppendLine($"Wander: hasTarget={_hasWanderTarget}  waiting={_isWanderWaiting}  timer={_wanderWaitTimer:F1}");
        sb.AppendLine($"Decision: {_debugLastDecisionReason}");

        return sb.ToString();
    }

    /// <summary>
    /// Public accessors for diagnostic commands.
    /// </summary>
    public bool DebugPlayerInSight => PlayerInSight;
    public bool DebugPlayerNoiseAudible => PlayerNoiseAudible;
    public bool DebugHasEverSpottedPlayer => _hasEverSpottedPlayer;
    public float DebugTimeSinceLastSpotted => Time.time - _lastPlayerSpottedTime;
    public float DebugChasePersistDuration => chasePersistDuration;
    public bool DebugHasWanderTarget => _hasWanderTarget;
    public bool DebugIsWanderWaiting => _isWanderWaiting;
    public float DebugWanderWaitTimer => _wanderWaitTimer;

    #endregion

#if UNITY_EDITOR
    #region Gizmos

    protected override string GetGizmoLabelText()
    {
        string goal = _currentGoal.ToString();
        if (_currentGoal == EnemyGoalType.ChasePlayer && !PlayerInSight && _hasEverSpottedPlayer)
        {
            goal = "Pursuing (memory)";
        }
        return $"{CurrentState}\n{goal}\nHP:{currentHealth:F0} STA:{_stamina:F0}";
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        if (drawSightCone)
        {
            DrawSightCone();
        }
    }

    /// <summary>
    /// Draws the directional FOV sight cone gizmo in front of the enemy.
    /// Yellow = not seeing player. Red = player in sight.
    /// Sight is directional (FOV cone). Hearing is omni-directional.
    /// </summary>
    private void DrawSightCone()
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 forward = transform.forward;
        float range = sightDistance;
        float halfAngle = sightConeHalfAngle;

        // Color based on whether player is currently visible
        bool seeing = Application.isPlaying && PlayerInSight;
        Color coneColor = seeing ? new Color(1f, 0f, 0f, 0.2f) : sightConeColor;

        Handles.color = coneColor;

        // Draw the cone arc
        Vector3 leftDir = Quaternion.Euler(0, -halfAngle, 0) * forward;
        Vector3 rightDir = Quaternion.Euler(0, halfAngle, 0) * forward;

        // Draw cone lines
        Gizmos.color = coneColor;
        Gizmos.DrawRay(origin, leftDir * range);
        Gizmos.DrawRay(origin, rightDir * range);
        Gizmos.DrawRay(origin, forward * range);

        // Draw arc at the end of the cone
        Handles.DrawWireArc(origin, Vector3.up, leftDir, halfAngle * 2f, range);

        // Draw filled arc for better visibility
        Color filledColor = coneColor;
        filledColor.a *= 0.5f;
        Handles.color = filledColor;
        Handles.DrawSolidArc(origin, Vector3.up, leftDir, halfAngle * 2f, range);

        // Draw a line to the player if we can see them
        if (seeing && playerTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, playerTarget.position + Vector3.up * 1.5f);
        }

        // Draw line to last known position if pursuing from memory
        if (Application.isPlaying && _hasEverSpottedPlayer && !seeing &&
            (Time.time - _lastPlayerSpottedTime) <= chasePersistDuration)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f); // orange
            Gizmos.DrawLine(origin, _lastKnownPlayerPosition + Vector3.up * 0.5f);
            Gizmos.DrawSphere(_lastKnownPlayerPosition + Vector3.up * 0.5f, 0.3f);
        }
    }

    #endregion
#endif
}