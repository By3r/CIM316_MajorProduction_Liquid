using System;
using UnityEngine;

namespace Liquid.Dialogue
{
    public abstract class DialogueNode : ScriptableObject
    {
        [SerializeField] private string nodeId = Guid.Empty.ToString();

        [Header("Presentation")]
        [SerializeField] private DialogueSpeakerKind speakerKind = DialogueSpeakerKind.Npc;

        [TextArea(3, 8)]
        [SerializeField] private string text;

        public string NodeId => nodeId;
        public DialogueSpeakerKind SpeakerKind => speakerKind;
        public string Text => text;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(nodeId) || nodeId == Guid.Empty.ToString())
                nodeId = Guid.NewGuid().ToString();
        }
    }
}