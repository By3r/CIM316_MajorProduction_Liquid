using UnityEngine;

namespace Liquid.Dialogue
{
    public abstract class DialogueCondition : ScriptableObject
    {
        public abstract bool IsMet(IDialogueContext context);
    }
}