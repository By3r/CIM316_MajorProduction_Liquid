using System.Collections.Generic;
using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Dialogue Asset", fileName = "Dialogue_")]
    public sealed class DialogueAsset : ScriptableObject
    {
        [SerializeField] private DialogueNode entryNode;

        [Tooltip("Node references so they are included in builds and easy to navigate in the inspector.")]
        [SerializeField] private List<DialogueNode> nodes = new();

        public DialogueNode EntryNode => entryNode;
        public IReadOnlyList<DialogueNode> Nodes => nodes;

        private void OnValidate()
        {
            nodes ??= new List<DialogueNode>();
        }
    }
}