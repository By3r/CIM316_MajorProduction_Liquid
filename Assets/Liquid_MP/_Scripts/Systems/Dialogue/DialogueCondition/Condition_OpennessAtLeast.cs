using Liquid.NPC;
using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Conditions/Openness At Least", fileName = "Cond_OpennessAtLeast_")]
    public sealed class Condition_OpennessAtLeast : DialogueCondition
    {
        [SerializeField] private OpennessLevel minimum;

        public override bool IsMet(IDialogueContext context)
        {
            var npc = context?.CurrentNpc;
            if (npc == null) return false;
            return npc.Openness >= minimum;
        }
    }
}