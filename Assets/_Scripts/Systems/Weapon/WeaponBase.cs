using System;
using UnityEngine;

namespace _Scripts.Systems.Weapon
{
    /// <summary>
    /// Abstract base class for all viewmodel weapons. Attach to the root of a weapon prefab.
    /// Manages the Animator state machine, weapon state tracking, and exposes events for WeaponManager.
    /// Subclasses (RangedWeapon, MeleeWeapon) implement the actual fire/reload/melee logic.
    ///
    /// Supports two modes:
    ///   1. Animation-driven: Animation Events on clips call OnDrawComplete(), OnFireComplete(), etc.
    ///   2. Timer fallback: If no Animation Event fires within the fallback duration, the state
    ///      auto-completes. This lets you test gameplay before setting up Animator Controllers.
    ///
    /// To use animation-driven mode: set up Animator Controller with trigger parameters
    /// (Fire, Reload, Draw, Holster, MeleeAttack) and add Animation Events on clip end frames.
    ///
    /// To use timer fallback mode: leave the Animator Controller empty or don't add events.
    /// The weapon will still cycle states correctly using the fallback durations.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public abstract class WeaponBase : MonoBehaviour
    {
        #region Serialized Fields

        [Header("-- Weapon Data --")]
        [Tooltip("ScriptableObject containing all stats for this weapon.")]
        [SerializeField] protected WeaponDataSO _weaponData;

        [Header("-- Fallback Timers --")]
        [Tooltip("If true, use timer-based state transitions when Animation Events don't fire. " +
                 "Disable this once you have proper Animator Controllers with Animation Events set up.")]
        [SerializeField] private bool _useFallbackTimers = true;

        [Tooltip("Fallback duration for the draw animation (seconds).")]
        [SerializeField] private float _fallbackDrawTime = 0.4f;

        [Tooltip("Fallback duration for the holster animation (seconds).")]
        [SerializeField] private float _fallbackHolsterTime = 0.3f;

        [Tooltip("Fallback duration for the fire animation (seconds).")]
        [SerializeField] private float _fallbackFireTime = 0.15f;

        [Tooltip("Fallback duration for the reload animation (seconds). " +
                 "Ammo refill happens at 75% of this duration.")]
        [SerializeField] private float _fallbackReloadTime = 2f;

        [Tooltip("Fallback duration for the melee swing animation (seconds). " +
                 "Damage frame happens at 50% of this duration.")]
        [SerializeField] private float _fallbackMeleeTime = 0.4f;

        #endregion

        #region Cached References

        protected Animator _animator;

        #endregion

        #region State

        protected WeaponState _currentState = WeaponState.Inactive;

        // Fallback timer tracking
        private float _stateTimer;
        private float _stateTargetTime;
        private bool _animEventFired;            // true if an Animation Event already completed this state
        private bool _reloadAmmoRefillDone;      // tracks whether the mid-reload refill happened
        private bool _meleeDamageFrameDone;      // tracks whether the mid-swing damage happened

        #endregion

        #region Animator Parameter Hashes

        protected static readonly int AnimFire = Animator.StringToHash("Fire");
        protected static readonly int AnimReload = Animator.StringToHash("Reload");
        protected static readonly int AnimDraw = Animator.StringToHash("Draw");
        protected static readonly int AnimHolster = Animator.StringToHash("Holster");
        protected static readonly int AnimMelee = Animator.StringToHash("MeleeAttack");
        protected static readonly int AnimSprinting = Animator.StringToHash("Sprinting");

        #endregion

        #region Events

        /// <summary>Fired when the draw animation completes. WeaponManager listens to know weapon is ready.</summary>
        public event Action OnDrawFinished;

        /// <summary>Fired when the holster animation completes. WeaponManager listens to deactivate/switch.</summary>
        public event Action OnHolsterFinished;

        /// <summary>Fired when the fire animation completes.</summary>
        public event Action OnFireFinished;

        /// <summary>Fired when the reload animation completes.</summary>
        public event Action OnReloadFinished;

        #endregion

        #region Properties

        /// <summary>Gets the weapon data ScriptableObject for this weapon.</summary>
        public WeaponDataSO WeaponData => _weaponData;

        /// <summary>Gets the current state of this weapon's state machine.</summary>
        public WeaponState CurrentState => _currentState;

        /// <summary>True if the weapon is in any busy state (animating, not ready for new input).</summary>
        public bool IsBusy => _currentState != WeaponState.Idle
                           && _currentState != WeaponState.Inactive
                           && _currentState != WeaponState.Aiming;

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        protected virtual void Update()
        {
            if (!_useFallbackTimers) return;
            if (_animEventFired) return; // Animation Event already handled this state

            UpdateFallbackTimer();
        }

        #endregion

        #region Fallback Timer

        /// <summary>
        /// Ticks the fallback timer. If the state has been active longer than the target time
        /// and no Animation Event has fired, auto-complete the state transition.
        /// This ensures weapons work even without Animator Controllers or Animation Events.
        /// </summary>
        private void UpdateFallbackTimer()
        {
            if (_stateTargetTime <= 0f) return;

            _stateTimer += Time.deltaTime;

            switch (_currentState)
            {
                case WeaponState.Drawing:
                    if (_stateTimer >= _stateTargetTime)
                    {
                        OnDrawComplete();
                    }
                    break;

                case WeaponState.Holstering:
                    if (_stateTimer >= _stateTargetTime)
                    {
                        OnHolsterComplete();
                    }
                    break;

                case WeaponState.Firing:
                    if (_stateTimer >= _stateTargetTime)
                    {
                        OnFireComplete();
                    }
                    break;

                case WeaponState.Reloading:
                    // Trigger ammo refill at 75% through the reload
                    if (!_reloadAmmoRefillDone && _stateTimer >= _stateTargetTime * 0.75f)
                    {
                        _reloadAmmoRefillDone = true;
                        OnFallbackReloadAmmoRefill();
                    }
                    if (_stateTimer >= _stateTargetTime)
                    {
                        OnReloadComplete();
                    }
                    break;

                case WeaponState.MeleeSwing:
                    // Trigger damage frame at 50% through the swing
                    if (!_meleeDamageFrameDone && _stateTimer >= _stateTargetTime * 0.5f)
                    {
                        _meleeDamageFrameDone = true;
                        OnFallbackMeleeDamageFrame();
                    }
                    if (_stateTimer >= _stateTargetTime)
                    {
                        OnFallbackMeleeSwingComplete();
                    }
                    break;
            }
        }

        /// <summary>
        /// Starts the fallback timer for the current state.
        /// Called internally when entering a timed state (draw, holster, fire, reload, melee).
        /// </summary>
        private void StartFallbackTimer(float duration)
        {
            _stateTimer = 0f;
            _stateTargetTime = duration;
            _animEventFired = false;
            _reloadAmmoRefillDone = false;
            _meleeDamageFrameDone = false;
        }

        /// <summary>
        /// Called by subclasses to trigger the mid-reload ammo refill during timer fallback.
        /// Override in RangedWeapon. Default does nothing.
        /// </summary>
        protected virtual void OnFallbackReloadAmmoRefill() { }

        /// <summary>
        /// Called by the fallback timer at the damage frame of a melee swing.
        /// Override in MeleeWeapon. Default does nothing.
        /// </summary>
        protected virtual void OnFallbackMeleeDamageFrame() { }

        /// <summary>
        /// Called by the fallback timer at the end of a melee swing.
        /// Override in MeleeWeapon. Default does nothing.
        /// </summary>
        protected virtual void OnFallbackMeleeSwingComplete() { }

        #endregion

        #region Public API -- Called by WeaponManager

        /// <summary>
        /// Starts the draw animation. Transitions state to Drawing.
        /// When the draw animation clip ends, it must call OnDrawComplete() via an Animation Event.
        /// If no event fires, the fallback timer will auto-complete after _fallbackDrawTime.
        /// </summary>
        public virtual void StartDraw()
        {
            _currentState = WeaponState.Drawing;
            StartFallbackTimer(_fallbackDrawTime);

            if (HasAnimatorParameter(AnimDraw))
            {
                _animator.SetTrigger(AnimDraw);
            }
        }

        /// <summary>
        /// Starts the holster animation. Transitions state to Holstering.
        /// When the holster animation clip ends, it must call OnHolsterComplete() via an Animation Event.
        /// If no event fires, the fallback timer will auto-complete after _fallbackHolsterTime.
        /// </summary>
        public virtual void StartHolster()
        {
            _currentState = WeaponState.Holstering;
            StartFallbackTimer(_fallbackHolsterTime);

            if (HasAnimatorParameter(AnimHolster))
            {
                _animator.SetTrigger(AnimHolster);
            }
        }

        /// <summary>
        /// Sets or clears the Aiming state. Called by WeaponManager when ADS is toggled.
        /// Only works when in Idle state (to enter aim) or Aiming state (to exit aim).
        /// </summary>
        public virtual void SetAiming(bool aiming)
        {
            if (aiming && _currentState == WeaponState.Idle)
            {
                _currentState = WeaponState.Aiming;
            }
            else if (!aiming && _currentState == WeaponState.Aiming)
            {
                _currentState = WeaponState.Idle;
            }
        }

        /// <summary>
        /// Sets or clears the Sprinting bool on the Animator.
        /// Called by WeaponManager each frame so the weapon can play sprint animations.
        /// Does not affect weapon state — sprint blocking is handled by WeaponManager.
        /// </summary>
        public virtual void SetSprinting(bool sprinting)
        {
            if (HasAnimatorParameter(AnimSprinting))
            {
                _animator.SetBool(AnimSprinting, sprinting);
            }
        }

        /// <summary>
        /// Attempts to fire the weapon. Returns false if the weapon can't fire
        /// (wrong state, no ammo, cooldown, etc.). Implemented by subclasses.
        /// </summary>
        public abstract bool TryFire();

        /// <summary>
        /// Attempts to reload the weapon. Returns false if reload isn't possible
        /// (wrong state, already full, melee weapon, etc.). Implemented by subclasses.
        /// </summary>
        public abstract bool TryReload();

        /// <summary>
        /// Attempts a melee attack. Returns false if not possible.
        /// For ranged weapons this typically returns false. Implemented by subclasses.
        /// </summary>
        public abstract bool TryMelee();

        /// <summary>
        /// Called every frame by WeaponManager while this weapon is the active weapon.
        /// Override for per-frame logic (e.g., charge weapons, continuous effects).
        /// </summary>
        public virtual void Tick() { }

        #endregion

        #region Helper — Fire/Reload Trigger with Fallback

        /// <summary>
        /// Sets a fire trigger on the animator and starts the fallback timer.
        /// Call this from subclass TryFire() instead of _animator.SetTrigger(AnimFire) directly.
        /// </summary>
        protected void TriggerFire()
        {
            StartFallbackTimer(_fallbackFireTime);

            if (HasAnimatorParameter(AnimFire))
            {
                _animator.SetTrigger(AnimFire);
            }
        }

        /// <summary>
        /// Sets a reload trigger on the animator and starts the fallback timer.
        /// Call this from subclass TryReload() instead of _animator.SetTrigger(AnimReload) directly.
        /// </summary>
        protected void TriggerReload()
        {
            StartFallbackTimer(_fallbackReloadTime);

            if (HasAnimatorParameter(AnimReload))
            {
                _animator.SetTrigger(AnimReload);
            }
        }

        /// <summary>
        /// Sets a melee trigger on the animator and starts the fallback timer.
        /// Call this from subclass TryMelee() instead of _animator.SetTrigger(AnimMelee) directly.
        /// </summary>
        protected void TriggerMelee()
        {
            StartFallbackTimer(_fallbackMeleeTime);

            if (HasAnimatorParameter(AnimMelee))
            {
                _animator.SetTrigger(AnimMelee);
            }
        }

        /// <summary>
        /// Checks if the Animator has a parameter with the given hash.
        /// Prevents "Parameter does not exist" warnings when no Animator Controller is assigned.
        /// </summary>
        private bool HasAnimatorParameter(int paramHash)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return false;

            foreach (AnimatorControllerParameter param in _animator.parameters)
            {
                if (param.nameHash == paramHash) return true;
            }

            return false;
        }

        #endregion

        #region Animation Event Callbacks

        /// <summary>
        /// Called by an Animation Event on the Draw clip's last frame.
        /// Transitions to Idle state and notifies WeaponManager.
        /// Also prevents the fallback timer from double-firing.
        /// </summary>
        public void OnDrawComplete()
        {
            if (_currentState != WeaponState.Drawing) return; // Already completed (prevent double-fire)
            _animEventFired = true;

            _currentState = WeaponState.Idle;
            OnDrawFinished?.Invoke();
        }

        /// <summary>
        /// Called by an Animation Event on the Holster clip's last frame.
        /// Transitions to Inactive state and notifies WeaponManager.
        /// </summary>
        public void OnHolsterComplete()
        {
            if (_currentState != WeaponState.Holstering) return;
            _animEventFired = true;

            _currentState = WeaponState.Inactive;
            OnHolsterFinished?.Invoke();
        }

        /// <summary>
        /// Called by an Animation Event on the Fire clip's last frame.
        /// Transitions back to Idle state (or Aiming if WeaponManager says we're still aiming).
        /// </summary>
        public void OnFireComplete()
        {
            if (_currentState != WeaponState.Firing) return;
            _animEventFired = true;

            _currentState = GetReturnState();
            OnFireFinished?.Invoke();
        }

        /// <summary>
        /// Called by an Animation Event on the Reload clip's last frame.
        /// Transitions back to Idle state (or Aiming if still aiming).
        /// </summary>
        public void OnReloadComplete()
        {
            if (_currentState != WeaponState.Reloading) return;
            _animEventFired = true;

            _currentState = GetReturnState();
            OnReloadFinished?.Invoke();
        }

        /// <summary>
        /// Returns Aiming if the WeaponManager says we're still aiming, otherwise Idle.
        /// Used by OnFireComplete and OnReloadComplete to return to the correct state.
        /// </summary>
        private WeaponState GetReturnState()
        {
            WeaponManager manager = GetComponentInParent<WeaponManager>();
            if (manager == null)
                manager = transform.root.GetComponent<WeaponManager>();

            if (manager != null && manager.IsAiming)
            {
                return WeaponState.Aiming;
            }

            return WeaponState.Idle;
        }

        #endregion
    }
}
