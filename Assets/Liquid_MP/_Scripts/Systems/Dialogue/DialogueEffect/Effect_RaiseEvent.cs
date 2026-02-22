using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Effects/Raise Event", fileName = "Fx_RaiseEvent_")]
    public sealed class Effect_RaiseEvent : DialogueEffect
    {
        [SerializeField] private string eventId;

        public override void Apply(IDialogueContext context)
        {
            if (context?.Events == null) return;
            if (string.IsNullOrWhiteSpace(eventId)) return;

            context.Events.Raise(eventId);
        }
    }
}