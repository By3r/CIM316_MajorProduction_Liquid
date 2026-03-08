using System.Collections.Generic;
using Liquid.AI.GOAP;
using UnityEngine;

/// <summary>
/// All GOAP actions for the Liquid enemy.
/// </summary>
public static class LiquidGoapActions
{
    public static GoapAction[] CreateAll()
    {
        return new GoapAction[]
        {
            new KillAction(),
            new PursueAction(),
            new ReorientAction(),
            new LeaveAction(),
        };
    }

    #region Kill.
    private class KillAction : GoapAction
    {
        public KillAction() : base("KillAction")
        {
            BaseCost = 0.1f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(LiquidEnemy.WS_PLAYER_IN_RANGE, true);

            AddEffect(LiquidEnemy.WS_PLAYER_IN_RANGE, false);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            return enemy != null && enemy.PlayerInRange;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null || !enemy.PlayerInRange) return false;

            enemy.SetState(EnemyState.Attacking);
            enemy.SetDebugActionName(ActionName);

            // TODO: route through player damage/death system
            // PlayerHealthController.Instance?.InstantKill();
            return true;
        }

        public override bool IsDone(GameObject agent) => true;
    }
    #endregion

    #region Pursue.
    private class PursueAction : GoapAction
    {
        public PursueAction() : base("PursueAction")
        {
            BaseCost = 1.0f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(LiquidEnemy.WS_SHOULD_LEAVE, false);
            AddPrecondition(LiquidEnemy.WS_LUNGE_BLOCKED, false);

            AddEffect(LiquidEnemy.WS_PURSUING, true);
            AddEffect(LiquidEnemy.WS_PLAYER_IN_RANGE, true);
        }

        public override float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost;
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null || !enemy.HasPlayer) return false;

            enemy.ChooseLungeAxis();
            return true;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Chasing);
            enemy.SetDebugActionName(ActionName);

            bool stillLunging = enemy.TickLunge();

            if (!stillLunging && enemy.LungeBlocked)
                return false;

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null) return true;

            return enemy.PlayerInRange || enemy.LungeBlocked;
        }
    }
    #endregion

    #region Reorient.
    private class ReorientAction : GoapAction
    {
        public ReorientAction() : base("ReorientAction")
        {
            BaseCost = 0.6f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_LUNGE_BLOCKED, true);
            AddPrecondition(LiquidEnemy.WS_SHOULD_LEAVE, false);

            AddEffect(LiquidEnemy.WS_LUNGE_BLOCKED, false);
            AddEffect(LiquidEnemy.WS_PURSUING, true);
        }

        public override float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost;
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            return enemy != null && enemy.HasPlayer;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Alerted);
            enemy.SetDebugActionName(ActionName);

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null) return true;

            if (!enemy.ReorientComplete) return false;

            if (enemy.HasPlayer)
                enemy.ChooseLungeAxis();

            return true;
        }
    }
    #endregion

    #region Leave.
    private class LeaveAction : GoapAction
    {
        private float _timer;

        public LeaveAction() : base("LeaveAction")
        {
            BaseCost = 1.0f;
            requiresInRange = false;

            AddPrecondition(LiquidEnemy.WS_SHOULD_LEAVE, true);

            AddEffect(LiquidEnemy.WS_LEFT, true);
        }

        public override float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost;
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _timer = 0f;
            return agent.GetComponent<LiquidEnemy>() != null;
        }

        public override bool Perform(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Roaming);
            enemy.SetDebugActionName(ActionName);

            _timer += Time.deltaTime;
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LiquidEnemy enemy = agent.GetComponent<LiquidEnemy>();
            if (enemy == null) return true;

            if (_timer < enemy.GetLeaveCost()) return false;

            enemy.MarkLeft();
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            _timer = 0f;
        }
    }
    #endregion
}