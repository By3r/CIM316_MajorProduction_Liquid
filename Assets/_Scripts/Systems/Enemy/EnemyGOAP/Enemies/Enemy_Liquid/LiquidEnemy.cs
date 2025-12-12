using System.Collections.Generic;
using UnityEngine;
using Liquid.Audio;

public class LiquidEnemy : EnemyBase, INoiseListener
{
    #region Variables
    #region Pond
    [Header("Pond Settings")]
    [Tooltip("Where the centre of this Liquid’s 'pond' is in world space.")]
    [SerializeField] private Transform pondCenter;

    [Tooltip("Radius around pondCenter that counts as 'in the pond'.")]
    [SerializeField] private float pondRadius = 4f;

    [Tooltip("How long (seconds) Liquid is happy to just relax in the pond before re-evaluating.")]
    [SerializeField] private float relaxDuration = 6f;
    #endregion

    #region Player awareness
    [Header("Awareness")]
    [Tooltip("Max distance for sight checks.")]
    [SerializeField] private float sightDistance = 18f;

    [Tooltip("If player is within this distance, we treat them as 'near pond' for emerge / chase.")]
    [SerializeField] private float chaseDistance = 15f;

    [Tooltip("How long a heard noise stays 'interesting' for decision making.")]
    [SerializeField] private float noiseMemoryDuration = 3f;
    #endregion

    #region Duplication
    [Header("Duplication")]
    [Tooltip("Prefab for a duplicated Liquid (can be this same prefab).")]
    [SerializeField] private LiquidEnemy liquidPrefab;

    [Tooltip("Maximum number of clones that may exist at once.")]
    [SerializeField] private int maxDuplicates = 3;

    [Tooltip("Cooldown between duplication attempts, in seconds.")]
    [SerializeField] private float duplicateCooldown = 8f;

    [Header("Merge")]
    [Tooltip("How far we search for another Liquid to merge with.")]
    [SerializeField] private float mergeSearchRadius = 6f;

    [Tooltip("Distance at which two Liquid enemies are allowed to actually merge.")]
    [SerializeField] private float mergeDistance = 2f;

    [Tooltip("Scale multiplier applied after a successful merge.")]
    [SerializeField] private float mergeScaleMultiplier = 1.3f;
    #endregion

    #region Swallow
    [Header("Swallow Attack")]
    [Tooltip("Distance to the player required to start holding them.")]
    [SerializeField] private float holdDistance = 1.5f;

    [Tooltip("How long Liquid pins the player before trying to swallow.")]
    [SerializeField] private float holdDuration = 2.5f;

    [Tooltip("Distance at which swallow is allowed to succeed.")]
    [SerializeField] private float swallowDistance = 1.1f;
    #endregion

    private LiquidGoalType _currentGoal = LiquidGoalType.None;

    private float _relaxTimer;

    private NoiseLevel _lastNoiseLevel = NoiseLevel.Low;
    private float _lastHeardNoiseTime;
    private Vector3 _lastHeardNoisePosition;

    private float _lastDuplicateTime;

    private float _holdTimer;
    private bool _isHoldingPlayer;

    private static readonly List<LiquidEnemy> _allLiquidEnemies = new List<LiquidEnemy>();

    public LiquidGoalType CurrentLiquidGoal => _currentGoal;
    public NoiseLevel LastNoiseLevel => _lastNoiseLevel;
    public float DebugDistanceToPlayer => DistanceToPlayer;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        if (!_allLiquidEnemies.Contains(this))
        {
            _allLiquidEnemies.Add(this);
        }
    }

    private void OnEnable()
    {
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.RegisterListener(this);
        }
    }

    private void OnDisable()
    {
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.UnregisterListener(this);
        }

        _allLiquidEnemies.Remove(this);
    }

    #region Noise callback
    /// <summary>
    /// Called by NoiseManager when any noise event reaches this enemy.
    /// </summary>
    public void OnNoiseHeard(NoiseEvent noiseEvent)
    {
        _lastNoiseLevel = noiseEvent.level;
        _lastHeardNoiseTime = Time.time;
        _lastHeardNoisePosition = noiseEvent.worldPosition;
    }
    #endregion

    protected override void Tick()
    {
        EvaluateAndSetGoal();
        ExecuteCurrentGoal();
    }

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

    private bool PlayerNearPond
    {
        get
        {
            if (playerTarget == null || pondCenter == null)
            {
                return false;
            }

            float distToPond = Vector3.Distance(playerTarget.position, pondCenter.position);
            return distToPond <= chaseDistance;
        }
    }

    private bool InPond
    {
        get
        {
            if (pondCenter == null)
            {
                return false;
            }

            float dist = Vector3.Distance(transform.position, pondCenter.position);
            return dist <= pondRadius;
        }
    }

    private bool PlayerInSight
    {
        get
        {
            if (playerTarget == null)
            {
                return false;
            }

            return HasLineOfSightTo(playerTarget.position, sightDistance);
        }
    }

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

    private bool CanHoldPlayer => PlayerAlive && DistanceToPlayer <= holdDistance && DebugHasValidPath;
    private bool CanSwallowPlayer => PlayerAlive && DistanceToPlayer <= swallowDistance && DebugHasValidPath;

    private int CurrentLiquidCount => _allLiquidEnemies.Count;

    private bool EnsurePathToPlayerIfNeeded()
    {
        if (!PlayerAlive)
        {
            return false;
        }

        Vector3 targetPos = playerTarget.position;
        bool needNewPath = !DebugHasValidPath || ShouldRecalculatePath(targetPos);
        if (needNewPath)
        {
            RequestPath(targetPos);
        }

        return DebugHasValidPath;
    }


    #region GOAP selection
    private void EvaluateAndSetGoal()
    {
        LiquidGoalType previous = _currentGoal;
        _currentGoal = SelectGoal();

        if (previous != _currentGoal)
        {
            if (_currentGoal == LiquidGoalType.RelaxInPond)
            {
                _relaxTimer = 0f;
            }

            if (_currentGoal == LiquidGoalType.HoldPlayer)
            {
                _holdTimer = 0f;
                _isHoldingPlayer = true;
            }
            else
            {
                _isHoldingPlayer = false;
            }
        }
    }

    private LiquidGoalType SelectGoal()
    {
        if (!PlayerAlive)
        {
            if (!InPond)
            {
                return LiquidGoalType.GoToPond;
            }

            return LiquidGoalType.RelaxInPond;
        }

        EnsurePathToPlayerIfNeeded();

        if (CanSwallowPlayer)
        {
            return LiquidGoalType.SwallowPlayer;
        }

        if (CanHoldPlayer)
        {
            return LiquidGoalType.HoldPlayer;
        }

        bool playerInteresting = PlayerInSight || PlayerNearPond || PlayerNoiseAudible;

        if (playerInteresting)
        {
            if (InPond)
            {
                return LiquidGoalType.EmergeFromPond;
            }

            bool canDuplicate = liquidPrefab != null && CurrentLiquidCount < maxDuplicates && (Time.time - _lastDuplicateTime) > duplicateCooldown;

            if (canDuplicate)
            {
                return LiquidGoalType.Duplicate;
            }

            return LiquidGoalType.ChasePlayer;
        }

        if (!InPond)
        {
            return LiquidGoalType.GoToPond;
        }

        return LiquidGoalType.RelaxInPond;
    }
    #endregion

    #region Goal
    private void ExecuteCurrentGoal()
    {
        switch (_currentGoal)
        {
            case LiquidGoalType.GoToPond:
                DoGoToPond();
                break;

            case LiquidGoalType.RelaxInPond:
                DoRelaxInPond();
                break;

            case LiquidGoalType.EmergeFromPond:
                DoEmergeFromPond();
                break;

            case LiquidGoalType.ChasePlayer:
                DoChasePlayer();
                break;

            case LiquidGoalType.Duplicate:
                DoDuplicate();
                break;

            case LiquidGoalType.AskForMerge:
            case LiquidGoalType.LookForMergePartner:
            case LiquidGoalType.MergeWithLiquid:
                DoMergeBehaviour();
                break;

            case LiquidGoalType.HoldPlayer:
                DoHoldPlayer();
                break;

            case LiquidGoalType.SwallowPlayer:
                DoSwallowPlayer();
                break;

            case LiquidGoalType.None:
            default:
                currentState = EnemyState.Idle;
                break;
        }
    }

    private void DoGoToPond()
    {
        if (pondCenter == null)
        {
            currentState = EnemyState.Idle;
            return;
        }

        currentState = EnemyState.Moving;

        Vector3 target = pondCenter.position;
        bool needNewPath = !DebugHasValidPath || ShouldRecalculatePath(target);
        if (needNewPath)
        {
            RequestPath(target);
        }

        if (DebugHasValidPath)
        {
            FollowPath();
        }

        if (InPond)
        {
            // ........
        }
    }

    private void DoRelaxInPond()
    {
        currentState = EnemyState.Idle;
        _relaxTimer += Time.deltaTime;

        if (_relaxTimer >= relaxDuration)
        {
            _relaxTimer = 0f;
        }
    }

    private void DoEmergeFromPond()
    {
        currentState = EnemyState.Alerted;

        if (playerTarget != null)
        {
            Vector3 dir = (playerTarget.position - transform.position).normalized;
            Vector3 stepTarget = transform.position + dir * 1.5f;

            bool needNewPath = !DebugHasValidPath || ShouldRecalculatePath(stepTarget);
            if (needNewPath)
            {
                RequestPath(stepTarget);
            }

            if (DebugHasValidPath)
            {
                FollowPath();
            }
        }
    }

    private void DoChasePlayer()
    {
        if (!PlayerAlive)
        {
            return;
        }

        currentState = EnemyState.Chasing;

        Vector3 target = playerTarget.position;
        bool needNewPath = !DebugHasValidPath || ShouldRecalculatePath(target);
        if (needNewPath)
        {
            RequestPath(target);
        }

        if (DebugHasValidPath)
        {
            FollowPath();
            return;
        }

        if (PlayerNoiseAudible)
        {
            bool needNoisePath = !DebugHasValidPath || ShouldRecalculatePath(_lastHeardNoisePosition);
            if (needNoisePath)
            {
                RequestPath(_lastHeardNoisePosition);
            }

            if (DebugHasValidPath)
            {
                FollowPath();
            }
        }
    }

    private void DoDuplicate()
    {
        if (liquidPrefab == null)
        {
            return;
        }

        if ((Time.time - _lastDuplicateTime) < duplicateCooldown)
        {
            return;
        }

        if (CurrentLiquidCount >= maxDuplicates)
        {
            return;
        }

        _lastDuplicateTime = Time.time;

        Vector3 offset = Random.insideUnitSphere;
        offset.y = 0f;

        if (offset.sqrMagnitude < 0.0001f)
        {
            offset = transform.right;
        }
        else
        {
            offset.Normalize();
        }

        offset *= 2f;

        Vector3 spawnPos = transform.position + offset;
        Instantiate(liquidPrefab, spawnPos, transform.rotation);
    }

    private void DoMergeBehaviour()
    {
        LiquidEnemy best = null;
        float bestDistance = float.MaxValue;

        foreach (LiquidEnemy other in _allLiquidEnemies)
        {
            if (other == null || other == this)
            {
                continue;
            }

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist < bestDistance && dist <= mergeSearchRadius)
            {
                bestDistance = dist;
                best = other;
            }
        }

        if (best == null)
        {
            return;
        }

        currentState = EnemyState.Moving;

        Vector3 partnerPos = best.transform.position;
        bool needNewPath = !DebugHasValidPath || ShouldRecalculatePath(partnerPos);
        if (needNewPath)
        {
            RequestPath(partnerPos);
        }

        if (DebugHasValidPath)
        {
            FollowPath();
        }

        float distNow = Vector3.Distance(transform.position, best.transform.position);
        if (distNow <= mergeDistance)
        {
            transform.localScale *= mergeScaleMultiplier;
            Destroy(best.gameObject);
        }
    }

    private void DoHoldPlayer()
    {
        if (!PlayerAlive)
        {
            _isHoldingPlayer = false;
            return;
        }

        currentState = EnemyState.Attacking;

        _holdTimer += Time.deltaTime;

        Vector3 playerPos = playerTarget.position;
        Vector3 direction = (playerPos - transform.position).normalized;
        transform.position = Vector3.Lerp(transform.position, playerPos - direction * 0.8f, Time.deltaTime * 3f);

        // TODO: call into a PlayerController actually restrict movement.

        if (_holdTimer >= holdDuration)
        {
            _isHoldingPlayer = false;
        }
    }

    private void DoSwallowPlayer()
    {
        if (!PlayerAlive)
        {
            return;
        }

        currentState = EnemyState.Attacking;

        Debug.Log("[LiquidEnemy] Swallowing the player.", this);

        // TODO:
        // PlayerHealth hp = playerTarget.GetComponent<PlayerHealth>();
        // if (hp != null) hp.Kill();
        _currentGoal = LiquidGoalType.GoToPond;
    }
    #endregion
}