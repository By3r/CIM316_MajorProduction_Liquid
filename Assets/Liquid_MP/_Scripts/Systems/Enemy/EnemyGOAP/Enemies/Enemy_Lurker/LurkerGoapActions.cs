using Liquid.AI.GOAP;
using UnityEngine;
using _Scripts.Systems.Inventory;

/// <summary>
/// All GOAP actions used by LurkerEnemy.
/// Reactive-light implementation (#3).
/// </summary>
public static class LurkerGoapActions
{
    public static GoapAction[] CreateAll()
    {
        return new GoapAction[]
        {
            new FreakOutInLightAction(),
            new RetreatFromLightAction(),

            new GetCloseToPlayerAction(),
            new StareAction(),
            new StealItemsAction(),

            new EscapeAfterStealAction(),
            new DisappearNowAction(),

            new LurkAction()
        };
    }

    #region Actions

    private class FreakOutInLightAction : GoapAction
    {
        private float _timer;

        public FreakOutInLightAction()
        {
            Cost = 0.2f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, true);
            AddPrecondition(LurkerEnemy.WS_FREAKED_OUT, false);

            AddEffect(LurkerEnemy.WS_FREAKED_OUT, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _timer = 0f;
            return agent.GetComponent<LurkerEnemy>() != null;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Threatening);
            enemy.SetFreakedOut(true);

            _timer += Time.deltaTime;
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return true;

            return _timer >= enemy.FreakOutDuration;
        }

        public override void Reset()
        {
            base.Reset();
            _timer = 0f;
        }
    }

    /// <summary>
    /// Runs to the retreat target computed by the enemy (away from light source),
    /// and completes once WS_IN_BRIGHT_LIGHT becomes false.
    /// </summary>
    private class RetreatFromLightAction : GoapAction
    {
        public RetreatFromLightAction()
        {
            Cost = 1.0f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, true);
            AddPrecondition(LurkerEnemy.WS_FREAKED_OUT, true);

            AddEffect(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            return enemy.HasRetreatTarget;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Chasing);

            // Move toward retreat target.
            return enemy.TryGoTo(enemy.RetreatTarget);
        }

        public override bool IsDone(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return true;

            // Done when no longer considered in bright light.
            return !enemy.InBrightLight;
        }
    }

    private class GetCloseToPlayerAction : GoapAction
    {
        public GetCloseToPlayerAction()
        {
            Cost = 1.2f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(LurkerEnemy.WS_PLAYER_IS_AWARE, false);
            AddPrecondition(LurkerEnemy.WS_PLAYER_IS_QUIET, true);
            AddPrecondition(LurkerEnemy.WS_PLAYER_IN_RANGE, true);
            AddPrecondition(LurkerEnemy.WS_CAN_SEE_PLAYER, true);
            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);

            AddEffect(LurkerEnemy.WS_CLOSE_TO_PLAYER, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null || !enemy.HasPlayer) return false;

            Target = enemy.PlayerTarget != null ? enemy.PlayerTarget.gameObject : null;
            return Target != null;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null || !enemy.HasPlayer) return false;

            enemy.SetState(EnemyState.Moving);
            return enemy.TryGoTo(enemy.PlayerPosition);
        }

        public override bool IsDone(GameObject agent) => true;
    }

    private class StareAction : GoapAction
    {
        private float _timer;

        public StareAction()
        {
            Cost = 0.6f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_CLOSE_TO_PLAYER, true);
            AddPrecondition(LurkerEnemy.WS_PLAYER_IS_AWARE, false);
            AddPrecondition(LurkerEnemy.WS_CAN_SEE_PLAYER, true);
            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);

            AddEffect(LurkerEnemy.WS_OBSERVED_PLAYER, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _timer = 0f;
            return agent.GetComponent<LurkerEnemy>() != null;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Alerted);
            enemy.MarkObserved();

            _timer += Time.deltaTime;
            return true;
        }

        public override bool IsDone(GameObject agent) => _timer >= 0.75f;

        public override void Reset()
        {
            base.Reset();
            _timer = 0f;
        }
    }

    private class StealItemsAction : GoapAction
    {
        public StealItemsAction()
        {
            Cost = 0.8f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_CLOSE_TO_PLAYER, true);
            AddPrecondition(LurkerEnemy.WS_WITHIN_STEAL_RANGE, true);
            AddPrecondition(LurkerEnemy.WS_PLAYER_HAS_ITEMS, true);
            AddPrecondition(LurkerEnemy.WS_CAN_SEE_PLAYER, true);
            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);

            AddEffect(LurkerEnemy.WS_HAS_STOLEN, true);
            AddEffect(LurkerEnemy.WS_PLAYER_IS_AWARE, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            return agent.GetComponent<LurkerEnemy>() != null;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;
            if (PlayerInventory.Instance == null) return false;

            int slotCount = PlayerInventory.Instance.SlotCount;

            System.Collections.Generic.List<int> candidates = new System.Collections.Generic.List<int>(slotCount);
            for (int i = 0; i < slotCount; i++)
            {
                var slot = PlayerInventory.Instance.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                {
                    candidates.Add(i);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            int pick = candidates[Random.Range(0, candidates.Count)];
            var removed = PlayerInventory.Instance.RemoveItemFromSlot(pick, 1);
            if (removed == null) return false;

            enemy.SetState(EnemyState.Attacking);
            enemy.MarkStolen();

            return true;
        }

        public override bool IsDone(GameObject agent) => true;
    }

    private class EscapeAfterStealAction : GoapAction
    {
        public EscapeAfterStealAction()
        {
            Cost = 0.7f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_PLAYER_IS_AWARE, true);
            AddPrecondition(LurkerEnemy.WS_HAS_STOLEN, true);
            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);

            AddEffect(LurkerEnemy.WS_HIDING, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            return agent.GetComponent<LurkerEnemy>() != null;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Alerted);
            enemy.SetHidden(true);
            return true;
        }

        public override bool IsDone(GameObject agent) => true;
    }

    private class DisappearNowAction : GoapAction
    {
        public DisappearNowAction()
        {
            Cost = 1.0f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_PLAYER_IS_AWARE, true);
            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);

            AddEffect(LurkerEnemy.WS_HIDING, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            return agent.GetComponent<LurkerEnemy>() != null;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Alerted);
            enemy.SetHidden(true);
            return true;
        }

        public override bool IsDone(GameObject agent) => true;
    }

    private class LurkAction : GoapAction
    {
        private float _timer;
        private float _duration;

        public LurkAction()
        {
            Cost = 1.1f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);
            AddEffect(LurkerEnemy.WS_REPOSITIONED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _timer = 0f;
            _duration = Random.Range(1.0f, 2.5f);
            return agent.GetComponent<LurkerEnemy>() != null;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Roaming);
            enemy.MarkRepositioned();

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