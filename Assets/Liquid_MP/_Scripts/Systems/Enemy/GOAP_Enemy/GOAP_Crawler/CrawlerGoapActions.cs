using System.Collections.Generic;
using Liquid.AI.GOAP;
using UnityEngine;

/// <summary>
/// All GOAP actions for the Crawler enemy.
/// </summary> 
public static class CrawlerGoapActions
{
    public static GoapAction[] CreateAll()
    {
        return new GoapAction[]
        {
            new ChasePlayerAction(),
            new LeapAttackAction(),
            new InvestigateNoiseAction(),
            new ChatWithPeerAction(),
            new RetreatToNestAction(),
            new ReturnHomeAction(),
            new RoamHomeAction(),
            new IdleHomeAction(),
        };
    }

    #region Chase Action.
    /// <summary>
    /// Chases the player until within leap range.
    /// Dynamic cost: cheaper when close, more expensive when far.
    /// Further reduced if the chat group was disturbed this session.
    /// </summary>
    private class ChasePlayerAction : GoapAction
    {
        public ChasePlayerAction() : base("ChasePlayerAction")
        {
            BaseCost = 1.6f;
            requiresInRange = false;

            AddPrecondition(CrawlerEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(CrawlerEnemy.WS_PLAYER_INTERESTING, true);
            AddPrecondition(CrawlerEnemy.WS_CAN_REACH_PLAYER, true);

            AddEffect(CrawlerEnemy.WS_IN_LEAP_RANGE, true);
        }

        public override float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost;
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            return enemy != null && enemy.HasPlayer;
        }

        public override bool Perform(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null || !enemy.HasPlayer) return false;

            enemy.SetState(EnemyState.Chasing);
            enemy.SetDebugActionName(ActionName);

            bool stillMoving = enemy.TryGoTo(enemy.PlayerPosition, this);
            if (!stillMoving)
            {
                return true;
            }

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return true;
            return enemy.InLeapRange;
        }
    }
    #endregion

    #region Leap Attack Action.
    /// <summary>
    /// Short forward burst onto the player.
    /// Dynamic cost: scales with attack cost modifier (distance + chat disturbance).
    /// </summary>
    private class LeapAttackAction : GoapAction
    {
        private float _timer;
        private Vector3 _dir;
        private bool _initialized;

        public LeapAttackAction() : base("LeapAttackAction")
        {
            BaseCost = 0.6f;
            requiresInRange = false;

            AddPrecondition(CrawlerEnemy.WS_HAS_PLAYER, true);
            AddPrecondition(CrawlerEnemy.WS_IN_LEAP_RANGE, true);

            AddEffect(CrawlerEnemy.WS_ATTACKED, true);
        }

        public override float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost;
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _timer = 0f;
            _initialized = false;

            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            return enemy != null && enemy.HasPlayer;
        }

        public override bool Perform(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null || !enemy.HasPlayer) return false;

            enemy.SetState(EnemyState.Attacking);
            enemy.SetDebugActionName(ActionName);

            if (!_initialized)
            {
                Vector3 toPlayer = enemy.PlayerPosition - enemy.transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude < 0.01f) toPlayer = enemy.transform.forward;
                _dir = toPlayer.normalized;
                _initialized = true;

                enemy.MarkAttacked();
                // TODO: apply leapDamage via player damage system when available.
            }

            _timer += Time.deltaTime;

            // TODO: Apply a leap Animation instead when available.
            Vector3 step = enemy.transform.position + _dir * enemy.LeapForwardDistance;
            enemy.TryGoTo(step, this);

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return true;
            return _timer >= enemy.LeapDuration;
        }

        public override void Reset()
        {
            base.Reset();
            _timer = 0f;
            _initialized = false;
            _dir = Vector3.zero;
        }
    }
    #endregion

    #region Investigate Noise Action.
    private class InvestigateNoiseAction : GoapAction
    {
        public InvestigateNoiseAction() : base("InvestigateNoiseAction")
        {
            BaseCost = 1.1f;
            requiresInRange = false;

            AddPrecondition(CrawlerEnemy.WS_HAS_NOISE_INTEREST, true);
            AddEffect(CrawlerEnemy.WS_AT_INVESTIGATE_POINT, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent) => agent.GetComponent<CrawlerEnemy>() != null;

        public override bool Perform(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Roaming);
            enemy.SetDebugActionName(ActionName);
            enemy.TryGoTo(enemy.InvestigatePoint, this);
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return true;
            return enemy.ReachedPoint(enemy.InvestigatePoint, 1.0f);
        }
    }
    #endregion

    #region Chat with peer Action.
    /// <summary>
    /// Peer-seeking chat behaviour.
    /// The crawler approaches the closest eligible peer, waits for chat duration, then marks chat complete.
    /// 
    /// Disturbance (noise event during chat) flags the whole group, which
    /// reduces attack cost for the remainder of the session.
    ///
    /// Max 4 per group for chat.
    /// </summary>
    private class ChatWithPeerAction : GoapAction
    {
        private CrawlerEnemy _chatTarget;
        private bool _chatStarted;
        private List<CrawlerEnemy> _group;

        public ChatWithPeerAction() : base("ChatWithPeerAction")
        {
            BaseCost = 1.0f;
            requiresInRange = false;

            AddPrecondition(CrawlerEnemy.WS_IN_HOME, true);
            AddEffect(CrawlerEnemy.WS_CHAT_COMPLETE, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return false;

            _chatTarget = FindChatTarget(enemy);
            _chatStarted = false;
            _group = null;

            return _chatTarget != null;
        }

        public override bool Perform(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null || _chatTarget == null) return false;

            enemy.SetDebugActionName(ActionName);

            // 1: Approach the chat target.
            if (!enemy.ReachedPoint(_chatTarget.transform.position, 1.5f))
            {
                enemy.SetState(EnemyState.Roaming);
                enemy.TryGoTo(_chatTarget.transform.position, this);
                return true;
            }

            // 2: Begin chat session once in range.
            if (!_chatStarted)
            {
                _chatStarted = true;
                _group = new List<CrawlerEnemy> { enemy, _chatTarget };
                enemy.BeginChat(_group);
                _chatTarget.BeginChat(_group);
                enemy.SetState(EnemyState.Idle);
            }

            // 3: Wait for chat to expire.
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return true;

            if (!enemy.InChat) return true;
            if (!enemy.IsChatExpired()) return false;

            enemy.EndChat();
            if (_chatTarget != null) _chatTarget.EndChat();
            enemy.MarkChatComplete();
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            _chatTarget = null;
            _chatStarted = false;
            _group = null;
        }

        private CrawlerEnemy FindChatTarget(CrawlerEnemy self)
        {
            Collider[] hits = Physics.OverlapSphere(self.transform.position, 8f);
            CrawlerEnemy best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                CrawlerEnemy peer = hits[i].GetComponent<CrawlerEnemy>();
                if (peer == null || peer == self) continue;
                if (peer.InChat) continue;
                if (peer.CurrentState == EnemyState.Chasing ||
                    peer.CurrentState == EnemyState.Attacking) continue;

                float dist = Vector3.Distance(self.transform.position, peer.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = peer;
                }
            }

            return best;
        }
    }
    #endregion

    #region Retreat to nest Action.
    /// <summary>
    /// Pathfinds toward nest.
    /// </summary>
    private class RetreatToNestAction : GoapAction
    {
        private Vector3 _lastPosition;
        private bool _tracking;

        public RetreatToNestAction() : base("RetreatToNestAction")
        {
            BaseCost = 2.0f;
            requiresInRange = false;

            AddEffect(CrawlerEnemy.WS_RETREATING, true);
        }

        public override float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost;
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return false;

            _lastPosition = enemy.transform.position;
            _tracking = false;
            return true;
        }

        public override bool Perform(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Roaming);
            enemy.SetDebugActionName(ActionName);

            if (!enemy.IsRetreating)
                enemy.BeginRetreat();

            float moved = Vector3.Distance(enemy.transform.position, _lastPosition);
            if (_tracking && moved >= 0.9f) // Roughly one node diameter.
            {
                enemy.IncrementRetreatNodes();
                _lastPosition = enemy.transform.position;
            }
            _tracking = true;

            enemy.TryGoTo(enemy.NestPosition, this);
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return true;

            return enemy.ReachedPoint(enemy.NestPosition, 1.0f);
        }

        public override void Reset()
        {
            base.Reset();
            _lastPosition = Vector3.zero;
            _tracking = false;
        }
    }
    #endregion

    #region Return home Action.
    private class ReturnHomeAction : GoapAction
    {
        public ReturnHomeAction() : base("ReturnHomeAction")
        {
            BaseCost = 0.9f;
            requiresInRange = false;

            AddPrecondition(CrawlerEnemy.WS_IN_HOME, false);
            AddEffect(CrawlerEnemy.WS_RETURNED_HOME, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
            => agent.GetComponent<CrawlerEnemy>() != null;

        public override bool Perform(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Roaming);
            enemy.SetDebugActionName(ActionName);

            bool stillMoving = enemy.TryGoTo(enemy.HomePosition, this);
            if (!stillMoving)
                enemy.MarkReturnedHome();

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            return enemy == null || enemy.InHome;
        }
    }
    #endregion

    #region Roam Home Action.
    private class RoamHomeAction : GoapAction
    {
        private float _timer;
        private float _duration;
        private Vector3 _target;

        public RoamHomeAction() : base("RoamHomeAction")
        {
            BaseCost = 1.0f;
            requiresInRange = false;

            AddPrecondition(CrawlerEnemy.WS_IN_HOME, true);
            AddEffect(CrawlerEnemy.WS_ROAMED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _timer = 0f;
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return false;

            _duration = Random.Range(enemy.RoamDurationRange.x, enemy.RoamDurationRange.y);
            _target = enemy.GetRandomHomePoint();
            return true;
        }

        public override bool Perform(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Roaming);
            enemy.SetDebugActionName(ActionName);

            _timer += Time.deltaTime;
            enemy.TryGoTo(_target, this);

            if (enemy.ReachedPoint(_target, enemy.RoamArriveRadius))
                enemy.MarkRoamed();

            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return true;
            return _timer >= _duration || enemy.ReachedPoint(_target, enemy.RoamArriveRadius);
        }

        public override void Reset()
        {
            base.Reset();
            _timer = 0f;
            _duration = 0f;
            _target = Vector3.zero;
        }
    }
    #endregion

    #region Idle Home Action.
    private class IdleHomeAction : GoapAction
    {
        private float _timer;
        private float _duration;

        public IdleHomeAction() : base("IdleHomeAction")
        {
            BaseCost = 0.7f;
            requiresInRange = false;

            AddPrecondition(CrawlerEnemy.WS_IN_HOME, true);
            AddEffect(CrawlerEnemy.WS_IDLED, true);
        }

        public override bool CheckProceduralPrecondition(GameObject agent)
        {
            _timer = 0f;
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return false;

            _duration = Random.Range(enemy.IdleDurationRange.x, enemy.IdleDurationRange.y);
            return true;
        }

        public override bool Perform(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            if (enemy == null) return false;

            enemy.SetState(EnemyState.Idle);
            enemy.SetDebugActionName(ActionName);

            _timer += Time.deltaTime;
            if (_timer >= _duration) enemy.MarkIdled();
            return true;
        }

        public override bool IsDone(GameObject agent)
        {
            CrawlerEnemy enemy = agent.GetComponent<CrawlerEnemy>();
            return enemy == null || _timer >= _duration;
        }

        public override void Reset()
        {
            base.Reset();
            _timer = 0f;
            _duration = 0f;
        }
    }
    #endregion
}