using Liquid.NPC;
using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Conditions/Proficiency Is Poser", fileName = "Cond_ProficiencyIsPoser_")]
    public sealed class Condition_ProficiencyIsPoser : DialogueCondition
    {
        public override bool IsMet(IDialogueContext context)
        {
            var npc = context?.CurrentNpc;
            if (npc == null) return false;
            return npc.Proficiency == OccupationProficiency.Poser;
        }
    }
}