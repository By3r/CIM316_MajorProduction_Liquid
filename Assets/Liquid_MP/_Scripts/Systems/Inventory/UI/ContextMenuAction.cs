using System;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// A single action entry for the dynamic inventory context menu.
    /// Built by InventoryUI per item type, consumed by ItemContextMenu to spawn buttons.
    /// </summary>
    public struct ContextMenuAction
    {
        /// <summary>Button label shown in the menu (e.g. "UPLOAD", "EQUIP", "DROP").</summary>
        public string Label;

        /// <summary>Callback invoked with the inventory slot index when the button is clicked.</summary>
        public Action<int> Callback;
    }
}
