using System.Collections.Generic;
using System.Linq;

namespace Liquid.AI.GOAP
{
    public static class GoapPlanner
    {
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

        public static bool Plan(
            List<GoapAction> availableActions,
            Dictionary<string, object> worldState,
            Dictionary<string, object> goal,
            out Queue<GoapAction> plan)
        {
            plan = null;

            List<GoapAction> usableActions = new List<GoapAction>();
            for (int i = 0; i < availableActions.Count; i++)
            {
                if (availableActions[i] != null)
                {
                    usableActions.Add(availableActions[i]);
                }
            }

            Node start = new Node(null, 0f, CopyState(worldState), null);
            List<Node> leaves = new List<Node>();

            bool success = BuildGraph(start, leaves, usableActions, goal);

            if (!success)
            {
                return false;
            }

            Node cheapest = leaves.OrderBy(n => n.RunningCost).FirstOrDefault();
            if (cheapest == null)
            {
                return false;
            }

            List<GoapAction> result = new List<GoapAction>();
            Node n2 = cheapest;
            while (n2 != null)
            {
                if (n2.Action != null)
                {
                    result.Insert(0, n2.Action);
                }
                n2 = n2.Parent;
            }

            plan = new Queue<GoapAction>(result);
            return true;
        }

        private static bool BuildGraph(
            Node parent,
            List<Node> leaves,
            List<GoapAction> usableActions,
            Dictionary<string, object> goal)
        {
            bool foundOne = false;

            for (int i = 0; i < usableActions.Count; i++)
            {
                GoapAction action = usableActions[i];
                if (action == null)
                {
                    continue;
                }

                if (!InState(action.Preconditions, parent.State))
                {
                    continue;
                }

                Dictionary<string, object> currentState = ApplyEffects(parent.State, action.Effects);
                Node node = new Node(parent, parent.RunningCost + action.Cost, currentState, action);

                if (InState(goal, currentState))
                {
                    leaves.Add(node);
                    foundOne = true;
                }
                else
                {
                    List<GoapAction> subset = ActionSubset(usableActions, action);
                    bool found = BuildGraph(node, leaves, subset, goal);
                    if (found)
                    {
                        foundOne = true;
                    }
                }
            }

            return foundOne;
        }

        private static bool InState(IReadOnlyDictionary<string, object> test, Dictionary<string, object> state)
        {
            foreach (KeyValuePair<string, object> kvp in test)
            {
                if (!state.TryGetValue(kvp.Key, out object value))
                {
                    return false;
                }

                if (!Equals(value, kvp.Value))
                {
                    return false;
                }
            }
            return true;
        }

        private static Dictionary<string, object> ApplyEffects(Dictionary<string, object> state, IReadOnlyDictionary<string, object> effects)
        {
            Dictionary<string, object> newState = CopyState(state);

            foreach (KeyValuePair<string, object> kvp in effects)
            {
                if (!newState.ContainsKey(kvp.Key))
                {
                    newState.Add(kvp.Key, kvp.Value);
                }
                else
                {
                    newState[kvp.Key] = kvp.Value;
                }
            }

            return newState;
        }

        private static List<GoapAction> ActionSubset(List<GoapAction> actions, GoapAction removeMe)
        {
            List<GoapAction> subset = new List<GoapAction>();
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] != null && actions[i] != removeMe)
                {
                    subset.Add(actions[i]);
                }
            }

            return subset;
        }

        private static Dictionary<string, object> CopyState(Dictionary<string, object> state)
        {
            Dictionary<string, object> copy = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> kvp in state)
            {
                copy[kvp.Key] = kvp.Value;
            }
            return copy;
        }
    }
}