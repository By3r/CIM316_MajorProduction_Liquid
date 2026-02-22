using Liquid.Audio;
using UnityEngine;

namespace _Scripts.Systems.Weapon
{
    /// <summary>
    /// Melee weapon viewmodel. Uses OverlapSphere + angle check for hit detection,
    /// similar to how enemies check their own attack range.
    ///
    /// Attach to a weapon viewmodel prefab alongside an Animator.
    /// Animation Events on clips (or fallback timers if no events are set up):
    ///   - MeleeAttack at damage frame -> OnMeleeDamageFrame()
    ///   - MeleeAttack clip end -> OnMeleeSwingComplete()
    ///   - Draw clip end -> OnDrawComplete() (inherited)
    ///   - Holster clip end -> OnHolsterComplete() (inherited)
    /// </summary>
    public class MeleeWeapon : WeaponBase
    {
        #region Runtime State

        private float _nextAttackTime;

        #endregion

        #region WeaponBase Overrides

        /// <summary>
        /// For melee weapons, the fire button triggers a melee swing.
        /// </summary>
        public override bool TryFire()
        {
            return TryMelee();
        }

        /// <summary>
        /// Melee weapons don't reload.
        /// </summary>
        public override bool TryReload()
        {
            return false;
        }

        /// <summary>
        /// Attempts a melee swing. Checks state and cooldown.
        /// Damage is applied when the animation event (or fallback timer) calls OnMeleeDamageFrame().
        /// </summary>
        public override bool TryMelee()
        {
            if (_currentState != WeaponState.Idle && _currentState != WeaponState.Aiming) return false;
            if (Time.time < _nextAttackTime) return false;

            _currentState = WeaponState.MeleeSwing;
            _nextAttackTime = Time.time + _weaponData.meleeCooldown;
            TriggerMelee();

            return true;
        }

        #endregion

        #region Fallback Timer Overrides

        /// <summary>
        /// Called by the fallback timer at 50% of the melee swing duration.
        /// Performs the damage check when no Animation Event is set up.
        /// </summary>
        protected override void OnFallbackMeleeDamageFrame()
        {
            OnMeleeDamageFrame();
        }

        /// <summary>
        /// Called by the fallback timer at the end of the melee swing duration.
        /// Completes the swing when no Animation Event is set up.
        /// </summary>
        protected override void OnFallbackMeleeSwingComplete()
        {
            OnMeleeSwingComplete();
        }

        #endregion

        #region Animation Event Callbacks

        /// <summary>
        /// Called by an Animation Event at the apex of the swing animation (the damage frame).
        /// Performs an OverlapSphere from the camera position and damages enemies within
        /// the swing arc (defined by meleeRange and meleeAngle in WeaponDataSO).
        /// Also emits a noise event through NoiseManager.
        /// </summary>
        public void OnMeleeDamageFrame()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 origin = cam.transform.position;
            Vector3 forward = cam.transform.forward;

            Collider[] hits = Physics.OverlapSphere(origin, _weaponData.meleeRange);

            foreach (Collider col in hits)
            {
                // Skip self (player)
                if (col.transform.root == transform.root) continue;

                // Angle check: is this collider within the swing arc?
                Vector3 toTarget = col.ClosestPoint(origin) - origin;
                float angle = Vector3.Angle(forward, toTarget);
                if (angle > _weaponData.meleeAngle) continue;

                // Check for enemy
                EnemyBase enemy = col.GetComponent<EnemyBase>();
                if (enemy == null)
                    enemy = col.GetComponentInParent<EnemyBase>();

                if (enemy != null)
                {
                    enemy.TakeDamage(_weaponData.meleeDamage);
                }
            }

            // Emit melee noise (quieter than gunshots)
            if (NoiseManager.Instance != null)
            {
                NoiseManager.Instance.EmitNoise(
                    origin,
                    _weaponData.meleeNoiseLevel,
                    NoiseCategory.ObjectImpact
                );
            }
        }

        /// <summary>
        /// Called by an Animation Event at the end of the melee swing clip.
        /// Transitions back to Idle state (or Aiming if WeaponManager says we're still aiming).
        /// </summary>
        public void OnMeleeSwingComplete()
        {
            if (_currentState != WeaponState.MeleeSwing) return; // Already completed

            WeaponManager manager = GetComponentInParent<WeaponManager>();
            if (manager == null)
                manager = transform.root.GetComponent<WeaponManager>();

            if (manager != null && manager.IsAiming)
            {
                _currentState = WeaponState.Aiming;
            }
            else
            {
                _currentState = WeaponState.Idle;
            }
        }

        #endregion
    }
}
