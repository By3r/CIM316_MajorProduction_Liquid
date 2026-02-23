using _Scripts.Systems.Inventory;
using Liquid.Audio;
using UnityEngine;

namespace _Scripts.Systems.Weapon
{
    /// <summary>
    /// Per-weapon gameplay stats: damage, range, pellets, noise, trail/impact VFX.
    /// Assigned on each weapon prefab's TacticalShooterWeapon alongside WeaponAnimationData.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Weapons/Combat Data", fileName = "NewWeaponCombatData")]
    public class WeaponCombatData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Link to inventory item data for pickup/equip integration.")]
        public InventoryItemData inventoryItem;

        [Header("Combat")]
        [Min(0f)] public float damage = 25f;
        [Min(0f)] public float range = 100f;
        [Min(1f)] public float headshotMultiplier = 2f;

        [Header("Multi-Pellet")]
        [Tooltip("1 = normal single-shot. 8+ = multi-pellet spread (e.g. shotgun).")]
        [Min(1)] public int pelletCount = 1;
        [Tooltip("Max spread angle in degrees for each pellet.")]
        [Min(0f)] public float spreadAngle = 5f;

        [Header("Melee Fallback")]
        [Min(0f)] public float meleeDamage = 40f;
        [Min(0f)] public float meleeRange = 2f;
        [Range(0f, 180f)] public float meleeAngle = 60f;
        [Min(0f)] public float meleeCooldown = 0.5f;

        [Header("Noise")]
        public NoiseLevel fireNoiseLevel = NoiseLevel.High;
        public NoiseLevel meleeNoiseLevel = NoiseLevel.Medium;

        [Header("Bullet Trail")]
        [Tooltip("Prefab with BulletTrailMover + TrailRenderer. Leave empty to skip trails.")]
        public GameObject trailPrefab;
        [Min(0f)] public float trailSpeed = 300f;

        [Header("Impact")]
        [Tooltip("VFX spawned when hitting a non-damageable surface (sparks, dust).")]
        public GameObject impactEffectPrefab;
        [Min(0f)] public float impactEffectLifetime = 2f;
    }
}
