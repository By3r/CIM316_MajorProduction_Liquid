using Liquid.NPC;
using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Conditions/Occupation Is", fileName = "Cond_OccupationIs_")]
    public sealed class Condition_OccupationIs : DialogueCondition
    {
        [SerializeField] private OccupationType occupation;

        public override bool IsMet(IDialogueContext context)
        {
            var npc = context?.CurrentNpc;
            if (npc == null) return false;
            return npc.Occupation == occupation;
        }
    }
}