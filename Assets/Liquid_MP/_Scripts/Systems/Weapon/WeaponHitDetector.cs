using Liquid.Audio;
using Liquid.Damage;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;

namespace _Scripts.Systems.Weapon
{
    /// <summary>
    /// Raycasts from the camera on every weapon fire event and applies damage
    /// to anything implementing IDamageable. Also spawns bullet trails and
    /// emits gunshot noise for enemy awareness.
    ///
    /// Attach to the same GameObject as TacticalShooterPlayer.
    /// </summary>
    [RequireComponent(typeof(TacticalShooterPlayer))]
    public class WeaponHitDetector : MonoBehaviour
    {
        #region Settings

        [Header("Raycast")]
        [Tooltip("Maximum distance a bullet can travel.")]
        [SerializeField] private float maxRange = 100f;

        [Tooltip("Layers the raycast can hit (enemies, environment, etc). Exclude the player layer.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Damage")]
        [Tooltip("Base damage per shot (full damage for rifles, split across pellets for shotguns).")]
        [SerializeField] private float baseDamage = 25f;

        [Header("Shotgun")]
        [Tooltip("Number of pellets per shotgun blast.")]
        [SerializeField] private int shotgunPelletCount = 8;

        [Tooltip("Maximum spread angle in degrees for each pellet.")]
        [SerializeField] private float shotgunSpreadAngle = 5f;

        [Header("Visual Feedback")]
        [Tooltip("Prefab with a BulletTrailMover + TrailRenderer. Leave empty to skip trails.")]
        [SerializeField] private GameObject bulletTrailPrefab;

        [SerializeField] private float trailSpeed = 200f;

        [Tooltip("Optional VFX spawned when hitting a non-damageable surface (sparks, dust).")]
        [SerializeField] private GameObject hitImpactPrefab;

        [Header("Noise")]
        [Tooltip("Noise level emitted on each shot for enemy awareness.")]
        [SerializeField] private NoiseLevel gunshotNoiseLevel = NoiseLevel.High;

        [Header("Debug")]
        [SerializeField] private bool showDebugRays;

        #endregion

        #region Runtime

        private TacticalShooterPlayer _player;
        private Camera _camera;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            _player = GetComponent<TacticalShooterPlayer>();
        }

        private void Start()
        {
            // Cache the camera used for aiming. FPSCameraAnimator drives its rotation.
            _camera = Camera.main;

            if (_camera == null)
            {
                Debug.LogError("[WeaponHitDetector] Camera.main is null. " +
                               "Ensure the FPS camera is tagged 'MainCamera'.");
            }
        }

        #endregion

        #region Subscription

        /// <summary>Call once per weapon after it is instantiated to hook into its fire event.</summary>
        public void SubscribeToWeapon(TacticalShooterWeapon weapon)
        {
            if (weapon != null) weapon.OnFired += HandleWeaponFired;
        }

        /// <summary>Unhook from a weapon (e.g. before it is destroyed or swapped).</summary>
        public void UnsubscribeFromWeapon(TacticalShooterWeapon weapon)
        {
            if (weapon != null) weapon.OnFired -= HandleWeaponFired;
        }

        #endregion

        #region Fire Handling

        private void HandleWeaponFired()
        {
            if (_camera == null) return;

            TacticalShooterWeapon weapon = _player.GetPrimaryWeapon();
            bool isShotgun = weapon is TacticalShotgun;

            if (isShotgun)
            {
                FireShotgunRaycasts(weapon);
            }
            else
            {
                FireSingleRaycast(weapon);
            }

            EmitGunshotNoise(weapon);
        }

        private void FireSingleRaycast(TacticalShooterWeapon weapon)
        {
            Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);
            Vector3 endPoint;

            if (Physics.Raycast(ray, out RaycastHit hit, maxRange, hitLayers,
                    QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
                ProcessHit(hit, baseDamage);

                if (showDebugRays)
                    Debug.DrawLine(ray.origin, hit.point, Color.red, 1f);
            }
            else
            {
                endPoint = ray.origin + ray.direction * maxRange;

                if (showDebugRays)
                    Debug.DrawRay(ray.origin, ray.direction * maxRange, Color.yellow, 1f);
            }

            SpawnTrail(weapon.GetMuzzlePosition(), endPoint);
        }

        private void FireShotgunRaycasts(TacticalShooterWeapon weapon)
        {
            float damagePerPellet = baseDamage / shotgunPelletCount;
            Vector3 muzzle = weapon.GetMuzzlePosition();

            for (int i = 0; i < shotgunPelletCount; i++)
            {
                Vector2 spread = Random.insideUnitCircle * shotgunSpreadAngle;
                Vector3 direction = Quaternion.Euler(spread.x, spread.y, 0f)
                                    * _camera.transform.forward;

                Ray ray = new Ray(_camera.transform.position, direction);
                Vector3 endPoint;

                if (Physics.Raycast(ray, out RaycastHit hit, maxRange, hitLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    endPoint = hit.point;
                    ProcessHit(hit, damagePerPellet);

                    if (showDebugRays)
                        Debug.DrawLine(ray.origin, hit.point, Color.red, 1f);
                }
                else
                {
                    endPoint = ray.origin + direction * maxRange;

                    if (showDebugRays)
                        Debug.DrawRay(ray.origin, direction * maxRange, Color.yellow, 1f);
                }

                SpawnTrail(muzzle, endPoint);
            }
        }

        #endregion

        #region Hit Processing

        private void ProcessHit(RaycastHit hit, float damage)
        {
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

            if (damageable != null && !damageable.IsDead)
            {
                DamageInfo info = new DamageInfo
                {
                    Amount = damage,
                    HitPoint = hit.point,
                    HitNormal = hit.normal,
                    Instigator = gameObject,
                    Type = DamageType.Bullet
                };

                damageable.TakeDamage(info);
            }
            else if (hitImpactPrefab != null)
            {
                // Spawn impact VFX on non-damageable surfaces (walls, floors, etc.)
                Instantiate(hitImpactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            }
        }

        #endregion

        #region Visual Feedback

        private void SpawnTrail(Vector3 start, Vector3 end)
        {
            if (bulletTrailPrefab == null) return;

            GameObject trail = Instantiate(bulletTrailPrefab, start, Quaternion.identity);
            BulletTrailMover mover = trail.GetComponent<BulletTrailMover>();

            if (mover != null)
            {
                mover.Initialise(start, end, trailSpeed);
            }
        }

        #endregion

        #region Noise

        private void EmitGunshotNoise(TacticalShooterWeapon weapon)
        {
            if (NoiseManager.Instance == null) return;

            Vector3 noisePosition = weapon.GetMuzzlePosition();
            NoiseManager.Instance.EmitNoise(noisePosition, gunshotNoiseLevel, NoiseCategory.Gunshot);
        }

        #endregion
    }
}
