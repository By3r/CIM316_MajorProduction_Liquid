using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base enemy that moves using a grid-based-AStar-pathfinder.
/// Walls should be tagged "Obstacle".
/// Uses CharacterController for physics-based movement (wall collision, gravity).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public abstract class EnemyBase : MonoBehaviour
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

    [Header("Physics")]
    [SerializeField] protected float gravity = -9.81f;

    protected CharacterController characterController;
    protected Vector3 verticalVelocity;
    protected bool isGrounded;

    [Header("Navigation")]
    [Tooltip("Layers this enemy is allowed to walk on (floor, walls, ceiling, etc.). Empty = use any walkable node.")]
    [SerializeField] protected LayerMask walkableLayers;

    [Header("References")]
    [SerializeField] protected Transform modelRoot;
    [SerializeField] protected Transform playerTarget;
    public Transform PlayerTarget => playerTarget;

    [Header("Debug")]
    [SerializeField] protected EnemyState currentState = EnemyState.Idle;
    [SerializeField] protected bool drawPathGizmos = true;

    [Header("Debug Logging")]
    [Tooltip("Turn OFF for performance.")]
    [SerializeField] protected bool logPathFailures = false;

    [Tooltip(" Turn OFF for performance.")]
    [SerializeField] protected bool logPathBlocked = false;

    [Tooltip("Turn OFF for performance.")]
    [SerializeField] protected bool logStateChanges = false;

    [Tooltip("Minimum seconds between repeated logs of the same type (per enemy).")]
    [SerializeField] protected float logCooldownSeconds = 1.0f;

    private float _nextAllowedPathFailLogTime;
    private float _nextAllowedPathBlockedLogTime;
    private float _nextAllowedStateLogTime;

    protected float currentHealth;
    protected bool isDead;

    protected List<Vector3> currentPath;
    protected int currentPathIndex;
    protected Vector3 lastPathTarget;

    private float _lastPathRequestTime;
    private const float PathRequestCooldown = 0.25f;

    public EnemyState CurrentState => currentState;
    public bool DebugHasValidPath => HasValidPath;
    #endregion

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        characterController = GetComponent<CharacterController>();
    }

    protected virtual void Update()
    {
        if (isDead)
        {
            return;
        }

        ApplyGravity();
        Tick();
    }

    protected abstract void Tick();

    #region Physics

    /// <summary>
    /// Applies gravity each frame using the CharacterController.
    /// Mirrors the player's MovementController gravity pattern.
    /// </summary>
    protected void ApplyGravity()
    {
        if (characterController == null) return;

        isGrounded = characterController.isGrounded;

        // Small downward force when grounded to keep the CC anchored (same as player pattern)
        if (isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        characterController.Move(verticalVelocity * Time.deltaTime);
    }

    /// <summary>
    /// Teleports the enemy to a position. Disables CharacterController temporarily
    /// to allow direct position change (CC blocks transform.position writes).
    /// Same pattern as PlayerCommands.TeleportPlayer().
    /// </summary>
    public void Teleport(Vector3 position)
    {
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.position = position;

        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    #endregion

    #region Health

    public virtual void TakeDamage(float amount)
    {
        if (isDead)
        {
            return;
        }

        currentHealth -= amount;
        OnDamaged(amount);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    protected virtual void OnDamaged(float amount) { }

    protected virtual void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentState = EnemyState.Dead;
        currentPath = null;

        Destroy(gameObject, 5f);
    }

    #endregion

    #region Pathfinding

    protected bool HasValidPath => currentPath != null && currentPathIndex < currentPath.Count;

    /// <summary>
    /// Requests a path to destination. Returns true if a non empty path was found.
    /// </summary>
    protected bool RequestPath(Vector3 destination)
    {
        if (Time.time < _lastPathRequestTime + PathRequestCooldown)
        {
            return HasValidPath;
        }

        _lastPathRequestTime = Time.time;

        if (GridPathfinder.Instance == null)
        {
            Debug.LogWarning("No GridPathfinder instance found in scene.", this);
            currentPath = null;
            currentPathIndex = 0;
            return false;
        }

        List<Vector3> newPath = GridPathfinder.Instance.FindPath(transform.position, destination, walkableLayers);

        if (newPath == null || newPath.Count == 0)
        {
            currentPath = null;
            currentPathIndex = 0;

            if (logPathFailures && Time.time >= _nextAllowedPathFailLogTime)
            {
                _nextAllowedPathFailLogTime = Time.time + Mathf.Max(0.1f, logCooldownSeconds);
                Debug.LogWarning($"{name} could not find a path. Start: {transform.position}, Target: {destination}", this);
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
        if (!HasValidPath)
        {
            return;
        }

        Vector3 targetPoint = currentPath[currentPathIndex];
        Vector3 direction = targetPoint - transform.position;
        float distance = direction.magnitude;

        if (distance <= waypointTolerance)
        {
            currentPathIndex++;
            if (!HasValidPath)
            {
                currentState = EnemyState.Idle;
                return;
            }

            targetPoint = currentPath[currentPathIndex];
            direction = targetPoint - transform.position;
            distance = direction.magnitude;
        }

        if (distance <= 0f)
        {
            return;
        }

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
        moveVector.y = 0f; // Horizontal only; gravity handled by ApplyGravity()
        characterController.Move(moveVector);

        if (direction.sqrMagnitude > 0.001f)
        {
            Vector3 flatDir = new Vector3(direction.x, 0f, direction.z);
            if (flatDir.sqrMagnitude > 0.0001f)
            {
                Vector3 newForward = Vector3.Slerp(transform.forward, flatDir.normalized, turnSpeed * Time.deltaTime);
                transform.forward = newForward;
            }
        }
    }

    protected virtual void OnPathBlocked()
    {
        if (logPathBlocked && Time.time >= _nextAllowedPathBlockedLogTime)
        {
            _nextAllowedPathBlockedLogTime = Time.time + Mathf.Max(0.1f, logCooldownSeconds);
            Debug.Log($"{name} path blocked, invalidating current path.", this);
        }

        currentPath = null;
        currentPathIndex = 0;
    }

    protected bool ShouldRecalculatePath(Vector3 newTargetPosition)
    {
        float distance = Vector3.Distance(lastPathTarget, newTargetPosition);
        return distance >= pathRecalcDistanceThreshold;
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Omni-directional line-of-sight check (no FOV restriction).
    /// Used by LiquidEnemy and other systems that need 360° awareness.
    /// </summary>
    protected bool HasLineOfSightTo(Vector3 worldPosition, float maxDistance)
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 direction = (worldPosition + Vector3.up * 1.5f) - origin;
        float distance = direction.magnitude;
        if (distance > maxDistance)
        {
            return false;
        }

        direction.Normalize();

        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Directional line-of-sight check with field-of-view restriction.
    /// Only returns true if the target is within the specified half-angle cone in front of the enemy.
    /// </summary>
    /// <param name="worldPosition">Target position to check.</param>
    /// <param name="maxDistance">Maximum detection range.</param>
    /// <param name="fovHalfAngle">Half-angle of the cone in degrees (e.g. 60 = 120° total FOV).</param>
    protected bool HasLineOfSightTo(Vector3 worldPosition, float maxDistance, float fovHalfAngle)
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 toTarget = (worldPosition + Vector3.up * 1.5f) - origin;
        float distance = toTarget.magnitude;

        if (distance > maxDistance)
        {
            return false;
        }

        // FOV check: is the target within the forward-facing cone?
        Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);

        if (flatToTarget.sqrMagnitude > 0.001f && flatForward.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(flatForward, flatToTarget);
            if (angle > fovHalfAngle)
            {
                return false;
            }
        }

        toTarget.Normalize();

        if (Physics.Raycast(origin, toTarget, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return true;
    }

    #region State handling

    /// <summary>
    /// Changes the enemy state in a controlled way.
    /// GOAP actions and AI logic should always use this instead of setting currentState directly.
    /// </summary>
    public virtual void SetState(EnemyState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        EnemyState previousState = currentState;
        currentState = newState;

        OnStateChanged(previousState, newState);
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

        if (logStateChanges && Time.time >= _nextAllowedStateLogTime)
        {
            _nextAllowedStateLogTime = Time.time + Mathf.Max(0.1f, logCooldownSeconds);
            Debug.Log($"{name} state changed: {oldState} -> {newState}", this);
        }
    }
    #endregion
    #endregion

    #region Gizmos

    [Header("Gizmo Settings")]
    [SerializeField] protected bool drawStateLabel = true;
    [SerializeField] protected int stateLabelFontSize = 16;

#if UNITY_EDITOR
    protected virtual void OnDrawGizmos()
    {
        if (drawStateLabel)
        {
            DrawStateLabel();
        }
    }
#endif

    protected virtual void OnDrawGizmosSelected()
    {
        if (!drawPathGizmos || currentPath == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;

        for (int i = currentPathIndex; i < currentPath.Count - 1; i++)
        {
            Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            Gizmos.DrawSphere(currentPath[i], 0.1f);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws a text label above the enemy's head showing current state.
    /// Subclasses can override GetGizmoLabelText() to add more info.
    /// </summary>
    private void DrawStateLabel()
    {
        Vector3 labelPos = transform.position + Vector3.up * 2.8f;
        string label = GetGizmoLabelText();

        GUIStyle style = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
        style.normal.textColor = GetStateLabelColor();
        style.fontSize = stateLabelFontSize;
        style.alignment = TextAnchor.MiddleCenter;

        UnityEditor.Handles.Label(labelPos, label, style);
    }

    /// <summary>
    /// Returns the text to draw above the enemy's head. Override in subclasses for more detail.
    /// </summary>
    protected virtual string GetGizmoLabelText()
    {
        return $"{currentState}";
    }

    /// <summary>
    /// Returns the color for the state label gizmo.
    /// </summary>
    protected virtual Color GetStateLabelColor()
    {
        switch (currentState)
        {
            case EnemyState.Idle: return Color.white;
            case EnemyState.Moving: return Color.yellow;
            case EnemyState.Attacking: return Color.red;
            case EnemyState.Resting: return Color.cyan;
            case EnemyState.Threatening: return new Color(1f, 0.5f, 0f); // orange
            case EnemyState.Dead: return Color.gray;
            case EnemyState.Chasing: return new Color(1f, 0.3f, 0f); // dark orange
            case EnemyState.Alerted: return Color.magenta;
            case EnemyState.Roaming: return Color.green;
            default: return Color.white;
        }
    }
#endif
    #endregion
}