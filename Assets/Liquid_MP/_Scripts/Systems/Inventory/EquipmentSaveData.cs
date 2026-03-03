using System;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Serializable data for a single equipment slot, used for persistence across floors.
    /// </summary>
    [Serializable]
    public class EquipmentSlotSaveData
    {
        public string itemId;
        public EquipmentSlotType slotType;
    }

    /// <summary>
    /// Serializable snapshot of all equipment slots + active weapon selection.
    /// </summary>
    [Serializable]
    public class EquipmentSaveData
    {
        public EquipmentSlotSaveData[] slots;
        public int activeWeaponSlot;
    }
}
