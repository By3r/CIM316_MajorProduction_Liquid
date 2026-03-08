namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Equipment slot categories on the player's hotbar.
    /// Throwable is intentionally excluded — deferred to a later phase.
    /// </summary>
    public enum EquipmentSlotType
    {
        PrimaryWeapon   = 0,
        SecondaryWeapon = 1,
        SuitAddon       = 2,
        ComsDevice      = 3
    }
}
