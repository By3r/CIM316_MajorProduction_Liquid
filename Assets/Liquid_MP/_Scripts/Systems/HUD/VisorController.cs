using System.Collections;
using _Scripts.Core.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Systems.HUD
{
    /// <summary>
    /// Controls the 3D visor HUD parented to the FPS camera.
    ///
    /// Three states:
    /// 1. Visor DOWN, panel CLOSED (default) — glass visible, AlwaysVisible group shown, panel hidden.
    /// 2. Visor DOWN, panel OPEN (TAB)       — glass visible, AlwaysVisible group shown, panel shown.
    /// 3. Visor RAISED (V key)               — glass raised/hidden, everything hidden, TAB blocked.
    ///
    /// The glass mesh is always visible when the visor is down.
    /// Hold V to raise the visor (flip up). While raised, TAB does nothing.
    ///
    /// This script handles attachment to the camera, canvas setup,
    /// and state transitions. It does NOT own TAB input or cursor lock —
    /// InventoryUI still handles that.
    /// </summary>
    public class VisorController : MonoBehaviour
    {
        #region Singleton

        private static VisorController _instance;
        public static VisorController Instance => _instance;

        #endregion

        #region Serialized Fields

        [Header("Visor References")]
        [Tooltip("The glass quad mesh behind the canvas. Always visible when visor is down.")]
        [SerializeField] private GameObject _visorGlass;

        [Tooltip("The World Space Canvas component on this visor.")]
        [SerializeField] private Canvas _visorCanvas;

        [Tooltip("The URP Overlay Camera that renders only the Visor layer. " +
                 "Used as the canvas event camera for GraphicRaycaster clicks.")]
        [SerializeField] private Camera _visorOverlayCamera;

        [Header("Visibility Groups")]
        [Tooltip("CanvasGroup for elements that are always visible when visor is down (crosshair, ammo, etc).")]
        [SerializeField] private CanvasGroup _alwaysVisibleGroup;

        [Tooltip("CanvasGroup for elements shown only when TAB is pressed (inventory, terminal, vitals).")]
        [SerializeField] private CanvasGroup _visorPanelGroup;

        [Header("Camera Attachment")]
        [Tooltip("Offset from camera when visor is down. Z = distance in front of camera.")]
        [SerializeField] private Vector3 _visorOffset = new Vector3(0f, 0f, 0.4f);

        [Header("Raise Animation")]
        [Tooltip("Local rotation when visor is raised (flipped up). Rotates around X axis.")]
        [SerializeField] private Vector3 _raisedRotation = new Vector3(-80f, 0f, 0f);

        [Tooltip("Local position offset when visor is raised (moves up slightly).")]
        [SerializeField] private Vector3 _raisedOffset = new Vector3(0f, 0.15f, 0.3f);

        [Tooltip("How fast the visor raises/lowers.")]
        [SerializeField] private float _raiseSpeed = 10f;

        [Header("Panel Fade")]
        [Tooltip("How fast the panel fades in/out.")]
        [SerializeField] private float _fadeSpeed = 8f;

        #endregion

        #region Private Fields

        private bool _isPanelOpen;
        private bool _isVisorRaised;
        private float _targetPanelAlpha;
        private float _targetAlwaysAlpha;
        private Camera _mainCamera;
        private bool _isAttached;

        // Raise animation state
        private Vector3 _targetLocalPosition;
        private Quaternion _targetLocalRotation;

        // Input
        private InputAction _raiseAction;

        #endregion

        #region Properties

        /// <summary>Whether the TAB panel (inventory, terminal, vitals) is currently open.</summary>
        public bool IsPanelOpen => _isPanelOpen;

        /// <summary>Whether the visor is raised (flipped up). When raised, TAB is blocked.</summary>
        public bool IsVisorRaised => _isVisorRaised;

        /// <summary>Whether the visor has successfully attached to the camera.</summary>
        public bool IsAttached => _isAttached;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private IEnumerator Start()
        {
            // Wait for camera to exist (player may not be spawned yet)
            while (Camera.main == null)
            {
                yield return null;
            }

            AttachToCamera(Camera.main);
            InitializeStates();
        }

        private void OnEnable()
        {
            _raiseAction = new InputAction("RaiseVisor", InputActionType.Button, "<Keyboard>/v");
            _raiseAction.performed += OnRaiseVisor;
            _raiseAction.Enable();
        }

        private void OnDisable()
        {
            if (_raiseAction != null)
            {
                _raiseAction.performed -= OnRaiseVisor;
                _raiseAction.Disable();
                _raiseAction.Dispose();
            }
        }

        private void Update()
        {
            UpdateFades();
            UpdateRaiseAnimation();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        #endregion

        #region Input Handling

        private void OnRaiseVisor(InputAction.CallbackContext context)
        {
            // Block if player input is disabled (e.g. debug console, menus)
            if (InputManager.Instance != null && !InputManager.Instance.IsPlayerInputEnabled)
                return;

            // If panel is open, close it first before allowing raise
            if (_isPanelOpen) return;

            ToggleVisorRaise();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Opens the visor panel (called by InventoryUI when TAB is pressed).
        /// Blocked if visor is raised.
        /// </summary>
        public void ShowPanel()
        {
            if (_isVisorRaised) return;

            _isPanelOpen = true;
            _targetPanelAlpha = 1f;

            if (_visorPanelGroup != null)
            {
                _visorPanelGroup.blocksRaycasts = true;
                _visorPanelGroup.interactable = true;
            }
        }

        /// <summary>
        /// Closes the visor panel (called by InventoryUI when inventory closes).
        /// </summary>
        public void HidePanel()
        {
            _isPanelOpen = false;
            _targetPanelAlpha = 0f;

            if (_visorPanelGroup != null)
            {
                _visorPanelGroup.blocksRaycasts = false;
                _visorPanelGroup.interactable = false;
            }
        }

        /// <summary>
        /// Toggles between visor raised and lowered.
        /// </summary>
        public void ToggleVisorRaise()
        {
            if (_isVisorRaised)
                LowerVisor();
            else
                RaiseVisor();
        }

        /// <summary>
        /// Raises the visor (flips it up). Glass animates upward,
        /// UI fades out smoothly during the transition.
        /// </summary>
        public void RaiseVisor()
        {
            _isVisorRaised = true;

            // Set animation targets — visor rotates up and shifts position
            _targetLocalPosition = _raisedOffset;
            _targetLocalRotation = Quaternion.Euler(_raisedRotation);

            // Fade out all UI (smooth, handled in Update)
            _targetAlwaysAlpha = 0f;
            _targetPanelAlpha = 0f;

            // Disable interaction immediately (no clicking during raise)
            if (_visorPanelGroup != null)
            {
                _visorPanelGroup.blocksRaycasts = false;
                _visorPanelGroup.interactable = false;
            }

            // Glass stays active — it animates out of view via rotation.
            // No SetActive(false) here; the rotation handles visibility.
        }

        /// <summary>
        /// Lowers the visor back down. Glass animates downward,
        /// AlwaysVisible UI fades back in.
        /// </summary>
        public void LowerVisor()
        {
            _isVisorRaised = false;

            // Set animation targets — visor returns to default position
            _targetLocalPosition = _visorOffset;
            _targetLocalRotation = Quaternion.identity;

            // Fade always-visible group back in (smooth, handled in Update)
            _targetAlwaysAlpha = 1f;

            // Glass is already active (never deactivated), just animates back.
            if (_visorGlass != null)
                _visorGlass.SetActive(true);
        }

        /// <summary>
        /// Re-attaches to a new camera. Useful if the player is respawned
        /// and Camera.main changes.
        /// </summary>
        public void ReattachToCamera()
        {
            if (Camera.main != null)
            {
                AttachToCamera(Camera.main);
            }
        }

        #endregion

        #region Private Methods

        private void AttachToCamera(Camera cam)
        {
            _mainCamera = cam;

            // Parent ourselves directly to the camera transform (FPCamera in the hierarchy).
            // This means we follow head bob, look rotation, camera shake — everything.
            transform.SetParent(_mainCamera.transform, false);
            transform.localPosition = _visorOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            // Initialize animation targets to current position
            _targetLocalPosition = _visorOffset;
            _targetLocalRotation = Quaternion.identity;

            // Wire the canvas event camera for GraphicRaycaster clicks.
            // Use the overlay camera if assigned (renders visor layer on top of world).
            // Falls back to main camera if no overlay camera is set.
            if (_visorCanvas != null)
            {
                _visorCanvas.worldCamera = _visorOverlayCamera != null
                    ? _visorOverlayCamera
                    : _mainCamera;
            }

            _isAttached = true;
        }

        private void InitializeStates()
        {
            // Visor starts DOWN with panel CLOSED.

            // Glass is visible by default.
            if (_visorGlass != null)
            {
                _visorGlass.SetActive(true);
            }

            // Always-visible group starts fully visible.
            _targetAlwaysAlpha = 1f;
            if (_alwaysVisibleGroup != null)
            {
                _alwaysVisibleGroup.alpha = 1f;
                _alwaysVisibleGroup.blocksRaycasts = false; // Don't block gameplay raycasts
                _alwaysVisibleGroup.interactable = false;   // Not interactive (crosshair, ammo)
            }

            // Panel group starts hidden.
            if (_visorPanelGroup != null)
            {
                _visorPanelGroup.alpha = 0f;
                _visorPanelGroup.blocksRaycasts = false;
                _visorPanelGroup.interactable = false;
            }
        }

        private void UpdateFades()
        {
            float step = _fadeSpeed * Time.deltaTime;

            // Fade the visor panel group (inventory, terminal, vitals)
            if (_visorPanelGroup != null)
            {
                float current = _visorPanelGroup.alpha;
                if (!Mathf.Approximately(current, _targetPanelAlpha))
                {
                    _visorPanelGroup.alpha = Mathf.MoveTowards(current, _targetPanelAlpha, step);
                }
            }

            // Fade the always-visible group (crosshair, ammo — fades out when visor is raised)
            if (_alwaysVisibleGroup != null)
            {
                float current = _alwaysVisibleGroup.alpha;
                if (!Mathf.Approximately(current, _targetAlwaysAlpha))
                {
                    _alwaysVisibleGroup.alpha = Mathf.MoveTowards(current, _targetAlwaysAlpha, step);
                }
            }
        }

        private void UpdateRaiseAnimation()
        {
            // Smoothly move/rotate the visor root to its target pose
            float t = _raiseSpeed * Time.deltaTime;

            transform.localPosition = Vector3.Lerp(
                transform.localPosition, _targetLocalPosition, t);

            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, _targetLocalRotation, t);
        }

        private void SetGroupAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
            }
        }

        #endregion
    }
}
