using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Liquid.Audio;
using TMPro;

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

    [Tooltip("How long after hearing a noise we still consider it 'audible' for GOAP decisions.")]
    [SerializeField] private float noiseMemoryDuration = 3f;

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

    private NoiseLevel _lastNoiseLevel = NoiseLevel.Low;
    private float _lastHeardNoiseTime;
    private Vector3 _lastHeardNoisePosition;

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

            return HasLineOfSightTo(playerTarget.position, sightDistance);
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
            _currentGoal == EnemyGoalType.ChasePlayer)
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
            return EnemyGoalType.TakeRest;
        }

        if (!PlayerAlive)
        {
            if (HasAnyPatrolPoint)
            {
                return EnemyGoalType.PatrolArea;
            }

            return EnemyGoalType.None;
        }

        bool canSeePlayer = PlayerInSight;
        bool mediumNoiseAndAudible = PlayerNoiseIsMedium && PlayerNoiseAudible;

        if (CanKillPlayer && HasStamina)
        {
            return EnemyGoalType.KillPlayer;
        }

        if (canSeePlayer)
        {
            if (DebugHasValidPath && !CanKillPlayer)
            {
                return EnemyGoalType.ChasePlayer;
            }

            if (!DebugHasValidPath)
            {
                return EnemyGoalType.ThreatenPlayer;
            }
        }

        if (mediumNoiseAndAudible)
        {
            return EnemyGoalType.ThreatenPlayer;
        }

        if (HasAnyPatrolPoint)
        {
            return EnemyGoalType.PatrolArea;
        }

        return EnemyGoalType.None;
    }

    private void OnGoalChanged(EnemyGoalType previousGoal, EnemyGoalType newGoal)
    {
        if (newGoal == EnemyGoalType.PatrolArea && HasAnyPatrolPoint)
        {
            _patrolIndex = FindNearestPatrolPointIndex(transform.position);
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
        SetState(EnemyState.Moving);

        if (!PlayerAlive)
        {
            return;
        }

        if (!HasStamina)
        {
            return;
        }

        Vector3 targetPos = playerTarget.position;

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

        sb.AppendLine($"Name: {name})");
        sb.AppendLine($"State: {CurrentState}");
        sb.AppendLine($"Goal: {CurrentGoal}");
        sb.AppendLine($"Stamina: {_stamina:F1}/{staminaMax:F1}  HasStamina={HasStamina}");
        sb.AppendLine($"PlayerDist: {DebugDistanceToPlayer:F2}  InSight={PlayerInSight}  PathValid={DebugHasValidPath}");
        sb.AppendLine($"Noise: {_lastNoiseLevel}  Audible={PlayerNoiseAudible}");
        sb.AppendLine($"Decision: {_debugLastDecisionReason}");

        return sb.ToString();
    }

    #endregion
}