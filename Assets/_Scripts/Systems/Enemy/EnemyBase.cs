using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base enemy that moves using a grid-based-AStar-pathfinder.
/// Walls should be tagged "Obstacle".
/// </summary>
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

    [Header("Navigation")]
    [Tooltip("Layers this enemy is allowed to walk on (floor, walls, ceiling, etc.). Empty = use any walkable node.")]
    [SerializeField] protected LayerMask walkableLayers;

    [Header("References")]
    [SerializeField] protected Transform modelRoot;
    [SerializeField] protected Transform playerTarget;

    [Header("Debug")]
    [SerializeField] protected EnemyState currentState = EnemyState.Idle;
    [SerializeField] protected bool drawPathGizmos = true;

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
    }

    protected virtual void Update()
    {
        if (isDead)
        {
            return;
        }

        Tick();
    }

    protected abstract void Tick();

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

        Debug.Log($"{name} died.");
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

        // Use per-enemy walkableLayers to decide which nodes are allowed.
        List<Vector3> newPath = GridPathfinder.Instance.FindPath(transform.position, destination, walkableLayers);

        if (newPath == null || newPath.Count == 0)
        {
            currentPath = null;
            currentPathIndex = 0;
            Debug.Log($"{name} could not find a path. Start: {transform.position}, Target: {destination}");
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

        // Use full 3D target (x, y, z) from the path so the enemy can move on the Y axis as well.
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

        // Still raycast along the movement direction to check for obstacles.
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction,
                out RaycastHit hit, stepDistance + 0.2f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Obstacle"))
            {
                OnPathBlocked();
                return;
            }
        }

        // Move in full 3D, including Y.
        transform.position += direction * stepDistance;

        // Keep rotation mostly horizontal (no pitching up/down), but you still turn towards movement direction.
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
        Debug.Log($"{name} path blocked, invalidating current path.");
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
    #endregion

    #region Gizmos
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
    #endregion
}