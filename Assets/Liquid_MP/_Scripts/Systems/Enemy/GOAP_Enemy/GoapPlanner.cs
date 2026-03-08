using System.Collections.Generic;

namespace Liquid.AI.GOAP
{
    /// <summary>
    /// Stateless A*-style GOAP planner.
    /// </summary>
    public static class GoapPlanner
    {
        #region Internal graph node.
        private class Node
        {
            public Node Parent;
            public float RunningCost;
            public Dictionary<string, object> State;
            public GoapAction Action;

            public Node(Node parent, float runningCost, Dictionary<string, object> state, GoapAction action)
            {
                Parent = parent;
                RunningCost = runningCost;
                State = state;
                Action = action;
            }
        }
        #endregion

        #region Public Functions.
        /// <summary>
        /// Builds the cheapest action plan that transforms worldState into goal.
        /// Returns true and populates plan if a valid plan was found.
        /// </summary>
        public static bool Plan(
            List<GoapAction> availableActions,
            Dictionary<string, object> worldState,
            Dictionary<string, object> goal,
            out Queue<GoapAction> plan)
        {
            plan = null;

            if (availableActions == null || availableActions.Count == 0) return false;
            if (worldState == null || goal == null) return false;

            // Strip nulls once up front
            List<GoapAction> usableActions = new List<GoapAction>(availableActions.Count);
            for (int i = 0; i < availableActions.Count; i++)
            {
                if (availableActions[i] != null)
                    usableActions.Add(availableActions[i]);
            }

            if (usableActions.Count == 0) return false;

            Node start = new Node(null, 0f, CopyState(worldState), null);
            List<Node> nodes = new List<Node>();

            if (!BuildGraph(start, nodes, usableActions, goal))
                return false;

            // Pick cheapest node
            Node cheapest = null;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (cheapest == null || nodes[i].RunningCost < cheapest.RunningCost)
                    cheapest = nodes[i];
            }

            if (cheapest == null) return false;

            // Walk back up the chain to reconstruct the plan
            List<GoapAction> result = new List<GoapAction>();
            Node node = cheapest;
            while (node != null)
            {
                if (node.Action != null)
                    result.Insert(0, node.Action);
                node = node.Parent;
            }

            if (result.Count == 0) return false;

            plan = new Queue<GoapAction>(result);
            return true;
        }
        #endregion

        #region Private Functions.

        #region  Graph construction
        private static bool BuildGraph(Node parent, List<Node> nodes, List<GoapAction> usableActions, Dictionary<string, object> goal)
        {
            bool foundOne = false;

            for (int i = 0; i < usableActions.Count; i++)
            {
                GoapAction action = usableActions[i];
                if (action == null) continue;

                // Checks if this action can fire given the current state.
                if (!InState(action.Preconditions, parent.State))
                    continue;

                // Simulate the world state after this action fires
                Dictionary<string, object> nextState = ApplyEffects(parent.State, action.Effects);

                // Dynamic cost: pass the pre-action state so the action can read things like distance-to-player, noise level, health, etc.
                float dynamicCost = action.GetDynamicCost(parent.State);
                Node node = new Node(parent, parent.RunningCost + dynamicCost, nextState, action);

                if (InState(goal, nextState))
                {
                    // This branch reaches the goal — record it as a node
                    nodes.Add(node);
                    foundOne = true;
                }
                else
                {
                    List<GoapAction> subset = BuildSubset(usableActions, action);
                    if (BuildGraph(node, nodes, subset, goal))
                        foundOne = true;
                }
            }

            return foundOne;
        }
        #endregion

        #region Helpers.
        /// <summary>
        /// Returns true if every key/value in test exists with an equal value in state.
        /// </summary>
        private static bool InState(IReadOnlyDictionary<string, object> test, Dictionary<string, object> state)
        {
            foreach (KeyValuePair<string, object> kvp in test)
            {
                if (!state.TryGetValue(kvp.Key, out object value)) return false;
                if (!Equals(value, kvp.Value)) return false;
            }
            return true;
        }

        /// <summary>
        /// Returns a new state dictionary with the action's effects applied.
        /// </summary>
        private static Dictionary<string, object> ApplyEffects(
            Dictionary<string, object> state,
            IReadOnlyDictionary<string, object> effects)
        {
            Dictionary<string, object> newState = CopyState(state);
            foreach (KeyValuePair<string, object> kvp in effects)
                newState[kvp.Key] = kvp.Value;
            return newState;
        }

        /// <summary>
        /// Builds the action list for a child branch — full set minus the action
        /// that was just consumed on this branch (prevents direct self-loops).
        /// </summary>
        private static List<GoapAction> BuildSubset(List<GoapAction> actions, GoapAction exclude)
        {
            List<GoapAction> subset = new List<GoapAction>(actions.Count - 1);
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] != null && actions[i] != exclude)
                    subset.Add(actions[i]);
            }
            return subset;
        }

        private static Dictionary<string, object> CopyState(Dictionary<string, object> state)
        {
            Dictionary<string, object> copy = new Dictionary<string, object>(state.Count);
            foreach (KeyValuePair<string, object> kvp in state)
                copy[kvp.Key] = kvp.Value;
            return copy;
        }
        #endregion
        #endregion
    }
}