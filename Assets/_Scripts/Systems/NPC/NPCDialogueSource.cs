using Liquid.NPC;
using UnityEngine;

namespace Liquid.Dialogue
{
    public sealed class NpcDialogueSource : MonoBehaviour
    {
        [Header("NPC Data")]
        [SerializeField] private NpcDefinition npc;

        [Header("Dialogue")]
        [SerializeField] private DialogueAsset dialogue;

        public NpcDefinition Npc => npc;
        public DialogueAsset Dialogue => dialogue;

        public bool IsValid => npc != null && dialogue != null;
    }
}