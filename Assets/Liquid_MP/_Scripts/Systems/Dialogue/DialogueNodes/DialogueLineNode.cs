using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Nodes/Line Node", fileName = "Node_Line_")]
    public sealed class DialogueLineNode : DialogueNode
    {
        [Header("Flow")]
        [SerializeField] private DialogueNode next;

        public DialogueNode Next => next;
    }
}