using Liquid.NPC;
using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Conditions/Proficiency At Least", fileName = "Cond_ProficiencyAtLeast_")]
    public sealed class Condition_ProficiencyAtLeast : DialogueCondition
    {
        [SerializeField] private OccupationProficiency minimum;

        public override bool IsMet(IDialogueContext context)
        {
            var npc = context?.CurrentNpc;
            if (npc == null) return false;

            return npc.Proficiency >= minimum;
        }
    }
}