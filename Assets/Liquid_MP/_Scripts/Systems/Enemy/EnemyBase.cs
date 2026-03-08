using System.Collections.Generic;
using Liquid.AI.GOAP;
using Liquid.Audio;
using Liquid.Damage;
using UnityEngine;
using System.Text;

/// <summary>
/// Base enemy that moves using a grid-based-AStar-pathfinder.
/// Walls should be tagged "Obstacle".
/// Uses CharacterController for physics-based movement (wall collision, gravity).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public abstract class EnemyBase : MonoBehaviour, IDamageable, INoiseListener
{
    #region Variables
    [Header("Stats")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float moveSpeed = 3f;
    [SerializeField] protected float turnSpeed = 8f;

    [Header("Movement")]
    [SerializeField] protected float waypointTolerance = 0.2f;
    [SerializeField] protected float pathRecalcDistanceThreshold = 2f;
    [SerializeField] protected LayerMask obstacleMask;

    [Header("Vertical Movement")]
    [Tooltip("False = ground enemy (y movement stripped, gravity applied). " +
             "True  = wall/ceiling enemy (y movement kept, no gravity).")]
    [SerializeField] protected bool useVerticalMovement = false;

    [Header("Physics")]
    [SerializeField] protected float gravity = -9.81f;

    [Header("Navigation")]
    [Tooltip("Layers this enemy is allowed to walk on. Empty = any walkable node.")]
    [SerializeField] protected LayerMask walkableLayers;

    [Header("References")]
    [SerializeField] protected Transform modelRoot;
    [SerializeField] protected Transform playerTarget;
    public Transform PlayerTarget => playerTarget;

    [Header("GOAP")]
    [Tooltip("How often the GOAP planner reruns (seconds). But remember that lower = more reactive and more CPU. ;;")]
    [SerializeField] protected float replanInterval = 0.35f;

    [Header("Path Failure")]
    [Tooltip("How many consecutive full-path failures before the action's cost gets inflated.")]
    [SerializeField] protected int pathFailuresBeforeInflation = 2;

    [Header("Debug")]
    [SerializeField] protected EnemyState currentState = EnemyState.Idle;
    [SerializeField] protected bool drawPathGizmos = true;

    [Header("Debug Logging")]
    [Tooltip("Log when a full path cannot be found.")]
    [SerializeField] protected bool logPathFailures = false;
    [Tooltip("Log when mid-path obstacle blocking occurs.")]
    [SerializeField] protected bool logPathBlocked = false;
    [Tooltip("Log every state transition.")]
    [SerializeField] protected bool logStateChanges = false;
    [Tooltip("Log GOAP plan changes.")]
    [SerializeField] protected bool logPlanChanges = false;
    [Tooltip("Minimum seconds between repeated logs of the same type.")]
    [SerializeField] protected float logCooldownSeconds = 1f;
    #endregion

    #region Runtime fields
    protected CharacterController characterController;
    protected Vector3 verticalVelocity;
    protected bool isGrounded;

    protected float currentHealth;
    protected bool isDead;

    // --- Pathfinding ---
    protected List<Vector3> currentPath;
    protected int currentPathIndex;
    protected Vector3 lastPathTarget;

    private float _lastPathRequestTime;
    private const float PathRequestCooldown = 0.25f;

    // Path failure tracking
    private Vector3 _lastFailedDestination;
    private int _consecutivePathFailures;

    // --- GOAP ---
    private List<GoapAction> _availableActions;
    private Queue<GoapAction> _currentPlan;
    private GoapAction _activeAction;
    private Dictionary<string, object> _currentGoal;
    private string _currentGoalKey;   // used to detect goal change for inflation reset
    private float _nextReplanTime;

    // --- Noise ---
    /// <summary>The most recent noise event that reached this enemy.</summary>
    protected NoiseEvent? LastNoiseEvent { get; private set; }
    /// <summary>Perceived loudness of the last noise (0..1, 0 = silence).</summary>
    protected float LastPerceivedLoudness { get; private set; }

    // --- Log cooldowns ---
    private float _nextPathFailLogTime;
    private float _nextPathBlockedLogTime;
    private float _nextStateLogTime;

    // --- Public accessors ---
    public EnemyState CurrentState => currentState;
    public bool IsDead => isDead;
    public bool DebugHasValidPath => HasValidPath;
    #endregion


    #region Subclass Interface
    /// <summary>Return all GoapActions this enemy can use.</summary>
    protected abstract List<GoapAction> BuildActions();

    /// <summary>
    /// Write the current world state into the provided dictionary.
    /// </summary>
    protected abstract void PopulateWorldState(Dictionary<string, object> state);

    /// <summary>
    /// Return the goal the enemy wants to achieve RIGHT NOW.
    /// Return null to pause planning (enemy stays in current state).
    /// </summary>
    protected abstract (string key, Dictionary<string, object> goal) SelectGoal();

    /// <summary>
    /// Use for sensor updates, LOS checks, noise processing, etc....
    /// </summary>
    protected virtual void OnBeforeTick() { }

    /// <summary>
    /// Use for animations, IK, effects, etc..
    /// </summary>
    protected virtual void OnAfterTick() { }

    /// <summary>
    /// Called when the enemy is killed.
    /// Override to play death FX before ReturnToPool is called.
    /// </summary>
    protected virtual void OnDeath() { }

    /// <summary>
    /// Called when the enemy should be returned to the object pool.
    /// TODO: Override this in each enemy subclass to return to their pool instead.
    /// </summary>
    protected virtual void ReturnToPool()
    {
        Destroy(gameObject, 5f); // gets destroyed instead for now.
    }
    #endregion

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        characterController = GetComponent<CharacterController>();

        _availableActions = BuildActions();
        _currentGoal = new Dictionary<string, object>();
    }

    protected virtual void OnEnable()
    {
        if (NoiseManager.Instance != null)
            NoiseManager.Instance.RegisterListener(this);
    }

    protected virtual void OnDisable()
    {
        if (NoiseManager.Instance != null)
            NoiseManager.Instance.UnregisterListener(this);
    }

    protected virtual void Update()
    {
        if (isDead) return;

        if (!useVerticalMovement)
            ApplyGravity();

        OnBeforeTick();
        GoapTick();
        OnAfterTick();
    }

    #region GOAP loop
    private readonly Dictionary<string, object> _worldStateBuffer = new Dictionary<string, object>();

    private void GoapTick()
    {
        // 1st. Decide goal.
        var (goalKey, goal) = SelectGoal();
        if (goal == null) return;

        // Detect goal change -> reset all action cost inflation
        if (goalKey != _currentGoalKey)
        {
            _currentGoalKey = goalKey;
            _currentGoal = goal;
            ResetAllInflation();
            InvalidatePlan();
        }

        // 2nd. Replan on schedule or when plan is empty.
        bool needReplan = (_currentPlan == null || _currentPlan.Count == 0) && _activeAction == null;
        bool replanDue = Time.time >= _nextReplanTime;

        if (needReplan || replanDue)
        {
            _nextReplanTime = Time.time + replanInterval;
            TryReplan();
        }

        // 3rd. Execute active action.
        if (_activeAction != null)
        {
            // Check if world state has changed enough to invalidate this action.
            if (!ActionStillValid(_activeAction))
            {
                if (logPlanChanges) Debug.Log($"{name} active action '{_activeAction.ActionName}' invalidated by world state change. Replanning.", this);

                InvalidatePlan();
                TryReplan();
                return;
            }

            bool done = _activeAction.IsDone(gameObject);
            bool success = _activeAction.Perform(gameObject);

            if (!success)
            {
                if (logPlanChanges)
                    Debug.Log($"{name} action '{_activeAction.ActionName}' returned failure. Replanning.", this);
                InvalidatePlan();
                TryReplan();
                return;
            }

            if (done)
            {
                _activeAction.Reset();
                _activeAction = null;

                if (_currentPlan != null && _currentPlan.Count > 0)
                    AdvancePlan();
            }
        }
    }

    private void TryReplan()
    {
        _worldStateBuffer.Clear();
        PopulateWorldState(_worldStateBuffer);

        if (GoapPlanner.Plan(_availableActions, _worldStateBuffer, _currentGoal, out Queue<GoapAction> plan))
        {
            _currentPlan = plan;

            if (logPlanChanges)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"{name} new plan [{_currentGoalKey}]: ");

                foreach (GoapAction a in plan)
                    sb.Append(a.ActionName).Append(" → ");
                Debug.Log(sb.ToString(), this);
            }

            AdvancePlan();
        }
        else
        {
            if (logPlanChanges)
                Debug.Log($"{name} planner found no plan for goal '{_currentGoalKey}'.", this);
        }
    }

    private void AdvancePlan()
    {
        if (_currentPlan == null || _currentPlan.Count == 0)
        {
            _activeAction = null;
            return;
        }

        _activeAction = _currentPlan.Dequeue();
        _activeAction.Reset();

        if (!_activeAction.CheckProceduralPrecondition(gameObject))
        {
            if (logPlanChanges)
                Debug.Log($"{name} procedural precondition failed for '{_activeAction.ActionName}'. Replanning.", this);
            InvalidatePlan();
            TryReplan();
        }
    }

    private void InvalidatePlan()
    {
        if (_activeAction != null)
        {
            _activeAction.Reset();
            _activeAction = null;
        }
        _currentPlan = null;
    }

    protected virtual bool ActionStillValid(GoapAction action) => true;

    private void ResetAllInflation()
    {
        if (_availableActions == null) return;
        for (int i = 0; i < _availableActions.Count; i++)
            _availableActions[i]?.ResetInflation();
    }

    #endregion

    #region Physics {Sprinkle sprinkle stars}
    protected void ApplyGravity()
    {
        if (characterController == null) return;

        isGrounded = characterController.isGrounded;

        if (isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        verticalVelocity.y += gravity * Time.deltaTime;
        characterController.Move(verticalVelocity * Time.deltaTime);
    }

    /// <summary>
    /// Teleports the enemy, bypassing CharacterController.
    /// </summary>
    public void Teleport(Vector3 position)
    {
        if (characterController != null) characterController.enabled = false;
        transform.position = position;
        if (characterController != null) characterController.enabled = true;
    }
    #endregion

    #region Health
    public virtual void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        OnDamaged(amount);

        if (currentHealth <= 0f)
            Die();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        TakeDamage(damageInfo.Amount);
        OnDamagedDetailed(damageInfo);
    }

    protected virtual void OnDamaged(float amount) { }
    protected virtual void OnDamagedDetailed(DamageInfo damageInfo) { }

    protected virtual void Die()
    {
        if (isDead) return;

        isDead = true;
        currentState = EnemyState.Dead;
        currentPath = null;

        InvalidatePlan();

        if (NoiseManager.Instance != null)
            NoiseManager.Instance.UnregisterListener(this);

        OnDeath();
        ReturnToPool();
    }
    #endregion
    
    #region Pathfinding
    protected bool HasValidPath => currentPath != null && currentPathIndex < currentPath.Count;

    /// <summary>
    /// Combined move call for GOAP actions.
    ///
    /// Tries to find a full path to destination. If the full path fails,
    /// attempts a partial path to the closest reachable node via FindPartialPath.
    /// If both fail, inflates the calling action's cost so the planner will
    /// look for alternatives after enough failures.
    ///
    /// Returns true while the enemy is still moving toward destination.
    /// Returns false when the enemy has arrived (within waypointTolerance).
    /// GOAP actions should return true from Perform() while TryGoTo returns true,
    /// and mark themselves done when TryGoTo returns false.
    /// </summary>
    public bool TryGoTo(Vector3 destination, GoapAction callingAction = null)
    {
        // Reached destination?
        if (Vector3.Distance(transform.position, destination) <= waypointTolerance)
        {
            _consecutivePathFailures = 0;
            return false; // Arrived.
        }

        // Already have a valid path heading roughly toward this destination
        if (HasValidPath && !ShouldRecalculatePath(destination))
        {
            FollowPath();
            return true; // Still moving.
        }

        // Try the full path.
        if (RequestPath(destination))
        {
            _consecutivePathFailures = 0;
            _lastFailedDestination = Vector3.zero;
            FollowPath();
            return true;
        }

        // Full path failed —> try the partial path.
        List<Vector3> partial = GridPathfinder.Instance != null ? GridPathfinder.Instance.FindPartialPath(transform.position, destination, walkableLayers) : null;

        if (partial != null && partial.Count > 0)
        {
            currentPath = partial;
            currentPathIndex = 0;
            lastPathTarget = destination;
            _consecutivePathFailures = 0;
            FollowPath();

            return true;
        }

        // Both failed.
        _consecutivePathFailures++;

        if (logPathFailures && Time.time >= _nextPathFailLogTime)
        {
            _nextPathFailLogTime = Time.time + Mathf.Max(0.1f, logCooldownSeconds);
            Debug.LogWarning($"{name} TryGoTo failed (full + partial) to {destination}. Consecutive failures: {_consecutivePathFailures}", this);
                            
        }

        // Inflate the calling action's cost after enough failures.
        if (callingAction != null && _consecutivePathFailures >= pathFailuresBeforeInflation)
        {
            // Only inflate if this is the same destination that keeps failing.
            if (_lastFailedDestination == Vector3.zero ||
                Vector3.Distance(_lastFailedDestination, destination) < 0.5f)
            {
                callingAction.InflateCost();
            }
        }

        _lastFailedDestination = destination;
        return true; // still nominally "moving", planner will replan soon.
    }

    /// <summary>
    /// Raw path request. Returns true if a complete path was found.
    /// Prefer TryGoTo for action movement.
    /// </summary>
    protected bool RequestPath(Vector3 destination)
    {
        if (Time.time < _lastPathRequestTime + PathRequestCooldown)
            return HasValidPath;

        _lastPathRequestTime = Time.time;

        if (GridPathfinder.Instance == null)
        {
            Debug.LogWarning("No GridPathfinder instance found.", this);
            currentPath = null;
            currentPathIndex = 0;
            return false;
        }

        List<Vector3> newPath = GridPathfinder.Instance.FindPath(
            transform.position, destination, walkableLayers);

        if (newPath == null || newPath.Count == 0)
        {
            currentPath = null;
            currentPathIndex = 0;

            if (logPathFailures && Time.time >= _nextPathFailLogTime)
            {
                _nextPathFailLogTime = Time.time + Mathf.Max(0.1f, logCooldownSeconds);
                Debug.LogWarning($"{name} RequestPath failed. Start: {transform.position}, Target: {destination}", this);
            }

            return false;
        }

        currentPath = newPath;
        currentPathIndex = 0;
        lastPathTarget = destination;
        return true;
    }

    protected void FollowPath()
    {
        if (!HasValidPath) return;

        Vector3 targetPoint = currentPath[currentPathIndex];
        Vector3 direction = targetPoint - transform.position;
        float distance = direction.magnitude;

        if (distance <= waypointTolerance)
        {
            currentPathIndex++;
            if (!HasValidPath)
            {
                SetState(EnemyState.Idle);
                return;
            }
            targetPoint = currentPath[currentPathIndex];
            direction = targetPoint - transform.position;
            distance = direction.magnitude;
        }

        if (distance <= 0f) return;

        direction.Normalize();

        float stepDistance = moveSpeed * Time.deltaTime;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction,
            out RaycastHit hit, stepDistance + 0.2f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Obstacle"))
            {
                OnPathBlocked();
                return;
            }
        }

        Vector3 moveVector = direction * stepDistance;
        if (!useVerticalMovement)
            moveVector.y = 0f;

        characterController.Move(moveVector);

        Vector3 flatDir = useVerticalMovement ? direction: new Vector3(direction.x, 0f, direction.z);
           
        if (flatDir.sqrMagnitude > 0.0001f)
        {
            Vector3 newForward = Vector3.Slerp(transform.forward, flatDir.normalized, turnSpeed * Time.deltaTime);
            transform.forward = newForward;
        }
    }

    protected virtual void OnPathBlocked()
    {
        if (logPathBlocked && Time.time >= _nextPathBlockedLogTime)
        {
            _nextPathBlockedLogTime = Time.time + Mathf.Max(0.1f, logCooldownSeconds);
            Debug.Log($"{name} path blocked mid-movement.", this);
        }

        currentPath = null;
        currentPathIndex = 0;
    }

    protected bool ShouldRecalculatePath(Vector3 newTargetPosition)
    {
        return Vector3.Distance(lastPathTarget, newTargetPosition) >= pathRecalcDistanceThreshold;
    }
    #endregion

    #region State management.
    public virtual void SetState(EnemyState newState)
    {
        if (currentState == newState) return;

        EnemyState prev = currentState;
        currentState = newState;
        OnStateChanged(prev, newState);
    }

    protected virtual void OnStateChanged(EnemyState oldState, EnemyState newState)
    {
        switch (newState)
        {
            case EnemyState.Idle:
            case EnemyState.Resting:
                currentPath = null;
                currentPathIndex = 0;
                break;
        }

        if (logStateChanges && Time.time >= _nextStateLogTime)
        {
            _nextStateLogTime = Time.time + Mathf.Max(0.1f, logCooldownSeconds);
            Debug.Log($"{name} state: {oldState} → {newState}", this);
        }
    }

    #endregion

    #region Line of sight.
    /// <summary>360° LOS check. Doesn't have FOV restriction.</summary>
    protected bool HasLineOfSightTo(Vector3 worldPosition, float maxDistance)
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 direction = (worldPosition + Vector3.up * 1.5f) - origin;
        float distance = direction.magnitude;

        if (distance > maxDistance) return false;

        direction.Normalize();
        return !Physics.Raycast(origin, direction, distance, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>Directional LOS check with FOV cone.</summary>
    /// <param name="fovHalfAngle">Half-angle in degrees (e.g. 60 = 120° total FOV).</param>
    protected bool HasLineOfSightTo(Vector3 worldPosition, float maxDistance, float fovHalfAngle)
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 toTarget = (worldPosition + Vector3.up * 1.5f) - origin;
        float distance = toTarget.magnitude;

        if (distance > maxDistance) return false;

        Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);

        if (flatToTarget.sqrMagnitude > 0.001f && flatForward.sqrMagnitude > 0.001f)
        {
            if (Vector3.Angle(flatForward, flatToTarget) > fovHalfAngle)
                return false;
        }

        toTarget.Normalize();
        return !Physics.Raycast(origin, toTarget, distance, obstacleMask, QueryTriggerInteraction.Ignore);
    }
    #endregion
    
    #region Noise listener.
    /// <summary>
    /// Called by NoiseManager when a noise event reaches this enemy.
    /// </summary>
    public void OnNoiseHeard(NoiseEvent noiseEvent)
    {
        LastNoiseEvent = noiseEvent;
        LastPerceivedLoudness = noiseEvent.perceivedLoudness01;
        OnNoiseReceived(noiseEvent);
    }

    /// <summary>
    /// Override in subclasses for immediate noise reactions
    /// (e.g. snapping head toward noise source, alerting group members).
    /// </summary>
    protected virtual void OnNoiseReceived(NoiseEvent noiseEvent) { }
    #endregion


    #region Gizmos
    [Header("Gizmo Settings")]
    [SerializeField] protected bool drawStateLabel = true;
    [SerializeField] protected int stateLabelFontSize = 16;

#if UNITY_EDITOR
    protected virtual void OnDrawGizmos()
    {
        if (drawStateLabel) DrawStateLabel();
    }
#endif

    protected virtual void OnDrawGizmosSelected()
    {
        if (!drawPathGizmos || currentPath == null) return;

        Gizmos.color = Color.cyan;
        for (int i = currentPathIndex; i < currentPath.Count - 1; i++)
        {
            Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            Gizmos.DrawSphere(currentPath[i], 0.1f);
        }
    }

#if UNITY_EDITOR
    private void DrawStateLabel()
    {
        Vector3 labelPos = transform.position + Vector3.up * 2.8f;
        string label = GetGizmoLabelText();

        GUIStyle style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
        {
            normal = { textColor = GetStateLabelColor() },
            fontSize = stateLabelFontSize,
            alignment = TextAnchor.MiddleCenter
        };

        UnityEditor.Handles.Label(labelPos, label, style);
    }

    protected virtual string GetGizmoLabelText()
    {
        string planInfo = _activeAction != null ? $"\n{_activeAction.ActionName}" : "";
        return $"{currentState}{planInfo}";
    }

    protected virtual Color GetStateLabelColor()
    {
        switch (currentState)
        {
            case EnemyState.Idle: return Color.white;
            case EnemyState.Moving: return Color.yellow;
            case EnemyState.Attacking: return Color.red;
            case EnemyState.Resting: return Color.cyan;
            case EnemyState.Threatening: return new Color(1f, 0.5f, 0f);
            case EnemyState.Dead: return Color.gray;
            case EnemyState.Chasing: return new Color(1f, 0.3f, 0f);
            case EnemyState.Alerted: return Color.magenta;
            case EnemyState.Roaming: return Color.green;
            default: return Color.white;
        }
    }
#endif

    #endregion
}