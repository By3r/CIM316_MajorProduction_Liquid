using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Effects/Inventory Item Delta", fileName = "Fx_InventoryItemDelta_")]
    public sealed class Effect_InventoryItemDelta : DialogueEffect
    {
        [SerializeField] private string itemId;
        [SerializeField] private int amount = 1;

        [Tooltip("If true, adds item. If false, removes item.")]
        [SerializeField] private bool addItem = true;

        public override void Apply(IDialogueContext context)
        {
            if (context?.Inventory == null) return;
            if (string.IsNullOrWhiteSpace(itemId)) return;

            int amt = Mathf.Max(1, amount);

            if (addItem)
                context.Inventory.AddItem(itemId, amt);
            else
                context.Inventory.RemoveItem(itemId, amt);
        }
    }
}