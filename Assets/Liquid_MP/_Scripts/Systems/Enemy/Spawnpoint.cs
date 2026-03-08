using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Place one of these on every enemy spawn location in the scene.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    #region Inspector

    [Header("Enemy Rules")]
    [Tooltip("Enemy types that ARE allowed to use this spawn point. " +
             "If empty, all types are allowed (unless blacklisted).")]
    [SerializeField] private List<EnemyType> whitelist = new List<EnemyType>();

    [Tooltip("Enemy types that are NEVER allowed to use this spawn point, " +
             "regardless of whitelist.")]
    [SerializeField] private List<EnemyType> blacklist = new List<EnemyType>();

    [Header("Pond Spawn")]
    [Tooltip("If true, this point spawns a SuffocatorPond + its Suffocator group " +
             "instead of a single enemy.")]
    [SerializeField] private bool isPondSpawn = false;

    [Tooltip("Pond prefab to instantiate at this point when isPondSpawn is true. " +
             "The prefab should carry a SuffocatorPond component.")]
    [SerializeField] private GameObject suffocatorPondPrefab;

    [Header("Tuning")]
    [Tooltip("Minimum distance from the player required before this point " +
             "can be selected for a spawn.")]
    [SerializeField] private float minimumPlayerDistance = 10f;

    [Tooltip("Once used, how long before this point can be used again (seconds). " +
             "0 = no cooldown.")]
    [SerializeField] private float reuseCooldown = 5f;

    #endregion

    #region Runtime

    private float _lastUsedTime = -999f;
    private bool _permanentlyBlocked;

    public bool IsPondSpawn => isPondSpawn;
    public bool PermanentlyBlocked => _permanentlyBlocked;
    public float MinimumPlayerDistance => minimumPlayerDistance;
    public GameObject SuffocatorPondPrefab => suffocatorPondPrefab;

    #endregion

    private void OnEnable()
    {
        SpawnPoolManager.Instance?.RegisterSpawnPoint(this);
    }

    private void OnDisable()
    {
        SpawnPoolManager.Instance?.UnregisterSpawnPoint(this);
    }

    #region Public Functions.

    /// <summary>
    /// Returns true if the given enemy type is permitted to use this point.
    /// Blacklist always wins. Whitelist is inclusive if non-empty.
    /// </summary>
    public bool Permits(EnemyType type)
    {
        if (_permanentlyBlocked) return false;

        if (blacklist.Contains(type)) return false;

        if (whitelist.Count > 0 && !whitelist.Contains(type)) return false;

        return true;
    }

    /// <summary>
    /// Returns true if this point is off cooldown and far enough from the player.
    /// </summary>
    public bool IsAvailable(Vector3 playerPosition)
    {
        if (_permanentlyBlocked) return false;

        if (Time.time < _lastUsedTime + reuseCooldown) return false;

        float dist = Vector3.Distance(transform.position, playerPosition);
        if (dist < minimumPlayerDistance) return false;

        return true;
    }

    /// <summary>
    /// Called by SpawnPoolManager after a successful spawn at this point.
    /// </summary>
    public void MarkUsed()
    {
        _lastUsedTime = Time.time;
    }

    /// <summary>
    /// Permanently blocks this point. Used when a pond is destroyed —
    /// its spawn point can never be reused.
    /// </summary>
    public void BlockPermanently()
    {
        _permanentlyBlocked = true;
    }

    #endregion

    #region Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        bool hasPond = isPondSpawn;
        bool anyWhite = whitelist.Count > 0;
        bool blocked = _permanentlyBlocked;

        Gizmos.color = blocked ? Color.gray : hasPond ? new Color(0.4f, 0.8f, 1f) : anyWhite ? Color.yellow : Color.green;

        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.8f);

        if (minimumPlayerDistance > 0f)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.08f);
            Gizmos.DrawSphere(transform.position, minimumPlayerDistance);
        }
    }
#endif
    #endregion
}