using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a mineral extraction pond. Spawned by SpawnPoolManager at a
/// pond SpawnPoint. Owns the SuffocatorEnemy group tied to this site.
/// </summary>
public class SuffocatorPond : MonoBehaviour
{
    #region Variables.
    [Header("Group")]
    [Tooltip("How many Suffocators this pond supports (default 6).")]
    [SerializeField] private int groupSize = 6;

    [Header("Extraction Site")]
    [Tooltip("Extraction site transform in this room. Suffocators use this to detect player extraction.")]
    [SerializeField] private Transform extractionSiteTransform;

    private readonly List<SuffocatorEnemy> _group = new List<SuffocatorEnemy>(6);
    private bool _isDead;
    private SpawnPoint _ownerSpawnPoint;

    public bool IsDead => _isDead;
    public int GroupSize => groupSize;
    public Transform ExtractionSite => extractionSiteTransform;
    public Transform PondTransform => transform;
    public IReadOnlyList<SuffocatorEnemy> Group => _group;
    #endregion

    #region Setup.
    /// <summary>
    /// Called by SpawnPoolManager immediately after spawning this pond
    /// to wire up the owning spawn point and enemy group.
    /// </summary>
    public void Initialise(SpawnPoint owner, List<SuffocatorEnemy> group)
    {
        _ownerSpawnPoint = owner;
        _group.Clear();
        _group.AddRange(group);
    }

    /// <summary>
    /// Called by SpawnPoolManager when it respawns a dead Suffocator
    /// back into this pond's group.
    /// </summary>
    public void RegisterEnemy(SuffocatorEnemy enemy)
    {
        if (!_group.Contains(enemy))
            _group.Add(enemy);
    }

    public void UnregisterEnemy(SuffocatorEnemy enemy)
    {
        _group.Remove(enemy);
    }
    #endregion

    #region Pond death.
    /// <summary>
    /// Called when the extraction is complete or the pond is otherwise destroyed.
    /// All living Suffocators die instantly; the spawn point is permanently blocked.
    /// </summary>
    public void DestroyPond()
    {
        if (_isDead) return;
        _isDead = true;

        for (int i = _group.Count - 1; i >= 0; i--)
        {
            SuffocatorEnemy s = _group[i];
            if (s != null && !s.IsDead)
                s.NotifyPondDead();
        }

        _group.Clear();

        _ownerSpawnPoint?.BlockPermanently();

        gameObject.SetActive(false);
    }
    #endregion

    #region Alive enemy count.
    public int CountAlive()
    {
        int count = 0;
        for (int i = 0; i < _group.Count; i++)
        {
            if (_group[i] != null && !_group[i].IsDead) count++;
        }
        return count;
    }
    #endregion
}