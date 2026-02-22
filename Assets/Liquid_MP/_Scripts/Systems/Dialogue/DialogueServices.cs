using Liquid.NPC;

namespace Liquid.Dialogue
{
    public interface IFriendshipService
    {
        int GetFriendshipPoints(NpcDefinition npc);
        void AddFriendshipPoints(NpcDefinition npc, int delta);
    }

    public interface ICurrencyService
    {
        int GetCurrency();
        bool CanSpend(int amount);
        void Add(int amount);
        bool Spend(int amount);
    }

    public interface IInventoryService
    {
        bool HasItem(string itemId, int amount = 1);
        void AddItem(string itemId, int amount = 1);
        bool RemoveItem(string itemId, int amount = 1);
    }

    public interface IGameEventService
    {
        void Raise(string eventId);
    }

    public interface IDialogueContext
    {
        NpcDefinition CurrentNpc { get; }

        IFriendshipService Friendship { get; }
        ICurrencyService Currency { get; }
        IInventoryService Inventory { get; }
        IGameEventService Events { get; }
    }
}