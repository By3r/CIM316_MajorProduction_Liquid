namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Categories for physical items that occupy inventory or equipment slots.
    /// Each derived InventoryItemData subclass auto-sets its type in the constructor.
    /// </summary>
    public enum PhysicalItemType
    {
        Miscellaneous,      // Generic crafting materials, junk, sellable loot
        Schematic,          // Unlocks a crafting recipe when used
        PrimaryWeapon,      // Equippable in Primary slot only
        SecondaryWeapon,    // Equippable in Primary or Secondary slot
        Throwable,          // Equippable in Throwable slot (grenades, etc.)
        Container,          // Holds resources (AR, gasoline, etc.) — can be emptied
        PowerCell,          // Powers elevator safe room
        SuitProcessor,      // One-time-use suit enhancement (extra slots, silent move, etc.)
        SuitAddon,          // Equippable in Suit Add-On slot (neutronic boots, etc.)
        KeyItem             // Quest/progression items — cannot be dropped
    }
}
