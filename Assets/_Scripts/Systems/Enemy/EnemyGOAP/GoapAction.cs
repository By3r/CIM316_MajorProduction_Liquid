using System.Collections.Generic;
using UnityEngine;

namespace Liquid.AI.GOAP
{
    public abstract class GoapAction
    {
        protected readonly Dictionary<string, object> preconditions = new Dictionary<string, object>();
        protected readonly Dictionary<string, object> effects = new Dictionary<string, object>();

        public float Cost { get; protected set; } = 1f;
        public string ActionName { get; protected set; }

        public virtual GameObject Target { get; set; }
        protected bool requiresInRange = false;
        public bool InRange { get; set; }

        public IReadOnlyDictionary<string, object> Preconditions => preconditions;
        public IReadOnlyDictionary<string, object> Effects => effects;
        public virtual bool RequiresInRange() { return requiresInRange; }

        public virtual void Reset()
        {
            Target = null;
            InRange = false;
        }

        public virtual bool IsDone(GameObject agent) { return true; }
        public virtual bool CheckProceduralPrecondition(GameObject agent) { return true; }
        public abstract bool Perform(GameObject agent);

        protected void AddPrecondition(string key, object value)
        {
            if (!preconditions.ContainsKey(key))
            {
                preconditions.Add(key, value);
            }
            else
            {
                preconditions[key] = value;
            }
        }

        protected void AddEffect(string key, object value)
        {
            if (!effects.ContainsKey(key))
            {
                effects.Add(key, value);
            }
            else
            {
                effects[key] = value;
            }
        }
    }
}