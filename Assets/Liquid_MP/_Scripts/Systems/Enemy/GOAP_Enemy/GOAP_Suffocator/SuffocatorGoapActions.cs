using System.Collections.Generic;
using Liquid.AI.GOAP;
using UnityEngine;

public static class SuffocatorGoapActions
{
    public static GoapAction[] CreateAll()
    {
        return new GoapAction[]
        {
            new SleepOnPondAction(),
            new SleepNearPondAction(),
            new ReturnToPondAction(),

            new CallForHelpAction(),
            new MergeAction(),

            new ChasePlayerAction(),
            new HoldPlayerAction(),
            new SwallowPlayerAction(),

            new InvestigateLastSeenAction(),
            new RoamAction(),
        };
    }

    #region Sleep on pond.
    private class SleepOnPondAction : GoapAction
    {
        public SleepOnPondAction() : base("SleepOnPondAction")
        {
            BaseCost = 1.0f;
            requiresInRange = false;

            AddPrecondition(SuffocatorEnemy.WS_POND_ALIVE, true);
            AddPrecondition(SuffocatorEnemy.WS_ON_POND, true);
            AddPrecondition(SuffocatorEnemy.WS_SLEEPY, true);

            AddEffect(SuffocatorEnemy.WS_SLEEPING, true);
        }

        public override float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost;
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy != null && enemy.OnPond && enemy.PondAlive;
        }

        public override bool Perform(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null) return false;

            enemy.SetDebugActionName(ActionName);
            enemy.BeginSleep();
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy == null || !enemy.IsHoldingPlayer && enemy.Sleepiness <= 0f;
        }
    }
    #endregion

    #region Sleep near pond.
    private class SleepNearPondAction : GoapAction
    {
        public SleepNearPondAction() : base("SleepNearPondAction")
        {
            BaseCost = 1.4f;
            requiresInRange = false;

            AddPrecondition(SuffocatorEnemy.WS_POND_ALIVE, true);
            AddPrecondition(SuffocatorEnemy.WS_NEAR_POND, true);
            AddPrecondition(SuffocatorEnemy.WS_SLEEPY, true);

            AddEffect(SuffocatorEnemy.WS_SLEEPING, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy != null && enemy.NearPond && enemy.PondAlive;
        }

        public override bool Perform(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null) return false;

            enemy.SetDebugActionName(ActionName);
            enemy.BeginSleep();
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy == null || enemy.Sleepiness <= 0f;
        }
    }
    #endregion

    #region Return to pond.
    private class ReturnToPondAction : GoapAction
    {
        public ReturnToPondAction() : base("ReturnToPondAction")
        {
            BaseCost = 1.2f;
            requiresInRange = false;

            AddPrecondition(SuffocatorEnemy.WS_POND_ALIVE, true);
            AddPrecondition(SuffocatorEnemy.WS_SLEEPY, true);

            AddEffect(SuffocatorEnemy.WS_ON_POND, true);
            AddEffect(SuffocatorEnemy.WS_NEAR_POND, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy != null && enemy.PondAlive;
        }

        public override bool Perform(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Moving);
            enemy.SetDebugActionName(ActionName);
            enemy.TryGoTo(enemy.PondPosition, this);
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy == null || enemy.NearPond;
        }
    }
    #endregion

    #region Call for help.
    private class CallForHelpAction : GoapAction
    {
        public CallForHelpAction() : base("CallForHelpAction")
        {
            BaseCost = 1.5f;
            requiresInRange = false;

            AddPrecondition(SuffocatorEnemy.WS_PLAYER_INTERESTING, true);
            AddPrecondition(SuffocatorEnemy.WS_CALLED_FOR_HELP, false);

            AddEffect(SuffocatorEnemy.WS_CALLED_FOR_HELP, true);
        }

        public override float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost;
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy != null && !enemy.CalledForHelp;
        }

        public override bool Perform(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Alerted);
            enemy.SetDebugActionName(ActionName);

            Collider[] hits = Physics.OverlapSphere(enemy.transform.position, enemy.CallForHelpRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                SuffocatorEnemy peer = hits[i].GetComponent<SuffocatorEnemy>();
                if (peer == null || peer == enemy || !peer.PondAlive) continue;
                // TODO: wire to NoiseManager.EmitNoise to alert peers via the noise system
                // NoiseManager.Instance?.EmitNoise(enemy.transform.position, NoiseLevel.Maximum, ...);
            }

            enemy.MarkCalledForHelp();
            return true;
        }

        public override bool IsDone(GameObject agent) => true;
    }
    #endregion

    #region Merge.
    private class MergeAction : GoapAction
    {
        private SuffocatorEnemy _mergeTarget;

        public MergeAction() : base("MergeAction")
        {
            BaseCost = 0.8f;
            requiresInRange = false;

            AddPrecondition(SuffocatorEnemy.WS_CAN_MERGE, true);
            AddPrecondition(SuffocatorEnemy.WS_IS_MERGED, false);
            AddPrecondition(SuffocatorEnemy.WS_PLAYER_INTERESTING, true);

            AddEffect(SuffocatorEnemy.WS_IS_MERGED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null) return false;

            _mergeTarget = FindMergeTarget(enemy);
            return _mergeTarget != null;
        }

        public override bool Perform(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null || _mergeTarget == null) return false;

            float dist = Vector3.Distance(enemy.transform.position, _mergeTarget.transform.position);
            if (dist > enemy.MergeRange)
            {
                enemy.SetState(EnemyState.Moving);
                enemy.SetDebugActionName(ActionName);
                enemy.TryGoTo(_mergeTarget.transform.position, this);
                return true;
            }

            enemy.SetState(EnemyState.Attacking);
            enemy.SetDebugActionName(ActionName);
            enemy.BeginMerge(_mergeTarget);
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy == null || enemy.IsMerged;
        }

        public override void Reset()
        {
            base.Reset();
            _mergeTarget = null;
        }

        private SuffocatorEnemy FindMergeTarget(SuffocatorEnemy self)
        {
            Collider[] hits = Physics.OverlapSphere(self.transform.position, self.MergeRange * 3f);
            SuffocatorEnemy best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                SuffocatorEnemy peer = hits[i].GetComponent<SuffocatorEnemy>();
                if (peer == null || peer == self || !peer.PondAlive) continue;

                float d = Vector3.Distance(self.transform.position, peer.transform.position);
                if (d < bestDist) { bestDist = d; best = peer; }
            }

            return best;
        }
    }
    #endregion

    #region Chase player.
    private class ChasePlayerAction : GoapAction
    {
        public ChasePlayerAction() : base("ChasePlayerAction")
        {
            BaseCost = 2.0f;
            requiresInRange = false;

            AddPrecondition(SuffocatorEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(SuffocatorEnemy.WS_PLAYER_INTERESTING, true);
            AddPrecondition(SuffocatorEnemy.WS_CAN_REACH_PLAYER, true);

            AddEffect(SuffocatorEnemy.WS_CAN_HOLD_PLAYER, true);
        }

        public override float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost;
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy != null && enemy.HasPlayer && enemy.CanReachPlayer;
        }

        public override bool Perform(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null || !enemy.HasPlayer) return false;

            enemy.SetState(EnemyState.Chasing);
            enemy.SetDebugActionName(ActionName);
            enemy.SetLastPathWasPlayer(true);

            bool moved = enemy.TryGoTo(enemy.PlayerPosition, this);
            if (!moved) enemy.MarkPlayerUnreachable();

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy == null || enemy.CanHoldPlayer;
        }
    }
    #endregion

    #region Hold player.
    private class HoldPlayerAction : GoapAction
    {
        public HoldPlayerAction() : base("HoldPlayerAction")
        {
            BaseCost = 1.0f;
            requiresInRange = false;

            AddPrecondition(SuffocatorEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(SuffocatorEnemy.WS_PLAYER_INTERESTING, true);
            AddPrecondition(SuffocatorEnemy.WS_CAN_HOLD_PLAYER, true);
            AddPrecondition(SuffocatorEnemy.WS_HOLDING_PLAYER, false);

            AddEffect(SuffocatorEnemy.WS_HOLDING_PLAYER, true);
        }

        public override float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost;
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy != null && enemy.CanHoldPlayer;
        }

        public override bool Perform(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null) return false;

            if (!enemy.IsHoldingPlayer)
            {
                enemy.SetState(EnemyState.Attacking);
                enemy.SetDebugActionName(ActionName);
                enemy.BeginHoldPlayer();
            }

            enemy.TickHold();
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy == null || !enemy.IsHoldingPlayer;
        }
    }
    #endregion

    #region Swallow player.
    private class SwallowPlayerAction : GoapAction
    {
        public SwallowPlayerAction() : base("SwallowPlayerAction")
        {
            BaseCost = 1.2f;
            requiresInRange = false;

            AddPrecondition(SuffocatorEnemy.WS_IS_MERGED, true);
            AddPrecondition(SuffocatorEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(SuffocatorEnemy.WS_PLAYER_INTERESTING, true);
            AddPrecondition(SuffocatorEnemy.WS_CAN_HOLD_PLAYER, true);

            AddEffect(SuffocatorEnemy.WS_HOLDING_PLAYER, true);
        }

        public override float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost;
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy != null && enemy.IsMerged && enemy.CanHoldPlayer;
        }

        public override bool Perform(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Attacking);
            enemy.SetDebugActionName(ActionName);

            if (!enemy.IsHoldingPlayer)
                enemy.BeginHoldPlayer();

            enemy.TickHold();

            // TODO: pass-through / swallow logic — player movement locked, look free,
            //       player spams space to escape. Wire to PlayerMovementController.
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy == null || !enemy.IsHoldingPlayer;
        }
    }
    #endregion

    #region Investigate last seen.
    private class InvestigateLastSeenAction : GoapAction
    {
        public InvestigateLastSeenAction() : base("InvestigateLastSeenAction")
        {
            BaseCost = 1.2f;
            requiresInRange = false;

            AddPrecondition(SuffocatorEnemy.WS_HAS_LAST_SEEN, true);
            AddEffect(SuffocatorEnemy.WS_AT_LAST_SEEN, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            return enemy != null && enemy.HasRecentLastSeen;
        }

        public override bool Perform(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null || !enemy.HasRecentLastSeen) return false;

            enemy.SetState(EnemyState.Roaming);
            enemy.SetDebugActionName(ActionName);
            enemy.TryGoTo(enemy.LastSeenPlayerPosition, this);
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null || !enemy.HasRecentLastSeen) return true;

            return Vector3.Distance(enemy.transform.position, enemy.LastSeenPlayerPosition)
                   <= enemy.LastSeenArriveRadius;
        }
    }
    #endregion

    #region Roam.
    private class RoamAction : GoapAction
    {
        private Vector3 _roamTarget;
        private float _timer;
        private float _duration;

        public RoamAction() : base("RoamAction")
        {
            BaseCost = 1.6f;
            requiresInRange = false;

            AddEffect(SuffocatorEnemy.WS_PATROLLED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _timer = 0f;

            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null) return false;

            _duration = Random.Range(enemy.RoamDurationRange.x, enemy.RoamDurationRange.y);
            _roamTarget = enemy.transform.position;

            for (int i = 0; i < enemy.RoamMaxRetries; i++)
            {
                Vector3 candidate = enemy.GetRoamPoint();

                if (GridPathfinder.Instance != null)
                {
                    var test = GridPathfinder.Instance.FindPath(
                        enemy.transform.position, candidate, default);
                    if (test != null && test.Count > 0) { _roamTarget = candidate; return true; }
                }
                else { _roamTarget = candidate; return true; }
            }

            return true;
        }

        public override bool Perform(GameObject agent)
        {
            SuffocatorEnemy enemy = agent.GetComponent<SuffocatorEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Roaming);
            enemy.SetDebugActionName(ActionName);
            enemy.TryGoTo(_roamTarget, this);
            _timer += Time.deltaTime;
            return true;
        }

        public override bool IsDone(GameObject agent) => _timer >= _duration;

        public override void Reset()
        {
            base.Reset();
            _timer = 0f;
            _duration = 0f;
        }
    }
    #endregion
}