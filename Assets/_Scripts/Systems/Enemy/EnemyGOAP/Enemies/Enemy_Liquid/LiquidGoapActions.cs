using Liquid.AI.GOAP;
using UnityEngine;

/// <summary>
/// All GOAP actions used by LiquidEnemy.
/// </summary>
public static class LiquidGoapActions
{
    public static GoapAction[] CreateAll()
    {
        return new GoapAction[]
        {
            new GoToPondAction(),
            new PatrolPondAction(),
            new RelaxInPondAction(),

            new InvestigateLastSeenAction(),

            new EmergeFromPondAction(),
            new ChasePlayerAction(),
            new HoldPlayerAction(),

            new AskForMergeHelpAction(),
            new RespondToMergeHelpAction(),
            new MergeIntoRequesterAction(),

            new SwallowPlayerAction(),
            new DuplicateFromPondAction()
        };
    }

    #region Actions

    #region Go To Pond.
    /// <summary>Move back to the pond center.</summary>
    private class GoToPondAction : GoapAction
    {
        public GoToPondAction()
        {
            Cost = 1f;
            requiresInRange = true;

            AddPrecondition(LiquidEnemy.WS_HAS_POND, true);
            AddEffect(LiquidEnemy.WS_IN_POND, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null || enemy.PondCenter == null)
            {
                return false;
            }

            Target = enemy.PondCenter.gameObject;
            return true;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null || enemy.PondCenter == null)
            {
                return false;
            }

            enemy.SetState(EnemyState.Roaming);

            if (!enemy.TryGoTo(enemy.PondCenter.position))
            {
                return false;
            }

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return true;
            }

            return enemy.InPond;
        }

        public override void Reset()
        {
            base.Reset();
        }
    }
    #endregion

    #region Patrol Pond
    /// <summary>
    /// Wander around inside the pond area.
    /// </summary>
    private class PatrolPondAction : GoapAction
    {
        private Vector3 _patrolTarget;
        private float _timer;
        private float _duration;

        public PatrolPondAction()
        {
            Cost = 0.9f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_IN_POND, true);
            AddPrecondition(LiquidEnemy.WS_HAS_POND, true);

            AddEffect(LiquidEnemy.WS_PATROLLED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _timer = 0f;

            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null || enemy.PondCenter == null)
            {
                return false;
            }

            _patrolTarget = enemy.GetPondPatrolPosition();
            _duration = Random.Range(enemy.PondPatrolDurationRange.x, enemy.PondPatrolDurationRange.y);
            return true;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return false;
            }

            if (!enemy.InPond)
            {
                return false;
            }

            enemy.SetState(EnemyState.Roaming);

            enemy.TryGoTo(_patrolTarget);

            _timer += Time.deltaTime;
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return true;
            }

            float dist = Vector3.Distance(enemy.transform.position, _patrolTarget);
            if (dist <= enemy.PondPatrolArriveRadius)
            {
                return true;
            }

            return _timer >= _duration;
        }

        public override void Reset()
        {
            base.Reset();
            _timer = 0f;
            _duration = 0f;
        }
    }
    #endregion

    #region Relax In Pond
    /// <summary>Idle in pond.</summary>
    private class RelaxInPondAction : GoapAction
    {
        private float _timer;

        public RelaxInPondAction()
        {
            Cost = 0.5f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_IN_POND, true);
            AddEffect(LiquidEnemy.WS_RELAXED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _timer = 0f;
            return true;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return false;
            }

            if (!enemy.InPond)
            {
                return false;
            }

            enemy.SetState(EnemyState.Resting);
            _timer += Time.deltaTime;

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return true;
            }

            return _timer >= enemy.RelaxDuration;
        }

        public override void Reset()
        {
            base.Reset();
            _timer = 0f;
        }
    }
    #endregion

    #region Investigate last seen player position.
    /// <summary>Go to the player's last known position if we recently had a sight/interest lock.</summary>
    private class InvestigateLastSeenAction : GoapAction
    {
        public InvestigateLastSeenAction()
        {
            Cost = 1.4f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_HAS_LAST_SEEN, true);
            AddEffect(LiquidEnemy.WS_AT_LAST_SEEN, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return false;
            }

            if (!enemy.HasRecentLastSeen)
            {
                return false;
            }

            return true;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return false;
            }

            if (!enemy.HasRecentLastSeen)
            {
                return false;
            }

            enemy.SetState(EnemyState.Roaming);

            return enemy.TryGoTo(enemy.LastSeenPlayerPosition);
        }

        public override bool IsDone(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return true;
            }

            if (!enemy.HasRecentLastSeen)
            {
                return true;
            }

            float d = Vector3.Distance(enemy.transform.position, enemy.LastSeenPlayerPosition);
            return d <= 1.25f;
        }
    }
    #endregion

    #region Emerge from Pond
    /// <summary>Step out of the pond when the player is interesting.</summary> 
    // TODO: Scale down when in pond, scale up when emerging.
    private class EmergeFromPondAction : GoapAction
    {
        public EmergeFromPondAction()
        {
            Cost = 1.2f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_IN_POND, true);
            AddPrecondition(LiquidEnemy.WS_PLAYER_INTERESTING, true);

            AddEffect(LiquidEnemy.WS_EMERGED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            return enemy != null;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return false;
            }

            enemy.SetState(EnemyState.Alerted);

            if (!enemy.HasPlayer)
            {
                return true;
            }

            Vector3 dir = (enemy.PlayerPosition - enemy.transform.position);
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.01f)
            {
                return true;
            }

            dir.Normalize();
            Vector3 stepTarget = enemy.transform.position + dir * 1.5f;

            enemy.TryGoTo(stepTarget);
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            return true;
        }
    }
    #endregion

    #region Chase Player
    /// <summary>Chase player by pathfinding.</summary>
    private class ChasePlayerAction : GoapAction
    {
        public ChasePlayerAction()
        {
            Cost = 2f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(LiquidEnemy.WS_PLAYER_INTERESTING, true);
            AddPrecondition(LiquidEnemy.WS_CAN_REACH_PLAYER, true);

            AddEffect(LiquidEnemy.WS_NEAR_PLAYER, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            return enemy != null && enemy.HasPlayer;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return false;
            }

            if (!enemy.HasPlayer)
            {
                return false;
            }

            enemy.SetState(EnemyState.Chasing);

            return enemy.TryGoToPlayerSmart();
        }

        public override bool IsDone(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return true;
            }

            return enemy.CanHoldPlayer;
        }
    }
    #endregion

    #region Hold Player
    /// <summary>Hold the player in place..</summary>
    private class HoldPlayerAction : GoapAction
    {
        private float _holdTimer;

        public HoldPlayerAction()
        {
            Cost = 1.5f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(LiquidEnemy.WS_CAN_HOLD, true);

            AddEffect(LiquidEnemy.WS_PLAYER_HELD, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _holdTimer = 0f;
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            return enemy != null && enemy.HasPlayer;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return false;
            }

            if (!enemy.HasPlayer)
            {
                return false;
            }

            enemy.SetState(EnemyState.Attacking);

            if (!enemy.CanHoldPlayer)
            {
                if (!enemy.TryGoToPlayerSmart())
                {
                    return false;
                }
            }
            else
            {
                enemy.SoftSnapToward(enemy.PlayerPosition, 0.8f);
            }

            _holdTimer += Time.deltaTime;
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return true;
            }

            return _holdTimer >= enemy.HoldDuration;
        }

        public override void Reset()
        {
            base.Reset();
            _holdTimer = 0f;
        }
    }
    #endregion

    #region Ask For Merge
    /// <summary>While holding player and not merged, ask for merge help.</summary>
    private class AskForMergeHelpAction : GoapAction
    {
        public AskForMergeHelpAction()
        {
            Cost = 0.8f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(LiquidEnemy.WS_PLAYER_HELD, true);
            AddPrecondition(LiquidEnemy.WS_IS_MERGED, false);
            AddPrecondition(LiquidEnemy.WS_CAN_REQUEST_MERGE, true);

            AddEffect(LiquidEnemy.WS_MERGE_REQUESTED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            return enemy != null && LiquidWorldState.Instance != null;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null || LiquidWorldState.Instance == null)
            {
                return false;
            }

            LiquidWorldState.Instance.RequestMergeHelp(enemy);
            enemy.MarkRequestedMerge();

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            return true;
        }
    }
    #endregion

    #region Respond to Merge
    /// <summary>Responder chooses requester as target.</summary>
    private class RespondToMergeHelpAction : GoapAction
    {
        public RespondToMergeHelpAction()
        {
            Cost = 0.6f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_HAS_MERGE_REQUEST, true);
            AddPrecondition(LiquidEnemy.WS_IS_MERGED, false);
            AddPrecondition(LiquidEnemy.WS_IS_BUSY, false);

            AddEffect(LiquidEnemy.WS_ACCEPTED_MERGE_REQUEST, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null || LiquidWorldState.Instance == null)
            {
                return false;
            }

            LiquidEnemy requester = LiquidWorldState.Instance.MergeRequester;
            if (requester == null || requester == enemy)
            {
                return false;
            }

            Target = requester.gameObject;
            return true;
        }

        public override bool Perform(GameObject agent)
        {
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            return true;
        }
    }
    #endregion

    #region Merge into Requester
    /// <summary>Move to requester and merge into them. Responder is destroyed. Requester becomes merged.</summary>
    private class MergeIntoRequesterAction : GoapAction
    {
        public MergeIntoRequesterAction()
        {
            Cost = 2.2f;
            requiresInRange = true;

            AddPrecondition(LiquidEnemy.WS_ACCEPTED_MERGE_REQUEST, true);
            AddPrecondition(LiquidEnemy.WS_HAS_MERGE_REQUEST, true);

            AddEffect(LiquidEnemy.WS_HELPED_MERGE, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null || LiquidWorldState.Instance == null)
            {
                return false;
            }

            LiquidEnemy requester = LiquidWorldState.Instance.MergeRequester;
            if (requester == null || requester == enemy)
            {
                return false;
            }

            if (requester.IsMerged)
            {
                return false;
            }

            Target = requester.gameObject;
            return true;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null || LiquidWorldState.Instance == null)
            {
                return false;
            }

            LiquidEnemy requester = LiquidWorldState.Instance.MergeRequester;
            if (requester == null)
            {
                return false;
            }

            enemy.SetState(EnemyState.Roaming);
            if (!enemy.TryGoTo(requester.transform.position))
            {
                return false;
            }

            if (Vector3.Distance(enemy.transform.position, requester.transform.position) <= enemy.MergeDistance)
            {
                if (!requester.IsMerged)
                {
                    requester.BecomeMerged();
                }

                LiquidWorldState.Instance.ClearMergeRequest(requester);

                Object.Destroy(enemy.gameObject);
            }

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return true;
            }

            if (Target == null)
            {
                return true;
            }

            return Vector3.Distance(enemy.transform.position, Target.transform.position) <= enemy.MergeDistance;
        }
    }
    #endregion

    #region Swallow Player
    /// <summary>Swallow player. Only possible when merged.</summary>
    private class SwallowPlayerAction : GoapAction
    {
        public SwallowPlayerAction()
        {
            Cost = 1f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(LiquidEnemy.WS_IS_MERGED, true);
            AddPrecondition(LiquidEnemy.WS_CAN_SWALLOW, true);

            AddEffect(LiquidEnemy.WS_PLAYER_SWALLOWED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            return enemy != null && enemy.HasPlayer;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return false;
            }

            if (!enemy.HasPlayer)
            {
                return false;
            }

            enemy.SetState(EnemyState.Attacking);

            if (!enemy.CanSwallowPlayer)
            {
                if (!enemy.TryGoToPlayerSmart())
                {
                    return false;
                }
            }

            if (enemy.CanSwallowPlayer)
            {
                Debug.Log("Swallowing the player.", enemy); //TODO: Actual player death.
            }

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return true;
            }

            return enemy.CanSwallowPlayer;
        }
    }
    #endregion

    #region Duplicate
    /// <summary>Duplicate only while in pond. Spawns exactly one Liquid.</summary>
    private class DuplicateFromPondAction : GoapAction
    {
        public DuplicateFromPondAction()
        {
            Cost = 2.5f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_IN_POND, true);
            AddPrecondition(LiquidEnemy.WS_CAN_DUPLICATE, true);
            AddPrecondition(LiquidEnemy.WS_PLAYER_INTERESTING, true);

            AddEffect(LiquidEnemy.WS_DUPLICATED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null)
            {
                return false;
            }

            if (enemy.LiquidPrefab == null)
            {
                return false;
            }

            if (LiquidWorldState.Instance == null)
            {
                return false;
            }

            return true;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null || enemy.LiquidPrefab == null || LiquidWorldState.Instance == null)
            {
                return false;
            }

            if (!enemy.InPond)
            {
                return false;
            }

            if (!LiquidWorldState.Instance.CanDuplicateNow())
            {
                return true;
            }

            LiquidWorldState.Instance.MarkDuplicatedNow();

            Vector3 spawnPos = enemy.GetPondSpawnPosition();
            Object.Instantiate(enemy.LiquidPrefab, spawnPos, enemy.transform.rotation);

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            return true;
        }
    }
    #endregion
    #endregion
}