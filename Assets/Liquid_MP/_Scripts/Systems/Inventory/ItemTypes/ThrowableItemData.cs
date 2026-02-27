using UnityEngine;

namespace _Scripts.Systems.Inventory.ItemTypes
{
    /// <summary>
    /// Item data for throwable items (grenades, decoys, etc.) that can be
    /// equipped in the Throwable slot and thrown by the player.
    /// <c>itemType</c> is auto-set to <see cref="PhysicalItemType.Throwable"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Items/Throwable Item", fileName = "NewThrowableItem")]
    public class ThrowableItemData : InventoryItemData
    {
        [Header("Throwable Data")]
        [Tooltip("Force applied when thrown.")]
        public float throwForce = 15f;

        [Tooltip("Upward arc angle in degrees.")]
        public float arcAngle = 30f;

        [Tooltip("Time in seconds before the effect triggers (0 = on impact).")]
        public float fuseDuration = 3f;

        [Tooltip("Prefab spawned on detonation / impact.")]
        public GameObject effectPrefab;

        [Tooltip("Damage dealt by the explosion or effect.")]
        public float damage = 50f;

        [Tooltip("Radius of the explosion or effect area.")]
        public float effectRadius = 5f;

        protected override void OnEnable()
        {
            itemType = PhysicalItemType.Throwable;
            base.OnEnable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            itemType = PhysicalItemType.Throwable;
            base.OnValidate();
        }
#endif
    }
}
