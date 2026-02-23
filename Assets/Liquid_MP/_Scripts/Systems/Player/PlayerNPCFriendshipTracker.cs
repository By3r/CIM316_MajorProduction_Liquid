using System.Collections.Generic;
using UnityEngine;

namespace Liquid.NPC
{
    public interface IFriendshipTracker
    {
        int GetPoints(NpcDefinition npc);
        void AddPoints(NpcDefinition npc, int delta);
        void SetPoints(NpcDefinition npc, int value);
    }

    /// <summary>
    /// Tracks player friendship points per NPC at runtime.
    /// </summary>
    public sealed class PlayerNpcFriendshipTracker : MonoBehaviour, IFriendshipTracker
    {
        [SerializeField] private int minPoints = -100;
        [SerializeField] private int maxPoints = 100;

        private readonly Dictionary<string, int> _pointsByNpcId = new();

        public int GetPoints(NpcDefinition npc)
        {
            if (npc == null) return 0;

            if (_pointsByNpcId.TryGetValue(npc.NpcId, out int value))
                return value;

            return 0;
        }

        public void AddPoints(NpcDefinition npc, int delta)
        {
            if (npc == null) return;

            int next = GetPoints(npc) + delta;
            next = Mathf.Clamp(next, minPoints, maxPoints);
            _pointsByNpcId[npc.NpcId] = next;
        }

        public void SetPoints(NpcDefinition npc, int value)
        {
            if (npc == null) return;

            int val = Mathf.Clamp(value, minPoints, maxPoints);
            _pointsByNpcId[npc.NpcId] = val;
        }
    }
}