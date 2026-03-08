using System.Collections.Generic;
using Liquid.AI.GOAP;
using _Scripts.Systems.Inventory;
using UnityEngine;

/// <summary>
/// All GOAP actions for the Lurker enemy.
/// </summary>
public static class LurkerGoapActions
{
    public static GoapAction[] CreateAll()
    {
        return new GoapAction[]
        {
            // Light escape.
            new FreakOutInLightAction(),
            new RetreatFromLightAction(),

            // Flashlight counter.
            new FlashlightPanicAction(),
            
            // Steal sequence.
            new GetCloseToPlayerAction(),
            new JumpscareAction(),
            new StealItemsAction(),

            // Post-steal / spotted responses.
            new EscapeAfterStealAction(),
            new DisappearNowAction(),

            // Idle behaviours.
            new ReappearAction(),
            new StareAction(),
            new LurkAction(),
        };
    }

    #region Freakout in light Action.
    private class FreakOutInLightAction : GoapAction
    {
        private float _timer;

        public FreakOutInLightAction() : base("FreakOutInLightAction")
        {
            BaseCost = 0.2f;
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
            enemy.SetDebugActionName(ActionName);
            enemy.SetFreakedOut(true);
            _timer += Time.deltaTime;
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            return enemy == null || _timer >= enemy.FreakOutDuration;
        }

        public override void Reset()
        {
            base.Reset();
            _timer = 0f;
        }
    }
    #endregion

    #region Retreat from light Action.
    private class RetreatFromLightAction : GoapAction
    {
        public RetreatFromLightAction() : base("RetreatFromLightAction")
        {
            BaseCost = 1.0f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, true);
            AddPrecondition(LurkerEnemy.WS_FREAKED_OUT, true);
            AddEffect(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            return enemy != null && enemy.HasRetreatTarget;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Chasing);
            enemy.SetDebugActionName(ActionName);
            enemy.TryGoTo(enemy.RetreatTarget, this);

            if (!enemy.InBrightLight)
                enemy.SetFreakedOut(false);

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            return enemy == null || !enemy.InBrightLight;
        }
    }
    #endregion

    #region Flashlight Panic Action.
    /// <summary>
    /// When the flashlight shines on the Lurker, it panics and attempts to
    /// disable/flicker the flashlight. It also retreats.
    ///
    /// Player counter: unequip flashlight before shutoff to prevent disable.
    /// Flashlight also damages the Lurker — enough hits kill it.
    /// Jumpscare still triggers if Lurker is already close when flashlight is shone
    /// (jumpscare counters the steal, not the scare).
    /// </summary>
    private class FlashlightPanicAction : GoapAction
    {
        public FlashlightPanicAction() : base("FlashlightPanicAction")
        {
            BaseCost = 0.3f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_FLASHLIGHT_ON_ME, true);
            AddPrecondition(LurkerEnemy.WS_FLASHLIGHT_PANICKING, false);
            AddEffect(LurkerEnemy.WS_FLASHLIGHT_PANICKING, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
            => agent.GetComponent<LurkerEnemy>() != null;

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Threatening);
            enemy.SetDebugActionName(ActionName);
            enemy.BeginFlashlightPanic();
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            return enemy == null || enemy.FlashlightPanicking;
        }
    }
    #endregion

    #region Get close to Player Action.
    /// <summary>
    /// Approaches the player unseen.
    /// </summary>
    private class GetCloseToPlayerAction : GoapAction
    {
        public GetCloseToPlayerAction() : base("GetCloseToPlayerAction")
        {
            BaseCost = 1.2f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(LurkerEnemy.WS_PLAYER_IS_AWARE, false);
            AddPrecondition(LurkerEnemy.WS_PLAYER_IS_QUIET, true);
            AddPrecondition(LurkerEnemy.WS_PLAYER_IN_RANGE, true);
            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);
            AddPrecondition(LurkerEnemy.WS_HIDING, false);

            AddEffect(LurkerEnemy.WS_CLOSE_TO_PLAYER, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            return enemy != null && enemy.HasPlayer;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null || !enemy.HasPlayer) return false;

            enemy.SetState(EnemyState.Moving);
            enemy.SetDebugActionName(ActionName);
            enemy.TryGoTo(enemy.PlayerPosition, this);
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null || !enemy.HasPlayer) return true;
            return Vector3.Distance(enemy.transform.position, enemy.PlayerPosition)
                   <= enemy.CloseToPlayerDistance;
        }
    }
    #endregion

    #region Jumpscare Action.
    /// <summary>
    /// Uninterruptible jumpscare sequence. Triggers once close enough.
    /// The jumpscare fires regardless of whether the player shines a flashlight
    /// after the Lurker is already within jumpscareRange.
    /// (Flashlight counters the STEAL, not the scare.)
    /// </summary>
    private class JumpscareAction : GoapAction
    {
        private float _timer;

        public JumpscareAction() : base("JumpscareAction")
        {
            BaseCost = 0.4f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_CLOSE_TO_PLAYER, true);
            AddPrecondition(LurkerEnemy.WS_PLAYER_IS_AWARE, false);
            AddPrecondition(LurkerEnemy.WS_JUMPSCARE_TRIGGERED, false);
            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);

            AddEffect(LurkerEnemy.WS_JUMPSCARE_TRIGGERED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _timer = 0f;
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null || !enemy.HasPlayer) return false;
            return Vector3.Distance(enemy.transform.position, enemy.PlayerPosition)
                   <= enemy.JumpscareRange;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Attacking);
            enemy.SetDebugActionName(ActionName);

            if (_timer == 0f)
                enemy.MarkJumpscareTriggered();

            _timer += Time.deltaTime;
            // TODO: trigger jumpscare animation/camera shake here
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return true;

            if (_timer >= enemy.JumpscareDuration)
            {
                enemy.MarkJumpscareDone();
                return true;
            }
            return false;
        }

        public override void Reset()
        {
            base.Reset();
            _timer = 0f;
        }
    }
    #endregion

    #region Steam Items Action.
    /// <summary>
    /// Steals items from the player. Prefers AR currency (1-40% of total).
    /// Falls back to a random item if AR currency is 0.
    /// Stub: AR currency steal routed through PlayerInventory for now.
    /// </summary>
    private class StealItemsAction : GoapAction
    {
        public StealItemsAction() : base("StealItemsAction")
        {
            BaseCost = 0.8f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_CLOSE_TO_PLAYER, true);
            AddPrecondition(LurkerEnemy.WS_WITHIN_STEAL_RANGE, true);
            AddPrecondition(LurkerEnemy.WS_PLAYER_HAS_ITEMS, true);
            AddPrecondition(LurkerEnemy.WS_PLAYER_IS_AWARE, false);
            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);
            AddPrecondition(LurkerEnemy.WS_HIDING, false);
            AddPrecondition(LurkerEnemy.WS_JUMPSCARE_DONE, true);

            AddEffect(LurkerEnemy.WS_HAS_STOLEN, true);
            AddEffect(LurkerEnemy.WS_PLAYER_IS_AWARE, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
            => agent.GetComponent<LurkerEnemy>() != null;

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null || !enemy.HasPlayer || PlayerInventory.Instance == null) return false;

            float distance = Vector3.Distance(enemy.transform.position, enemy.PlayerPosition);
            if (distance > enemy.StealRangeDistance)
            {
                enemy.SetState(EnemyState.Moving);
                enemy.TryGoTo(enemy.PlayerPosition, this);
                return true;
            }

            enemy.SetState(EnemyState.Attacking);
            enemy.SetDebugActionName(ActionName);

            // TODO: replace with DebugPlayerResources.Instance?.StealARCurrency(pct)
            bool stole = TryStealRandomItem();
            if (!stole) return false;

            enemy.MarkStolen();
            return true;
        }

        public override bool IsDone(GameObject agent) => true;

        private bool TryStealRandomItem()
        {
            if (PlayerInventory.Instance == null) return false;

            int slotCount = PlayerInventory.Instance.SlotCount;
            List<int> candidates = new List<int>(slotCount);

            for (int i = 0; i < slotCount; i++)
            {
                var slot = PlayerInventory.Instance.GetSlot(i);
                if (slot != null && !slot.IsEmpty) candidates.Add(i);
            }

            if (candidates.Count == 0) return false;

            int pick = candidates[Random.Range(0, candidates.Count)];
            var removed = PlayerInventory.Instance.RemoveItemFromSlot(pick, 1);
            return removed != null;
        }
    }
    #endregion

    #region Escape After Steal Action.
    /// <summary>
    /// Post-steal escape. Moves to a wall-adjacent retreat node away from the
    /// player and any active flashlight cone, then hides.
    /// </summary>
    private class EscapeAfterStealAction : GoapAction
    {
        private Vector3 _retreatNode;
        private bool _nodeFound;

        public EscapeAfterStealAction() : base("EscapeAfterStealAction")
        {
            BaseCost = 0.7f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_HAS_STOLEN, true);
            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);

            AddEffect(LurkerEnemy.WS_HIDING, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            _nodeFound = TryFindRetreatNode(enemy, out _retreatNode);
            return true;
        }

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Alerted);
            enemy.SetDebugActionName(ActionName);

            if (_nodeFound && !enemy.ReachedPoint(_retreatNode, 1.0f))
            {
                enemy.TryGoTo(_retreatNode, this);
                return true;
            }

            enemy.SetHidden(true);
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            return enemy == null || enemy.IsHiding;
        }

        public override void Reset()
        {
            base.Reset();
            _nodeFound = false;
            _retreatNode = Vector3.zero;
        }

        private bool TryFindRetreatNode(LurkerEnemy enemy, out Vector3 node)
        {
            node = enemy.transform.position;
            if (!enemy.HasPlayer) return false;

            // Find a point away from the player, near walls.
            Vector3 awayFromPlayer = (enemy.transform.position - enemy.PlayerPosition).normalized;
            float retreatDist = 6f;

            for (int i = 0; i < 6; i++)
            {
                float jitter = Random.Range(-45f, 45f);
                Vector3 dir = Quaternion.Euler(0f, jitter, 0f) * awayFromPlayer;
                Vector3 candidate = enemy.transform.position + dir * retreatDist;

                if (GridPathfinder.Instance != null)
                {
                    var path = GridPathfinder.Instance.FindPath(
                        enemy.transform.position, candidate, enemy.WalkableLayers);
                    if (path != null && path.Count > 0) { node = candidate; return true; }
                }
                else { node = candidate; return true; }
            }

            return false;
        }
    }
    #endregion

    #region Disappear Now Action.
    /// <summary>
    /// Instant hide when spotted before stealing. No movement — just vanishes.
    /// Distinct from EscapeAfterSteal which moves first.
    /// </summary>
    private class DisappearNowAction : GoapAction
    {
        public DisappearNowAction() : base("DisappearNowAction")
        {
            BaseCost = 0.5f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_PLAYER_IS_AWARE, true);
            AddPrecondition(LurkerEnemy.WS_HAS_STOLEN, false);
            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);

            AddEffect(LurkerEnemy.WS_HIDING, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
            => agent.GetComponent<LurkerEnemy>() != null;

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Alerted);
            enemy.SetDebugActionName(ActionName);
            enemy.SetHidden(true);
            return true;
        }

        public override bool IsDone(GameObject agent) => true;
    }
    #endregion

    #region Reappear Action.
    private class ReappearAction : GoapAction
    {
        public ReappearAction() : base("ReappearAction")
        {
            BaseCost = 0.4f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_HIDING, true);
            AddEffect(LurkerEnemy.WS_HIDING, false);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
            => agent.GetComponent<LurkerEnemy>() != null;

        public override bool Perform(GameObject agent)
        {
            LurkerEnemy enemy = agent.GetComponent<LurkerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Idle);
            enemy.SetDebugActionName(ActionName);
            enemy.SetHidden(false);
            return true;
        }

        public override bool IsDone(GameObject agent) => true;
    }
    #endregion

    #region Stare Action.
    private class StareAction : GoapAction
    {
        private float _timer;

        public StareAction() : base("StareAction")
        {
            BaseCost = 0.6f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_CLOSE_TO_PLAYER, true);
            AddPrecondition(LurkerEnemy.WS_PLAYER_IS_AWARE, false);
            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);
            AddPrecondition(LurkerEnemy.WS_HIDING, false);

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
            enemy.SetDebugActionName(ActionName);
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
    #endregion

    #region Lurk Action.
    private class LurkAction : GoapAction
    {
        private float _timer;
        private float _duration;

        public LurkAction() : base("LurkAction")
        {
            BaseCost = 1.1f;
            requiresInRange = false;

            AddPrecondition(LurkerEnemy.WS_IN_BRIGHT_LIGHT, false);
            AddPrecondition(LurkerEnemy.WS_HIDING, false);
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
            enemy.SetDebugActionName(ActionName);
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