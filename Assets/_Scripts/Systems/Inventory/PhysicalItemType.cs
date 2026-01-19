namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Types of physical items that occupy inventory slots.
    /// </summary>
    public enum PhysicalItemType
    {
        PowerCell,      // Used to power elevator and machines
        Grenade,        // Throwable explosive
        ARContainer,    // Container for AR extraction
        KeyItem         // Quest/progression items
    }
}
