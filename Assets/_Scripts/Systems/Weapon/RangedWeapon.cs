using _Scripts.Core.Managers;
using Liquid.Audio;
using UnityEngine;

namespace _Scripts.Systems.Weapon
{
    /// <summary>
    /// Ranged weapon viewmodel. Performs raycast hits from the camera center,
    /// tracks ammo, handles fire rate limiting, emits gunshot noise,
    /// and spawns its own bullet trails and impact effects.
    ///
    /// All VFX configuration (trail material, impact prefabs) comes from WeaponDataSO,
    /// so each weapon can have unique visual feedback without shared global settings.
    ///
    /// Attach to a weapon viewmodel prefab alongside an Animator.
    /// Requires Animation Events on clips:
    ///   - Fire clip end -> OnFireComplete() (inherited from WeaponBase)
    ///   - Reload at ammo-refill frame -> OnReloadAmmoRefill()
    ///   - Reload clip end -> OnReloadComplete() (inherited from WeaponBase)
    ///   - Draw clip end -> OnDrawComplete() (inherited)
    ///   - Holster clip end -> OnHolsterComplete() (inherited)
    ///
    /// Optional child objects on the prefab:
    ///   - "MuzzlePoint" (Transform) — bullet trail origin. Falls back to camera if missing.
    /// </summary>
    public class RangedWeapon : WeaponBase
    {
        #region Runtime State

        private int _currentAmmo;
        private float _nextFireTime;
        private Transform _muzzlePoint;

        #endregion

        #region Properties

        /// <summary>Current rounds remaining in the magazine.</summary>
        public int CurrentAmmo => _currentAmmo;

        /// <summary>Maximum magazine capacity from weapon data.</summary>
        public int MaxAmmo => _weaponData != null ? _weaponData.magazineSize : 0;

        /// <summary>True if the magazine is empty.</summary>
        public bool IsEmpty => _currentAmmo <= 0;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            _currentAmmo = _weaponData != null ? _weaponData.magazineSize : 0;

            // Cache muzzle point for bullet trail origin
            _muzzlePoint = transform.Find("MuzzlePoint");
        }

        #endregion

        #region WeaponBase Overrides

        /// <summary>
        /// Attempts to fire the weapon. Checks state, fire rate cooldown, and ammo.
        /// On success: decrements ammo, triggers fire animation, performs raycast, emits noise.
        /// Can fire from both Idle and Aiming states.
        /// </summary>
        public override bool TryFire()
        {
            // Allow firing from Idle or Aiming states
            if (_currentState != WeaponState.Idle && _currentState != WeaponState.Aiming) return false;
            if (Time.time < _nextFireTime) return false;
            if (_currentAmmo <= 0) return false;

            _currentState = WeaponState.Firing;
            _currentAmmo--;
            _nextFireTime = Time.time + _weaponData.fireRate;

            TriggerFire();
            PerformRaycast();
            EmitGunshotNoise();

            return true;
        }

        /// <summary>
        /// Attempts to reload the weapon. Checks state and whether magazine is already full.
        /// Ammo is actually refilled when the animation event calls OnReloadAmmoRefill().
        /// Can reload from both Idle and Aiming states.
        /// </summary>
        public override bool TryReload()
        {
            if (_currentState != WeaponState.Idle && _currentState != WeaponState.Aiming) return false;
            if (_currentAmmo >= _weaponData.magazineSize) return false;

            _currentState = WeaponState.Reloading;
            TriggerReload();
            return true;
        }

        /// <summary>
        /// Ranged weapons don't support melee attacks (could add gun-butt in the future).
        /// </summary>
        public override bool TryMelee()
        {
            return false;
        }

        #endregion

        #region Animation Event Callbacks

        /// <summary>
        /// Called by an Animation Event on the Reload clip at the frame where
        /// the magazine visually enters the weapon. Refills ammo to full.
        /// </summary>
        public void OnReloadAmmoRefill()
        {
            _currentAmmo = _weaponData.magazineSize;
        }

        /// <summary>
        /// Called by the fallback timer at 75% of the reload duration.
        /// Refills ammo when no Animation Event is set up.
        /// </summary>
        protected override void OnFallbackReloadAmmoRefill()
        {
            OnReloadAmmoRefill();
        }

        #endregion

        #region Raycast & Damage

        /// <summary>
        /// Performs a raycast from the camera center forward.
        /// If it hits an EnemyBase, calls TakeDamage with weapon damage.
        /// Spawns bullet trail and impact effects using data from WeaponDataSO.
        /// Also publishes the OnWeaponFired game event.
        /// </summary>
        private void PerformRaycast()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            Vector3 endPoint;

            if (Physics.Raycast(ray, out RaycastHit hit, _weaponData.range))
            {
                endPoint = hit.point;

                // Check for enemy on the hit object or its parents
                EnemyBase enemy = hit.collider.GetComponent<EnemyBase>();
                if (enemy == null)
                    enemy = hit.collider.GetComponentInParent<EnemyBase>();

                if (enemy != null)
                {
                    enemy.TakeDamage(_weaponData.damage);
                }
                else
                {
                    // Hit a non-enemy surface -> spawn impact effect
                    SpawnImpactEffect(hit.point, hit.normal);
                }
            }
            else
            {
                // No hit -> trail goes to max range
                endPoint = ray.GetPoint(_weaponData.range);
            }

            // Spawn bullet trail from muzzle to end point
            SpawnBulletTrail(endPoint);

            // Publish weapon fired event for UI, analytics, etc.
            GameManager.Instance?.EventManager?.Publish(GameEvents.OnWeaponFired);
        }

        #endregion

        #region Bullet Trail VFX

        /// <summary>
        /// Spawns a bullet trail (thin LineRenderer) from the muzzle to the end point.
        /// Uses trail configuration from this weapon's WeaponDataSO.
        /// The trail self-destructs after trailDuration seconds.
        /// </summary>
        private void SpawnBulletTrail(Vector3 endPoint)
        {
            if (_weaponData.bulletTrailMaterial == null) return;

            Vector3 startPos = GetMuzzleWorldPosition();

            GameObject trailObj = new GameObject("BulletTrail");
            trailObj.transform.position = startPos;

            LineRenderer lr = trailObj.AddComponent<LineRenderer>();
            lr.material = _weaponData.bulletTrailMaterial;
            lr.startColor = _weaponData.trailColor;
            lr.endColor = new Color(_weaponData.trailColor.r, _weaponData.trailColor.g, _weaponData.trailColor.b, 0f);
            lr.startWidth = _weaponData.trailStartWidth;
            lr.endWidth = _weaponData.trailEndWidth;
            lr.positionCount = 2;
            lr.SetPosition(0, startPos);
            lr.SetPosition(1, endPoint);
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            Object.Destroy(trailObj, _weaponData.trailDuration);
        }

        /// <summary>
        /// Gets the world position of the muzzle point (barrel end).
        /// Falls back to camera forward offset if no MuzzlePoint child exists on the prefab.
        /// </summary>
        private Vector3 GetMuzzleWorldPosition()
        {
            if (_muzzlePoint != null)
            {
                return _muzzlePoint.position;
            }

            // Fallback: camera position + small forward offset
            Camera cam = Camera.main;
            if (cam != null)
            {
                return cam.transform.position + cam.transform.forward * 0.5f;
            }

            return transform.position;
        }

        #endregion

        #region Impact Effects

        /// <summary>
        /// Spawns impact VFX (particle effect + optional bullet hole decal) at a hit point.
        /// Uses prefab references from this weapon's WeaponDataSO.
        /// The particles orient to the surface normal.
        /// </summary>
        private void SpawnImpactEffect(Vector3 hitPoint, Vector3 hitNormal)
        {
            // Spawn particle effect
            if (_weaponData.impactEffectPrefab != null)
            {
                Quaternion rotation = Quaternion.LookRotation(hitNormal);
                GameObject impact = Instantiate(_weaponData.impactEffectPrefab, hitPoint, rotation);
                Destroy(impact, _weaponData.impactEffectLifetime);
            }

            // Spawn bullet hole decal
            if (_weaponData.bulletHoleDecalPrefab != null)
            {
                // Offset slightly from surface to avoid z-fighting
                Vector3 decalPos = hitPoint + hitNormal * 0.01f;
                Quaternion decalRot = Quaternion.LookRotation(-hitNormal);
                GameObject decal = Instantiate(_weaponData.bulletHoleDecalPrefab, decalPos, decalRot);
                Destroy(decal, _weaponData.decalLifetime);
            }
        }

        #endregion

        #region Noise

        /// <summary>
        /// Emits a gunshot noise through NoiseManager.
        /// Enemies with hearing (INoiseListener) will react to this.
        /// The Gunshot category already has a 2x multiplier in the noise system.
        /// </summary>
        private void EmitGunshotNoise()
        {
            if (NoiseManager.Instance != null)
            {
                NoiseManager.Instance.EmitNoise(
                    transform.position,
                    _weaponData.fireNoiseLevel,
                    NoiseCategory.Gunshot
                );
            }
        }

        #endregion
    }
}
