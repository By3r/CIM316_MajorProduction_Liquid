using System.Collections.Generic;
using _Scripts.Core.Managers;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Player;
using UnityEngine;

namespace _Scripts.Systems.Weapon
{
    /// <summary>
    /// Central orchestrator for the weapon system. Placed on the Player GameObject.
    /// Handles ONLY input routing, weapon lifecycle (holster/draw/switch), and auto-draw.
    ///
    /// Does NOT handle:
    ///   - Viewmodel visual motion (sway, bob, ADS positioning) -> ViewmodelMotion
    ///   - Bullet trails and impact effects -> RangedWeapon (reads config from WeaponDataSO)
    ///   - Weapon stats -> WeaponDataSO
    ///
    /// The viewmodel hierarchy at runtime:
    ///   PlayerCamera
    ///     +-- ViewmodelRoot (ViewmodelMotion component)
    ///           +-- [Active Weapon Prefab] (Animator + RangedWeapon/MeleeWeapon)
    ///
    /// Weapon switching flow:
    ///   1. Player selects weapon from wheel -> OnWeaponSelectedFromWheel(InventoryItemData)
    ///   2. If a weapon is equipped: play Holster animation -> wait for OnHolsterFinished
    ///   3. Deactivate old viewmodel -> instantiate/activate new -> play Draw animation
    ///   4. Wait for OnDrawFinished -> weapon enters Idle state, ready for input
    ///
    /// Auto-draw: If the weapon is holstered and the player presses fire, reload, or aim,
    /// the weapon automatically draws before performing the action.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        #region Serialized Fields

        [Header("-- References --")]
        [Tooltip("The camera transform. ViewmodelRoot will be created as a child of this.")]
        [SerializeField] private Transform _cameraTransform;

        [Header("-- Weapon Database --")]
        [Tooltip("All WeaponDataSO assets in the game. Used to look up weapon data from InventoryItemData.")]
        [SerializeField] private List<WeaponDataSO> _weaponDatabase = new List<WeaponDataSO>();

        [Header("-- Holster Input --")]
        [Tooltip("Key to holster or unholster the current weapon.")]
        [SerializeField] private KeyCode _holsterKey = KeyCode.H;

        [Header("-- Sprint Behavior --")]
        [Tooltip("If true, the player can fire, reload, and aim while sprinting. " +
                 "Intended as a future upgrade/ability unlock.")]
        [SerializeField] private bool _canUseWeaponWhileSprinting = false;

        [Header("-- Debug --")]
        [SerializeField] private bool _showDebugLogs = false;

        #endregion

        #region Private Fields

        private Transform _viewmodelRoot;
        private ViewmodelMotion _viewmodelMotion;
        private WeaponBase _currentWeapon;
        private WeaponDataSO _currentWeaponData;
        private WeaponDataSO _pendingWeaponData;      // weapon to switch TO after holster completes
        private bool _isHolstered;                     // true when player explicitly holstered
        private bool _isSwitching;                     // true during holster->draw transition
        private bool _isAiming;                        // true when player is holding aim
        private List<WeaponDataSO> _wheelWeapons;      // ordered list of weapons currently in the wheel

        // Cached reference
        private MovementController _movementController;

        // Lookup: InventoryItemData -> WeaponDataSO
        private Dictionary<InventoryItemData, WeaponDataSO> _itemToWeaponMap;

        // Auto-draw queued action after draw completes
        private enum QueuedAction { None, Fire, Reload, Aim }
        private QueuedAction _queuedAction;

        #endregion

        #region Properties

        /// <summary>The currently equipped weapon instance (null if none).</summary>
        public WeaponBase CurrentWeapon => _currentWeapon;

        /// <summary>The data for the currently equipped weapon (null if none).</summary>
        public WeaponDataSO CurrentWeaponData => _currentWeaponData;

        /// <summary>True if the player has explicitly holstered their weapon.</summary>
        public bool IsHolstered => _isHolstered;

        /// <summary>True if a weapon is equipped and not holstered.</summary>
        public bool HasWeaponEquipped => _currentWeapon != null && !_isHolstered;

        /// <summary>True if currently in the middle of a weapon switch animation.</summary>
        public bool IsSwitching => _isSwitching;

        /// <summary>True if the player is aiming down sights.</summary>
        public bool IsAiming => _isAiming;

        /// <summary>The ViewmodelMotion component on the ViewmodelRoot.</summary>
        public ViewmodelMotion ViewmodelMotion => _viewmodelMotion;

        /// <summary>
        /// When true, the player can fire, reload, and aim while sprinting.
        /// Set this from an upgrade/ability system to unlock the behavior.
        /// </summary>
        public bool CanUseWeaponWhileSprinting
        {
            get => _canUseWeaponWhileSprinting;
            set => _canUseWeaponWhileSprinting = value;
        }

        #endregion

        #region Initialization

        private void Awake()
        {
            _movementController = GetComponent<MovementController>();
            BuildItemToWeaponMap();
            CreateViewmodelRoot();
        }

        /// <summary>
        /// Builds a dictionary mapping InventoryItemData -> WeaponDataSO for fast lookup
        /// when the weapon wheel fires a selection event with an InventoryItemData reference.
        /// </summary>
        private void BuildItemToWeaponMap()
        {
            _itemToWeaponMap = new Dictionary<InventoryItemData, WeaponDataSO>();

            foreach (WeaponDataSO data in _weaponDatabase)
            {
                if (data != null && data.inventoryItem != null)
                {
                    _itemToWeaponMap[data.inventoryItem] = data;
                }
            }

            if (_showDebugLogs)
            {
                Debug.Log($"[WeaponManager] Built item-to-weapon map: {_itemToWeaponMap.Count} weapons registered.");
            }
        }

        /// <summary>
        /// Creates the ViewmodelRoot empty Transform as a child of the camera.
        /// Adds a ViewmodelMotion component and initializes it with player references.
        /// All weapon viewmodel prefabs are instantiated under this root.
        /// </summary>
        private void CreateViewmodelRoot()
        {
            if (_cameraTransform == null)
            {
                _cameraTransform = transform.Find("PlayerCamera");
            }

            if (_cameraTransform != null)
            {
                GameObject rootObj = new GameObject("ViewmodelRoot");
                rootObj.transform.SetParent(_cameraTransform, false);
                rootObj.transform.localPosition = Vector3.zero;
                rootObj.transform.localRotation = Quaternion.identity;
                _viewmodelRoot = rootObj.transform;

                // Add ViewmodelMotion to handle sway, bob, and ADS positioning
                _viewmodelMotion = rootObj.AddComponent<ViewmodelMotion>();

                MovementController movementController = GetComponent<MovementController>();
                Camera playerCamera = _cameraTransform.GetComponent<Camera>();
                _viewmodelMotion.Initialize(movementController, playerCamera);
            }
            else
            {
                Debug.LogWarning("[WeaponManager] Camera transform not found! Viewmodel will not be parented correctly.");
            }
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            // Block all weapon input during switch transitions
            if (_isSwitching) return;

            bool isSprinting = _movementController != null && _movementController.IsSprinting;

            // Sync sprint animation on the weapon every frame
            _currentWeapon?.SetSprinting(isSprinting);

            // If sprinting and the ability isn't unlocked, cancel ADS and block weapon actions
            if (isSprinting && !_canUseWeaponWhileSprinting)
            {
                if (_isAiming) SetAiming(false);

                // Still allow holster/switch while sprinting
                HandleScrollSwitchInput();
                HandleHolsterInput();

                _currentWeapon?.Tick();
                return;
            }

            HandleFireInput();
            HandleReloadInput();
            HandleAimInput();
            HandleScrollSwitchInput();
            HandleHolsterInput();

            // Let the weapon do per-frame work (e.g., charge effects)
            _currentWeapon?.Tick();
        }

        #endregion

        #region Input Handling

        private void HandleFireInput()
        {
            if (_currentWeaponData == null) return;

            bool shouldFire = _currentWeaponData.isAutomatic
                ? InputManager.Instance.FirePressed
                : InputManager.Instance.FireJustPressed;

            if (!shouldFire) return;

            // Auto-draw if holstered
            if (_isHolstered && _currentWeaponData != null)
            {
                AutoDrawWithQueue(QueuedAction.Fire);
                return;
            }

            if (_currentWeapon == null || _isHolstered) return;

            _currentWeapon.TryFire();
        }

        private void HandleReloadInput()
        {
            if (!InputManager.Instance.ReloadPressed) return;

            // Auto-draw if holstered
            if (_isHolstered && _currentWeaponData != null)
            {
                AutoDrawWithQueue(QueuedAction.Reload);
                return;
            }

            if (_currentWeapon == null || _isHolstered) return;

            _currentWeapon.TryReload();
        }

        private void HandleAimInput()
        {
            if (InputManager.Instance.AimJustPressed)
            {
                // Auto-draw if holstered
                if (_isHolstered && _currentWeaponData != null)
                {
                    AutoDrawWithQueue(QueuedAction.Aim);
                    return;
                }

                if (_currentWeapon != null && !_isHolstered)
                {
                    SetAiming(true);
                }
            }

            if (InputManager.Instance.AimJustReleased)
            {
                SetAiming(false);
            }

            // Failsafe: if _isAiming but button no longer held, release
            if (_isAiming && !InputManager.Instance.AimPressed)
            {
                SetAiming(false);
            }
        }

        private void HandleScrollSwitchInput()
        {
            if (_wheelWeapons == null || _wheelWeapons.Count <= 1) return;

            float scroll = InputManager.Instance.SwitchWeaponInput;
            if (Mathf.Abs(scroll) < 0.1f) return;

            // Cancel ADS on weapon switch
            SetAiming(false);

            int currentIndex = _currentWeaponData != null
                ? _wheelWeapons.IndexOf(_currentWeaponData)
                : -1;

            if (currentIndex < 0) currentIndex = 0;

            int direction = scroll > 0 ? 1 : -1;
            int nextIndex = (currentIndex + direction + _wheelWeapons.Count) % _wheelWeapons.Count;

            SwitchToWeapon(_wheelWeapons[nextIndex]);
        }

        private void HandleHolsterInput()
        {
            if (!Input.GetKeyDown(_holsterKey)) return;

            // Cancel ADS on holster
            SetAiming(false);

            if (_isHolstered && _currentWeaponData != null)
            {
                // Un-holster: draw the last weapon
                if (_showDebugLogs) Debug.Log("[WeaponManager] Unholstering weapon.");
                DrawWeapon(_currentWeaponData);
            }
            else if (_currentWeapon != null)
            {
                // Holster the current weapon (no pending switch)
                if (_showDebugLogs) Debug.Log("[WeaponManager] Holstering weapon.");
                HolsterCurrentWeapon(null);
            }
        }

        /// <summary>
        /// Auto-draws the weapon when the player tries to fire/reload/aim while holstered.
        /// Queues the action to be performed after the draw animation finishes.
        /// </summary>
        private void AutoDrawWithQueue(QueuedAction action)
        {
            if (_showDebugLogs)
                Debug.Log($"[WeaponManager] Auto-drawing weapon for queued action: {action}");

            _queuedAction = action;
            DrawWeapon(_currentWeaponData);
        }

        #endregion

        #region ADS State

        /// <summary>
        /// Centralized method to set/clear aim state. Updates both the WeaponBase state
        /// and tells ViewmodelMotion to transition positions.
        /// </summary>
        private void SetAiming(bool aiming)
        {
            _isAiming = aiming;

            // Tell the weapon about aim state (for state gating on fire/reload)
            _currentWeapon?.SetAiming(aiming);

            // Tell ViewmodelMotion to transition positions
            _viewmodelMotion?.SetAiming(aiming);
        }

        #endregion

        #region Weapon Switching -- Public API

        /// <summary>
        /// Called by WeaponWheelController when the player selects a weapon from the wheel.
        /// Accepts the InventoryItemData (from Dana's wheel system) and looks up the
        /// corresponding WeaponDataSO to perform the switch.
        /// </summary>
        public void OnWeaponSelectedFromWheel(InventoryItemData itemData)
        {
            if (itemData == null) return;

            if (!_itemToWeaponMap.TryGetValue(itemData, out WeaponDataSO weaponData))
            {
                if (_showDebugLogs)
                    Debug.Log($"[WeaponManager] Item '{itemData.displayName}' has no matching WeaponDataSO. Ignoring.");
                return;
            }

            // Cancel ADS on weapon switch
            SetAiming(false);

            SwitchToWeapon(weaponData);
        }

        /// <summary>
        /// Switches to a specific weapon. If a weapon is already equipped and active,
        /// holsters it first, then draws the new one.
        /// </summary>
        public void SwitchToWeapon(WeaponDataSO newWeapon)
        {
            if (newWeapon == null) return;

            // Already equipped and not holstered -- nothing to do
            if (newWeapon == _currentWeaponData && !_isHolstered) return;

            if (_showDebugLogs)
            {
                string weaponName = newWeapon.inventoryItem != null ? newWeapon.inventoryItem.displayName : newWeapon.name;
                Debug.Log($"[WeaponManager] Switching to weapon: {weaponName}");
            }

            if (_currentWeapon != null && _currentWeapon.CurrentState != WeaponState.Inactive)
            {
                // Need to holster current weapon first, then draw new one
                HolsterCurrentWeapon(newWeapon);
            }
            else
            {
                // No weapon active, draw directly
                DrawWeapon(newWeapon);
            }
        }

        /// <summary>
        /// Called by WeaponWheelController to sync the ordered list of weapons in the wheel.
        /// Enables scroll-wheel switching to cycle in wheel order.
        /// </summary>
        public void SetWheelWeaponOrder(List<InventoryItemData> items)
        {
            _wheelWeapons = new List<WeaponDataSO>();

            if (items == null) return;

            foreach (InventoryItemData item in items)
            {
                if (item != null && _itemToWeaponMap.TryGetValue(item, out WeaponDataSO weaponData))
                {
                    _wheelWeapons.Add(weaponData);
                }
            }

            if (_showDebugLogs)
            {
                Debug.Log($"[WeaponManager] Wheel weapon order synced: {_wheelWeapons.Count} weapons.");
            }
        }

        #endregion

        #region Holster / Draw Flow

        /// <summary>
        /// Starts the holster animation on the current weapon.
        /// If pendingNext is not null, that weapon will be drawn after holster completes.
        /// If pendingNext is null, the player simply holsters (empty hands).
        /// </summary>
        private void HolsterCurrentWeapon(WeaponDataSO pendingNext)
        {
            _pendingWeaponData = pendingNext;
            _isSwitching = true;

            _currentWeapon.OnHolsterFinished += OnHolsterAnimationDone;
            _currentWeapon.StartHolster();
        }

        /// <summary>
        /// Callback when the holster animation finishes (via Animation Event -> WeaponBase -> this).
        /// Deactivates the current viewmodel. If there's a pending weapon, draws it.
        /// Otherwise, marks the player as holstered.
        /// </summary>
        private void OnHolsterAnimationDone()
        {
            if (_currentWeapon != null)
            {
                _currentWeapon.OnHolsterFinished -= OnHolsterAnimationDone;
                _currentWeapon.gameObject.SetActive(false);
            }

            // Tell ViewmodelMotion there's no active weapon
            _viewmodelMotion?.SetActiveWeapon(null, null);

            if (_pendingWeaponData != null)
            {
                // Switch to the pending weapon
                DrawWeapon(_pendingWeaponData);
                _pendingWeaponData = null;
            }
            else
            {
                // Pure holster -- no new weapon, player has empty hands
                _isHolstered = true;
                _isSwitching = false;
            }
        }

        /// <summary>
        /// Draws a weapon by instantiating (or reactivating) its viewmodel prefab
        /// under the ViewmodelRoot and playing the draw animation.
        /// </summary>
        private void DrawWeapon(WeaponDataSO weaponData)
        {
            _isHolstered = false;
            _currentWeaponData = weaponData;

            // If the current weapon instance is a different weapon, destroy it
            if (_currentWeapon != null && _currentWeapon.WeaponData != weaponData)
            {
                Destroy(_currentWeapon.gameObject);
                _currentWeapon = null;
            }

            // Instantiate the viewmodel if we don't have one
            if (_currentWeapon == null && weaponData.viewmodelPrefab != null && _viewmodelRoot != null)
            {
                GameObject instance = Instantiate(weaponData.viewmodelPrefab, _viewmodelRoot);
                // Weapon child sits at local origin — the Animator controls its localPosition/localRotation.
                // The hip/ADS offset is handled by ViewmodelMotion on the PARENT (ViewmodelRoot).
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                _currentWeapon = instance.GetComponent<WeaponBase>();

                if (_currentWeapon == null)
                {
                    Debug.LogError($"[WeaponManager] Viewmodel prefab '{weaponData.viewmodelPrefab.name}' " +
                                   "is missing a WeaponBase component (RangedWeapon or MeleeWeapon)!");
                    Destroy(instance);
                    _isSwitching = false;
                    return;
                }
            }

            if (_currentWeapon != null)
            {
                _currentWeapon.gameObject.SetActive(true);
                _isSwitching = true;

                // Reset aim state on fresh draw
                SetAiming(false);

                // Tell ViewmodelMotion about the new weapon
                _viewmodelMotion?.SetActiveWeapon(_currentWeapon.transform, weaponData);

                _currentWeapon.OnDrawFinished += OnDrawAnimationDone;
                _currentWeapon.StartDraw();
            }
            else
            {
                _isSwitching = false;
            }
        }

        /// <summary>
        /// Callback when the draw animation finishes. The weapon is now in Idle state and ready for input.
        /// Executes any queued action from auto-draw.
        /// </summary>
        private void OnDrawAnimationDone()
        {
            if (_currentWeapon != null)
            {
                _currentWeapon.OnDrawFinished -= OnDrawAnimationDone;
            }

            _isSwitching = false;

            if (_showDebugLogs)
            {
                string name = _currentWeaponData?.inventoryItem?.displayName ?? "Unknown";
                Debug.Log($"[WeaponManager] Weapon '{name}' drawn and ready.");
            }

            // Execute queued action from auto-draw
            ExecuteQueuedAction();
        }

        /// <summary>
        /// Executes the queued action that triggered the auto-draw.
        /// </summary>
        private void ExecuteQueuedAction()
        {
            QueuedAction action = _queuedAction;
            _queuedAction = QueuedAction.None;

            if (_currentWeapon == null) return;

            switch (action)
            {
                case QueuedAction.Fire:
                    _currentWeapon.TryFire();
                    break;
                case QueuedAction.Reload:
                    _currentWeapon.TryReload();
                    break;
                case QueuedAction.Aim:
                    SetAiming(true);
                    break;
            }
        }

        #endregion
    }
}
