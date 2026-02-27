using _Scripts.Systems.Weapon;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;

namespace _Scripts.Systems.Inventory.ItemTypes
{
    /// <summary>
    /// Which equipment slots this weapon can be placed in.
    /// </summary>
    public enum WeaponSlotType
    {
        PrimaryOnly,            // Can only go in the Primary slot (rifles, shotguns)
        SecondaryOnly,          // Can only go in the Secondary slot (pistols, sidearms)
        PrimaryOrSecondary      // Can go in either slot (SMGs, compact weapons)
    }

    /// <summary>
    /// Item data for weapons that can be equipped in Primary or Secondary slots.
    /// <c>itemType</c> is auto-set based on <see cref="weaponSlot"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Items/Weapon Item", fileName = "NewWeaponItem")]
    public class WeaponItemData : InventoryItemData
    {
        [Header("Weapon Slot")]
        [Tooltip("Which equipment slots this weapon fits in.")]
        public WeaponSlotType weaponSlot = WeaponSlotType.PrimaryOnly;

        [Header("Weapon Data")]
        [Tooltip("The weapon prefab instantiated when equipped (KINEMATION weapon).")]
        public GameObject weaponPrefab;

        [Tooltip("Combat stats (damage, range, pellets, trail, noise).")]
        public WeaponCombatData combatData;

        [Tooltip("Animation settings (recoil, sway, procedural motion).")]
        public WeaponAnimationData animationData;

        protected override void OnEnable()
        {
            SyncItemType();
            base.OnEnable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            SyncItemType();
            base.OnValidate();

            if (isStackable)
            {
                isStackable = false;
                maxStackSize = 1;
            }
        }
#endif

        private void SyncItemType()
        {
            switch (weaponSlot)
            {
                case WeaponSlotType.PrimaryOnly:
                    itemType = PhysicalItemType.PrimaryWeapon;
                    break;
                case WeaponSlotType.SecondaryOnly:
                case WeaponSlotType.PrimaryOrSecondary:
                    itemType = PhysicalItemType.SecondaryWeapon;
                    break;
            }
        }
    }
}
