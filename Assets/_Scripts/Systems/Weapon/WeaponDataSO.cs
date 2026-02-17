using _Scripts.Systems.Inventory;
using Liquid.Audio;
using UnityEngine;

namespace _Scripts.Systems.Weapon
{
    /// <summary>
    /// ScriptableObject holding all weapon configuration data.
    /// Links to an InventoryItemData for weapon wheel integration (Dana's system).
    /// The viewmodel prefab is instantiated as a child of the camera when equipped.
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/Weapon/Weapon Data", fileName = "NewWeaponData")]
    public class WeaponDataSO : ScriptableObject
    {
        #region Identity

        [Header("-- Identity --")]
        [Tooltip("The InventoryItemData that represents this weapon in the weapon wheel.")]
        public InventoryItemData inventoryItem;

        [Tooltip("Whether this weapon is ranged (gun) or melee (sword, bat, etc.)")]
        public WeaponType weaponType;

        #endregion

        #region Viewmodel

        [Header("-- Viewmodel --")]
        [Tooltip("Prefab with Animator + WeaponBase subclass. Instantiated as child of camera's ViewmodelRoot.")]
        public GameObject viewmodelPrefab;

        [Tooltip("Hip-fire position offset from camera center. Typical FPS: (0.25, -0.2, 0.4) for right-hand hold.")]
        public Vector3 hipPosition = new Vector3(0.25f, -0.2f, 0.4f);

        [Tooltip("Hip-fire rotation offset (Euler angles).")]
        public Vector3 hipRotation = Vector3.zero;

        [Tooltip("ADS position offset from camera center. Usually closer to center: (0, -0.1, 0.3).")]
        public Vector3 adsPosition = new Vector3(0f, -0.1f, 0.3f);

        [Tooltip("ADS rotation offset (Euler angles).")]
        public Vector3 adsRotation = Vector3.zero;

        [Tooltip("Speed of transitioning between hip and ADS positions.")]
        public float adsTransitionSpeed = 10f;

        [Tooltip("FOV offset when aiming down sights. Negative = zoom in, positive = zoom out. " +
                 "Added to the player's base FOV (e.g. -15 means 15 degrees narrower). 0 = no change.")]
        public float adsFOV = -15f;

        #endregion

        #region Ranged Stats

        [Header("-- Ranged Stats --")]
        [Tooltip("Damage per shot.")]
        public float damage = 25f;

        [Tooltip("Seconds between shots (lower = faster fire rate).")]
        public float fireRate = 0.15f;

        [Tooltip("Rounds per magazine.")]
        public int magazineSize = 30;

        [Tooltip("Time to reload in seconds (animation length should match).")]
        public float reloadTime = 2f;

        [Tooltip("Maximum effective range of the weapon in meters.")]
        public float range = 100f;

        [Tooltip("Damage multiplier for headshots (future use).")]
        public float headshotMultiplier = 2f;

        [Tooltip("If true, holding fire button continuously fires. If false, each click fires once.")]
        public bool isAutomatic = true;

        #endregion

        #region Melee Stats

        [Header("-- Melee Stats --")]
        [Tooltip("Damage per melee swing.")]
        public float meleeDamage = 40f;

        [Tooltip("Range of the melee attack in meters.")]
        public float meleeRange = 2f;

        [Tooltip("Half-angle of the melee swing arc in degrees.")]
        public float meleeAngle = 60f;

        [Tooltip("Cooldown between melee swings in seconds.")]
        public float meleeCooldown = 0.5f;

        #endregion

        #region Noise

        [Header("-- Noise --")]
        [Tooltip("Noise level when firing a ranged weapon. Enemies with hearing will react.")]
        public NoiseLevel fireNoiseLevel = NoiseLevel.High;

        [Tooltip("Noise level when swinging a melee weapon.")]
        public NoiseLevel meleeNoiseLevel = NoiseLevel.Medium;

        #endregion

        #region Recoil / Feel

        [Header("-- Recoil / Feel --")]
        [Tooltip("Camera pitch kick when firing (degrees). Future use.")]
        public float recoilKickback = 1f;

        [Tooltip("How fast the camera recovers from recoil. Future use.")]
        public float recoilRecoverySpeed = 5f;

        #endregion

        #region Bullet Trail

        [Header("-- Bullet Trail --")]
        [Tooltip("Optional prefab with a TrailRenderer component. If assigned, this is spawned and moved " +
                 "from muzzle to hit point so the TrailRenderer draws behind it. Configure duration, width, " +
                 "color, and fade on the TrailRenderer component in the prefab itself. " +
                 "If null, falls back to the LineRenderer settings below.")]
        public GameObject trailPrefab;

        [Tooltip("How fast the trail prefab travels from muzzle to hit point (meters/second). " +
                 "Higher = snappier trail. Only used when trailPrefab is assigned.")]
        public float trailSpeed = 300f;

        [Header("-- Bullet Trail (LineRenderer Fallback) --")]
        [Tooltip("Material for the fallback LineRenderer trail. Only used when Trail Prefab is empty. " +
                 "If both Trail Prefab and this are null, trails are disabled for this weapon.")]
        public Material bulletTrailMaterial;

        [Tooltip("Start width of the LineRenderer trail.")]
        public float trailStartWidth = 0.02f;

        [Tooltip("End width of the LineRenderer trail.")]
        public float trailEndWidth = 0.005f;

        [Tooltip("How long the LineRenderer trail lasts in seconds.")]
        public float trailDuration = 0.08f;

        [Tooltip("Color of the LineRenderer trail.")]
        public Color trailColor = new Color(1f, 0.9f, 0.5f, 0.8f);

        #endregion

        #region Impact Effects

        [Header("-- Impact Effects --")]
        [Tooltip("Particle prefab spawned at bullet impact points on non-enemy surfaces.")]
        public GameObject impactEffectPrefab;

        [Tooltip("How long the impact effect lives before being destroyed.")]
        public float impactEffectLifetime = 2f;

        [Tooltip("Decal projector prefab for bullet holes. If null, no decals are spawned.")]
        public GameObject bulletHoleDecalPrefab;

        [Tooltip("How long bullet hole decals last.")]
        public float decalLifetime = 30f;

        #endregion
    }
}
