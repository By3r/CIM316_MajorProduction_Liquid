using System;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Serializable data representing a player-dropped item in the world.
    /// Used for persistence across floor transitions (both floor-scoped and safe room-scoped).
    /// </summary>
    [Serializable]
    public class DroppedItemData
    {
        /// <summary>
        /// Unique ID for this dropped instance. Format: "dropped_{itemId}_{8-char-guid}"
        /// </summary>
        public string droppedItemId;

        /// <summary>
        /// The InventoryItemData.itemId used to look up the ScriptableObject at restore time.
        /// </summary>
        public string itemId;

        /// <summary>
        /// Stack quantity of the dropped item.
        /// </summary>
        public int quantity;

        /// <summary>
        /// World position (stored as floats for JsonUtility compatibility).
        /// </summary>
        public float posX, posY, posZ;

        /// <summary>
        /// World rotation euler angles.
        /// </summary>
        public float rotX, rotY, rotZ;
    }
}
