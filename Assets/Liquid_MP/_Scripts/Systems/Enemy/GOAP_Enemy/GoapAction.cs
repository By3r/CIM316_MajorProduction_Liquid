using System.Collections.Generic;
using UnityEngine;

namespace Liquid.AI.GOAP
{
    /// <summary>
    /// Base class for all GOAP actions.
    /// </summary>
    public abstract class GoapAction
    {
        [Header("Preconditions & Effects")]
        protected readonly Dictionary<string, object> preconditions = new Dictionary<string, object>();
        protected readonly Dictionary<string, object> effects = new Dictionary<string, object>();

        public IReadOnlyDictionary<string, object> Preconditions => preconditions;
        public IReadOnlyDictionary<string, object> Effects => effects;

        [Header("Identity")]
        /// <summary>
        /// Subclasses MUST pass this via the constructor.
        /// </summary>
        public string ActionName { get; }

        #region Cost.
        /// <summary>
        /// The static base cost set in the subclass constructor.
        /// </summary>
        public float BaseCost { get; protected set; } = 1f;

        /// <summary>
        /// Temporary cost added on top by the path failure system.
        /// Resets to 0 when the active goal changes (via ResetInflation).
        /// </summary>
        private float _inflatedCost = 0f;

        /// <summary>
        /// How much inflation is added per path failure event.
        /// Subclasses can override this to make some actions more resistant to inflation.
        /// </summary>
        protected virtual float InflationStep => 2f;

        /// <summary>
        /// Cap on how much inflation can accumulate before it stops growing.
        /// Prevents infinite cost spiralling on very stuck enemies.
        /// </summary>
        protected virtual float MaxInflation => 10f;

        /// <summary>
        /// Called by the planner to get the final cost of this action.
        /// Override in subclasses to return a value that reacts to world state,
        /// distance, noise level, health, etc.
        /// The base implementation returns BaseCost + inflation.
        /// </summary>
        public virtual float GetDynamicCost(Dictionary<string, object> worldState)
        {
            return BaseCost + _inflatedCost;
        }

        /// <summary>
        /// Inflates this action's cost by one step. Called by EnemyBase when
        /// pathfinding repeatedly fails to reach this action's movement target.
        /// </summary>
        public void InflateCost()
        {
            _inflatedCost = Mathf.Min(_inflatedCost + InflationStep, MaxInflation);
        }

        /// <summary>
        /// Resets temporary inflation to zero. Called by EnemyBase when the
        /// active GOAP goal changes, giving the action a clean slate.
        /// </summary>
        public void ResetInflation()
        {
            _inflatedCost = 0f;
        }
        #endregion

        #region Targeting.
        public virtual GameObject Target { get; set; }
        protected bool requiresInRange = false;
        public bool InRange { get; set; }
        public virtual bool RequiresInRange() => requiresInRange;
        #endregion

        #region Constructor.
        /// <summary>
        /// Subclasses need to call this constructor with their action name.
        /// Example: public MyAction() : base("MyAction") { ... }
        /// </summary>
        protected GoapAction(string actionName)
        {
            ActionName = actionName;
        }
        #endregion

        #region Lifecycle.
        /// <summary>
        /// Reset is called when the planner discards this action mid-execution.
        /// Always call base.Reset() in overrides.
        /// </summary>
        public virtual void Reset()
        {
            Target = null;
            InRange = false;
        }

        /// <summary>
        /// Returns true when this action has fully completed its work.
        /// </summary>
        public virtual bool IsDone(GameObject agent) => true;

        /// <summary>
        /// Called before the planner includes this action in a plan.
        /// Return false to exclude this action from the current plan entirely
        /// (e.g. target is null, out of ammo, etc.).
        /// </summary>
        public virtual bool CheckProceduralPrecondition(GameObject agent) => true;

        /// <summary>
        /// Called every frame while this action is the active plan step.
        /// Return false to signal failure; the planner will replan.
        /// Return true to signal the action is still running.
        /// </summary>
        public abstract bool Perform(GameObject agent);
        #endregion

        #region Precondition & Effect helpers.
        protected void AddPrecondition(string key, object value)
        {
            preconditions[key] = value;
        }

        protected void AddEffect(string key, object value)
        {
            effects[key] = value;
        }
        #endregion
    }
}