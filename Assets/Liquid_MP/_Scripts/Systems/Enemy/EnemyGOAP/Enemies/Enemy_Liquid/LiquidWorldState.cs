using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global world state shared by all Liquid enemies.
/// </summary>
public class LiquidWorldState : MonoBehaviour
{
    public static LiquidWorldState Instance { get; private set; }
    #region Variables
    [Header("Duplication")]
    [Tooltip("Maximum number of Liquid enemies allowed at once for this pond.")]
    [SerializeField] private int maxLiquidCount = 10;

    [Tooltip("Global cooldown between duplication eventsto prevent spam spawn across all Liquids.")]
    [SerializeField] private float duplicateCooldownSeconds = 6f;

    [Header("Merge Help")]
    [Tooltip("How long a merge request stays valid for.")]
    [SerializeField] private float mergeRequestTimeoutSeconds = 6f;

    private readonly List<LiquidEnemy> _allLiquids = new List<LiquidEnemy>();

    private float _lastDuplicateTime;

    private LiquidEnemy _mergeRequester;
    private float _mergeRequestTime;

    public int CurrentLiquidCount => _allLiquids.Count;
    public int MaxLiquidCount => maxLiquidCount;

    public bool HasMergeRequest => _mergeRequester != null;
    public LiquidEnemy MergeRequester => _mergeRequester;
    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple instances found. Destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #region Registration
    public void Register(LiquidEnemy liquid)
    {
        if (liquid == null)
        {
            return;
        }

        if (!_allLiquids.Contains(liquid))
        {
            _allLiquids.Add(liquid);
        }
    }

    public void Unregister(LiquidEnemy liquid)
    {
        if (liquid == null)
        {
            return;
        }

        _allLiquids.Remove(liquid);

        if (_mergeRequester == liquid)
        {
            ClearMergeRequest(liquid);
        }
    }

    #endregion

    #region Duplication
    public bool CanDuplicateNow()
    {
        if (CurrentLiquidCount >= maxLiquidCount)
        {
            return false;
        }

        return (Time.time - _lastDuplicateTime) >= duplicateCooldownSeconds;
    }

    public void MarkDuplicatedNow()
    {
        _lastDuplicateTime = Time.time;
    }

    #endregion

    #region Merge Help
    public bool CanRequestMerge(LiquidEnemy requester)
    {
        if (requester == null)
        {
            return false;
        }

        if (_mergeRequester != null && _mergeRequester != requester)
        {
            return false;
        }

        return true;
    }

    public void RequestMergeHelp(LiquidEnemy requester)
    {
        if (requester == null)
        {
            return;
        }

        if (!CanRequestMerge(requester))
        {
            return;
        }

        _mergeRequester = requester;
        _mergeRequestTime = Time.time;
    }

    public void ClearMergeRequest(LiquidEnemy requester)
    {
        if (_mergeRequester != requester)
        {
            return;
        }

        _mergeRequester = null;
        _mergeRequestTime = 0f;
    }

    public bool IsMergeRequestExpired()
    {
        if (_mergeRequester == null)
        {
            return true;
        }

        return (Time.time - _mergeRequestTime) > mergeRequestTimeoutSeconds;
    }

    private void Update()
    {
        if (_mergeRequester != null && IsMergeRequestExpired())
        {
            _mergeRequester = null;
            _mergeRequestTime = 0f;
        }
    }
    #endregion
}