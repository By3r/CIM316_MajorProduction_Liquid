using Liquid.NPC;
using UnityEngine;

namespace Liquid.Dialogue
{
    /// <summary>
    /// Provides context for the dialogue to run accordingly. The Interfaces below are not final and can be removed/ modified.
    /// </summary>
    public sealed class DialogueContextBehaviour : MonoBehaviour, IDialogueContext
    {
        [SerializeField] private NpcDefinition currentNpc;

        [Header("Services")]
        [SerializeField] private MonoBehaviour friendshipService;
        [SerializeField] private MonoBehaviour currencyService;
        [SerializeField] private MonoBehaviour inventoryService;
        [SerializeField] private MonoBehaviour gameEventService;

        public NpcDefinition CurrentNpc => currentNpc;

        public IFriendshipService Friendship => friendshipService as IFriendshipService;
        public ICurrencyService Currency => currencyService as ICurrencyService;
        public IInventoryService Inventory => inventoryService as IInventoryService;
        public IGameEventService Events => gameEventService as IGameEventService;

        public void SetNpc(NpcDefinition npc) => currentNpc = npc;
    }
}