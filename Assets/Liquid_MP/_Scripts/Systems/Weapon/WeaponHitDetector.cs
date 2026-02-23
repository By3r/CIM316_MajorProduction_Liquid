using Liquid.Audio;
using Liquid.Damage;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;

namespace _Scripts.Systems.Weapon
{
    /// <summary>
    /// Raycasts from the camera on every weapon fire event and applies damage
    /// to anything implementing IDamageable. Reads all stats (damage, range,
    /// pellet count, trail, noise) from the weapon's <see cref="WeaponCombatData"/>.
    ///
    /// Attach to the same GameObject as TacticalShooterPlayer.
    /// </summary>
    [RequireComponent(typeof(TacticalShooterPlayer))]
    public class WeaponHitDetector : MonoBehaviour
    {
        #region Settings

        [Tooltip("Layers the raycast can hit (enemies, environment, etc). Exclude the player layer.")]
        [SerializeField] private LayerMask hitLayers = ~0;

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
            WeaponCombatData data = weapon.combatData;

            if (data == null)
            {
                Debug.LogWarning($"[WeaponHitDetector] {weapon.name} has no WeaponCombatData assigned.");
                return;
            }

            if (data.pelletCount > 1)
                FireMultiPellet(weapon, data);
            else
                FireSingleRaycast(weapon, data);

            EmitGunshotNoise(weapon, data);
        }

        private void FireSingleRaycast(TacticalShooterWeapon weapon, WeaponCombatData data)
        {
            Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);
            Vector3 endPoint;

            if (Physics.Raycast(ray, out RaycastHit hit, data.range, hitLayers,
                    QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
                ProcessHit(hit, data.damage, data);

                if (showDebugRays)
                    Debug.DrawLine(ray.origin, hit.point, Color.red, 1f);
            }
            else
            {
                endPoint = ray.origin + ray.direction * data.range;

                if (showDebugRays)
                    Debug.DrawRay(ray.origin, ray.direction * data.range, Color.yellow, 1f);
            }

            if (!_player.IsAiming)
                SpawnTrail(weapon.GetMuzzlePosition(), endPoint, data);
        }

        private void FireMultiPellet(TacticalShooterWeapon weapon, WeaponCombatData data)
        {
            float damagePerPellet = data.damage / data.pelletCount;
            Vector3 muzzle = weapon.GetMuzzlePosition();

            for (int i = 0; i < data.pelletCount; i++)
            {
                Vector2 spread = Random.insideUnitCircle * data.spreadAngle;
                Vector3 direction = Quaternion.Euler(spread.x, spread.y, 0f)
                                    * _camera.transform.forward;

                Ray ray = new Ray(_camera.transform.position, direction);
                Vector3 endPoint;

                if (Physics.Raycast(ray, out RaycastHit hit, data.range, hitLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    endPoint = hit.point;
                    ProcessHit(hit, damagePerPellet, data);

                    if (showDebugRays)
                        Debug.DrawLine(ray.origin, hit.point, Color.red, 1f);
                }
                else
                {
                    endPoint = ray.origin + direction * data.range;

                    if (showDebugRays)
                        Debug.DrawRay(ray.origin, direction * data.range, Color.yellow, 1f);
                }

                if (!_player.IsAiming)
                    SpawnTrail(muzzle, endPoint, data);
            }
        }

        #endregion

        #region Hit Processing

        private void ProcessHit(RaycastHit hit, float damage, WeaponCombatData data)
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
            else if (data.impactEffectPrefab != null)
            {
                GameObject fx = Instantiate(data.impactEffectPrefab, hit.point,
                    Quaternion.LookRotation(hit.normal));

                if (data.impactEffectLifetime > 0f)
                    Destroy(fx, data.impactEffectLifetime);
            }
        }

        #endregion

        #region Visual Feedback

        private void SpawnTrail(Vector3 start, Vector3 end, WeaponCombatData data)
        {
            if (data.trailPrefab == null) return;

            GameObject trail = Instantiate(data.trailPrefab, start, Quaternion.identity);
            BulletTrailMover mover = trail.GetComponent<BulletTrailMover>();

            if (mover != null)
            {
                mover.Initialise(start, end, data.trailSpeed);
            }
        }

        #endregion

        #region Noise

        private void EmitGunshotNoise(TacticalShooterWeapon weapon, WeaponCombatData data)
        {
            if (NoiseManager.Instance == null) return;

            Vector3 noisePosition = weapon.GetMuzzlePosition();
            NoiseManager.Instance.EmitNoise(noisePosition, data.fireNoiseLevel, NoiseCategory.Gunshot);
        }

        #endregion
    }
}
