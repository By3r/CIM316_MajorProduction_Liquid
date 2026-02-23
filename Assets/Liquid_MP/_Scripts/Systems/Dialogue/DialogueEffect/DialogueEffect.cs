using UnityEngine;

namespace Liquid.Dialogue
{
    public abstract class DialogueEffect : ScriptableObject
    {
        public abstract void Apply(IDialogueContext context);
    }
}