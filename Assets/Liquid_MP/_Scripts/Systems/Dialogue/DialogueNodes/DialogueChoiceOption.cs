using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liquid.Dialogue
{
    [Serializable]
    public sealed class DialogueChoiceOption
    {
        [TextArea(1, 2)]
        [SerializeField] private string text;

        [Tooltip("If true, this choice is treated as the exit option. The runner will always keep exit as the last option.")]
        [SerializeField] private bool isExitOption;

        [Tooltip("Node to go to after selecting this option. If null and IsExitOption is true, dialogue ends.")]
        [SerializeField] private DialogueNode next;

        [Header("Rules")]
        [SerializeField] private List<DialogueCondition> conditions = new();

        [Header("Consequences")]
        [SerializeField] private List<DialogueEffect> effects = new();

        public string Text => text;
        public bool IsExitOption => isExitOption;
        public DialogueNode Next => next;
        public IReadOnlyList<DialogueCondition> Conditions => conditions;
        public IReadOnlyList<DialogueEffect> Effects => effects;

        public bool HasNext => next != null;

        public DialogueChoiceOption(string text, bool isExit, DialogueNode next)
        {
            this.text = text;
            this.isExitOption = isExit;
            this.next = next;
            this.conditions = new List<DialogueCondition>();
            this.effects = new List<DialogueEffect>();
        }
    }
}