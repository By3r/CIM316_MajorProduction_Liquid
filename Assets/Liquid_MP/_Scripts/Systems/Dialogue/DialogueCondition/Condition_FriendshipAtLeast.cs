using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Conditions/Friendship At Least", fileName = "Cond_FriendshipAtLeast_")]
    public sealed class Condition_FriendshipAtLeast : DialogueCondition
    {
        [SerializeField] private int minimumPoints = 0;

        public override bool IsMet(IDialogueContext context)
        {
            if (context?.CurrentNpc == null) return false;
            if (context.Friendship == null) return false;

            int points = context.Friendship.GetFriendshipPoints(context.CurrentNpc);
            return points >= minimumPoints;
        }
    }
}