using System.Collections.Generic;
using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Nodes/Choice Node", fileName = "Node_Choice_")]
    public sealed class DialogueChoiceNode : DialogueNode
    {
        [Header("Choices")]
        [SerializeField] private List<DialogueChoiceOption> options = new();

        public IReadOnlyList<DialogueChoiceOption> Options => options;

        private void OnValidate()
        {
            options ??= new List<DialogueChoiceOption>();
        }
    }
}