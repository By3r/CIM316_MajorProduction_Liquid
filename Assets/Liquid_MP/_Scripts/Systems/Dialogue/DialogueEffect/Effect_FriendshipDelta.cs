using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Effects/Friendship Delta", fileName = "Fx_FriendshipDelta_")]
    public sealed class Effect_FriendshipDelta : DialogueEffect
    {
        [SerializeField] private int delta;

        public override void Apply(IDialogueContext context)
        {
            if (context?.CurrentNpc == null) return;
            if (context.Friendship == null) return;

            context.Friendship.AddFriendshipPoints(context.CurrentNpc, delta);
        }
    }
}