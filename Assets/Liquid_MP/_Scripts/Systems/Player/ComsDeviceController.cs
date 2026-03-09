using _Scripts.Systems.Inventory.ItemTypes;
using KINEMATION.TacticalShooterPack.Scripts.Animation;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using UnityEngine;

namespace _Scripts.Systems.Player
{
    /// <summary>
    /// Manages the COMS device lifecycle: equip/unequip, activate/deactivate,
    /// and left-hand IK blending. Lives on the Player GameObject.
    /// PlayerEquipment calls EquipDevice/UnequipDevice when the COMS slot changes.
    /// PlayerEquipment calls ToggleComs when key 3 is pressed.
    ///
    /// KEY DESIGN: The COMS device is parented to the left hand bone. IK target is
    /// computed inside the animation job from the head bone (after spine rotation) +
    /// head-relative offsets. This matches how weapons work: everything in one animation
    /// pass, zero C# relay lag. The device follows wherever IK puts the hand.
    /// </summary>
    public class ComsDeviceController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Animation")]
        [Tooltip("Speed for blending the left-hand IK weight.")]
        [SerializeField] private float _leftHandBlendSpeed = 25f;

        [Header("Left Hand IK Target (head-bone-relative)")]
        [Tooltip("Where the left hand should be relative to the head bone (after spine rotation). " +
                 "Negative X = left, negative Y = down, positive Z = forward.")]
        [SerializeField] private Vector3 _handPositionOffset = new Vector3(-0.15f, -0.2f, 0.25f);

        [Tooltip("Left hand rotation relative to the head bone.")]
        [SerializeField] private Vector3 _handRotationOffset = Vector3.zero;

        #endregion

        #region Private Fields

        private TacticalShooterPlayer _tacticalPlayer;
        private TacticalProceduralAnimation _tacProceduralAnimation;
        private Transform _leftHandBone;

        private ComsDevice _comsInstance;
        private bool _isEquipped;
        private bool _isActive;
        private float _comsLeftHandWeight;

        #endregion

        #region Properties

        /// <summary>Whether a COMS device is equipped in the equipment slot.</summary>
        public bool IsEquipped => _isEquipped;

        /// <summary>Whether the COMS device is currently toggled on (visible in hand).</summary>
        public bool IsActive => _isActive;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _tacticalPlayer = GetComponent<TacticalShooterPlayer>();
            _tacProceduralAnimation = GetComponent<TacticalProceduralAnimation>();
        }

        private void Start()
        {
            if (_tacProceduralAnimation != null)
            {
                _leftHandBone = _tacProceduralAnimation.bones.leftHand;
            }
        }

        private void Update()
        {
            if (!_isEquipped || _comsInstance == null) return;

            // Smoothly blend left-hand IK weight
            float target = _isActive ? 1f : 0f;
            _comsLeftHandWeight = Mathf.MoveTowards(_comsLeftHandWeight, target,
                Time.deltaTime * _leftHandBlendSpeed);

            // Pass head-relative offsets and weight to the animation system.
            // The animation job computes the world-space IK target from the head bone
            // (after spine rotation) + these offsets — all in one animation pass, zero lag.
            if (_tacticalPlayer != null && _comsLeftHandWeight > 0.001f)
            {
                _tacticalPlayer.SetComsHandOffset(
                    _handPositionOffset,
                    Quaternion.Euler(_handRotationOffset));
                _tacticalPlayer.SetComsLeftHandWeight(_comsLeftHandWeight);
            }
            else if (_tacticalPlayer != null)
            {
                _tacticalPlayer.SetComsLeftHandWeight(0f);
            }

            // Hide model when blend reaches 0 during deactivation
            if (!_isActive && _comsLeftHandWeight <= 0f && _comsInstance.gameObject.activeSelf)
            {
                _comsInstance.Hide();

                // If no weapon is drawn, hide the arm renderers again
                // (they were shown when COMS activated).
                if (_tacticalPlayer != null && _tacticalPlayer.IsUnarmed)
                    _tacticalPlayer.SetCharacterRenderersVisible(false);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Called by PlayerEquipment when a COMS device is equipped to slot 3.
        /// Instantiates the runtime prefab, parents to the left hand bone, hides it.
        /// </summary>
        public void EquipDevice(ComsDeviceItemData data)
        {
            if (data == null || data.comsBehaviourPrefab == null)
            {
                Debug.LogWarning("[ComsDeviceController] COMS item has no behaviour prefab.");
                return;
            }

            // Clean up any existing instance
            if (_comsInstance != null)
                UnequipDevice();

            // Parent to left hand bone — the device follows wherever IK puts the hand.
            Transform parent = _leftHandBone != null ? _leftHandBone : transform;
            GameObject instance = Instantiate(data.comsBehaviourPrefab, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            _comsInstance = instance.GetComponent<ComsDevice>();
            if (_comsInstance == null)
            {
                Debug.LogError("[ComsDeviceController] COMS behaviour prefab is missing ComsDevice component!");
                Destroy(instance);
                return;
            }

            _comsInstance.Hide(); // Start hidden — activated via key 3
            _isEquipped = true;
            _isActive = false;
            _comsLeftHandWeight = 0f;
        }

        /// <summary>
        /// Called by PlayerEquipment when the COMS device is unequipped.
        /// Deactivates and destroys the runtime instance.
        /// </summary>
        public void UnequipDevice()
        {
            if (_isActive)
            {
                _isActive = false;
                _comsLeftHandWeight = 0f;

                if (_tacticalPlayer != null)
                {
                    _tacticalPlayer.SetComsLeftHandWeight(0f);

                    // Hide arm renderers if no weapon is drawn
                    if (_tacticalPlayer.IsUnarmed)
                        _tacticalPlayer.SetCharacterRenderersVisible(false);
                }
            }

            if (_comsInstance != null)
            {
                Destroy(_comsInstance.gameObject);
                _comsInstance = null;
            }

            _isEquipped = false;
        }

        /// <summary>
        /// Toggles the COMS device on/off. Called by PlayerEquipment when key 3 is pressed.
        /// PlayerEquipment handles weapon holstering before calling this.
        /// </summary>
        public void ToggleComs()
        {
            if (!_isEquipped || _comsInstance == null) return;

            if (_isActive)
                Deactivate();
            else
                Activate();
        }

        #endregion

        #region Private Methods

        private void Activate()
        {
            _comsInstance.Show();
            _isActive = true;
            // _comsLeftHandWeight will blend up in Update()

            // Ensure arm renderers are visible — the player may be "unarmed"
            // (no weapon drawn) which normally hides the first-person arms.
            if (_tacticalPlayer != null)
                _tacticalPlayer.SetCharacterRenderersVisible(true);
        }

        private void Deactivate()
        {
            _isActive = false;
            // _comsLeftHandWeight will blend down in Update()
            // Model hides when blend reaches 0
        }

        #endregion
    }
}
