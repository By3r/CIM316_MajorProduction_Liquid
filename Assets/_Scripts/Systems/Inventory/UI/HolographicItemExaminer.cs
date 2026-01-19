using UnityEngine;
using UnityEngine.UI;
using TMPro;
using _Scripts.Core.Managers;

namespace _Scripts.Systems.Inventory.UI
{
    /// <summary>
    /// Holographic item examination system.
    /// Spawns item in front of player with holographic effect.
    /// World-space UI shows title above and description to the right.
    /// Player movement is frozen while examining.
    /// </summary>
    public class HolographicItemExaminer : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Hologram Settings")]
        [Tooltip("Material with holographic shader to apply to the examined item.")]
        [SerializeField] private Material _hologramMaterial;
        [Tooltip("Distance from camera to spawn the hologram.")]
        [SerializeField] private float _spawnDistance = 2f;
        [Tooltip("Height offset from camera center.")]
        [SerializeField] private float _heightOffset = -0.3f;
        [Tooltip("Scale multiplier for the hologram.")]
        [SerializeField] private float _hologramScale = 0.5f;

        [Header("World Space UI")]
        [Tooltip("Canvas for world-space UI (title and description).")]
        [SerializeField] private Canvas _worldCanvas;
        [Tooltip("Text for item name (positioned above hologram).")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [Tooltip("Text for item description (positioned to the right).")]
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [Tooltip("Background panel for description.")]
        [SerializeField] private Image _descriptionBackground;

        [Header("UI Positioning")]
        [Tooltip("Offset for title text above the hologram.")]
        [SerializeField] private Vector3 _titleOffset = new Vector3(0f, 0.5f, 0f);
        [Tooltip("Offset for description panel to the right of hologram.")]
        [SerializeField] private Vector3 _descriptionOffset = new Vector3(0.8f, 0f, 0f);

        [Header("Rotation Settings")]
        [SerializeField] private float _rotationSpeed = 100f;
        [SerializeField] private bool _autoRotate = true;
        [SerializeField] private float _autoRotateSpeed = 30f;

        [Header("Close Settings")]
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;
        [SerializeField] private KeyCode _alternateCloseKey = KeyCode.E;

        #endregion

        #region Private Fields

        private GameObject _currentHologram;
        private InventoryItemData _currentItemData;
        private bool _isOpen = false;
        private bool _isDragging = false;
        private Vector2 _lastMousePosition;
        private Quaternion _currentRotation = Quaternion.identity;
        private Camera _mainCamera;

        // Store original materials to restore if needed
        private Material[] _originalMaterials;

        #endregion

        #region Properties

        public bool IsOpen => _isOpen;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _mainCamera = Camera.main;

            // Create world canvas if not assigned
            if (_worldCanvas == null)
            {
                CreateWorldCanvas();
            }

            // Start hidden
            HideUI();
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Handle close input
            if (Input.GetKeyDown(_closeKey) || Input.GetKeyDown(_alternateCloseKey))
            {
                Hide();
                return;
            }

            // Handle rotation input
            HandleRotationInput();

            // Auto-rotate if enabled and not dragging
            if (_autoRotate && !_isDragging && _currentHologram != null)
            {
                // Auto-rotate around camera's up axis for consistent appearance
                Vector3 cameraUp = _mainCamera.transform.up;
                _currentRotation = Quaternion.AngleAxis(_autoRotateSpeed * Time.unscaledDeltaTime, cameraUp) * _currentRotation;
                ApplyRotation();
            }

            // Keep hologram in front of camera
            UpdateHologramPosition();

            // Make UI face the camera
            UpdateUIOrientation();
        }

        private void OnDestroy()
        {
            CleanupHologram();
        }

        #endregion

        #region Setup

        private void CreateWorldCanvas()
        {
            // Create canvas GameObject
            GameObject canvasObj = new GameObject("HologramUI_Canvas");
            canvasObj.transform.SetParent(transform);

            _worldCanvas = canvasObj.AddComponent<Canvas>();
            _worldCanvas.renderMode = RenderMode.WorldSpace;
            _worldCanvas.sortingOrder = 32767; // Max sorting order to render on top

            // Add canvas scaler for consistent sizing
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;

            // Set canvas size - much smaller scale for world space visibility
            RectTransform canvasRect = _worldCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(400f, 200f);
            canvasRect.localScale = Vector3.one * 0.001f; // Very small scale for world space

            // Create title text
            CreateTitleText(canvasRect);

            // Create description panel
            CreateDescriptionPanel(canvasRect);
        }

        private void CreateTitleText(RectTransform parent)
        {
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(parent);

            _titleText = titleObj.AddComponent<TextMeshProUGUI>();
            _titleText.fontSize = 36;
            _titleText.fontStyle = FontStyles.Bold;
            _titleText.alignment = TextAlignmentOptions.Center;
            _titleText.color = new Color(0.3f, 0.9f, 1f, 1f); // Cyan hologram color

            RectTransform titleRect = _titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(400f, 50f);
            titleRect.anchoredPosition = Vector2.zero;
        }

        private void CreateDescriptionPanel(RectTransform parent)
        {
            // Create background
            GameObject bgObj = new GameObject("DescriptionBackground");
            bgObj.transform.SetParent(parent);

            _descriptionBackground = bgObj.AddComponent<Image>();
            _descriptionBackground.color = new Color(0f, 0.1f, 0.15f, 0.8f);

            RectTransform bgRect = _descriptionBackground.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(300f, 150f);
            bgRect.anchoredPosition = new Vector2(200f, 0f);

            // Create description text
            GameObject descObj = new GameObject("DescriptionText");
            descObj.transform.SetParent(bgRect);

            _descriptionText = descObj.AddComponent<TextMeshProUGUI>();
            _descriptionText.fontSize = 24;
            _descriptionText.alignment = TextAlignmentOptions.TopLeft;
            _descriptionText.color = new Color(0.8f, 0.95f, 1f, 1f);

            RectTransform descRect = _descriptionText.GetComponent<RectTransform>();
            descRect.anchorMin = Vector2.zero;
            descRect.anchorMax = Vector2.one;
            descRect.offsetMin = new Vector2(10f, 10f);
            descRect.offsetMax = new Vector2(-10f, -10f);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the holographic examination for the given item.
        /// </summary>
        public void Show(InventoryItemData itemData)
        {
            if (itemData == null)
            {
                Debug.LogWarning("[HolographicItemExaminer] Cannot examine item - itemData is null.");
                return;
            }

            if (itemData.worldPrefab == null)
            {
                Debug.LogWarning($"[HolographicItemExaminer] Cannot examine '{itemData.displayName}' - no worldPrefab assigned.");
                return;
            }

            // Ensure we have a camera reference
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (_mainCamera == null)
            {
                Debug.LogError("[HolographicItemExaminer] No main camera found!");
                return;
            }

            Debug.Log($"[HolographicItemExaminer] Showing hologram for: {itemData.displayName}");

            _currentItemData = itemData;
            _isOpen = true;

            // Freeze player movement
            FreezePlayer(true);

            // Create the hologram
            CreateHologram(itemData);

            // Update UI text
            UpdateUIText(itemData);

            // Show UI
            ShowUI();

            // Reset rotation to face camera
            _currentRotation = Quaternion.LookRotation(_mainCamera.transform.forward, Vector3.up);

            // Show cursor for interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Hides the holographic examination.
        /// </summary>
        public void Hide()
        {
            _isOpen = false;
            _currentItemData = null;

            // Cleanup hologram
            CleanupHologram();

            // Hide UI
            HideUI();

            // Unfreeze player
            FreezePlayer(false);

            // Hide cursor (inventory will handle this if still open)
        }

        #endregion

        #region Private Methods

        private void CreateHologram(InventoryItemData itemData)
        {
            CleanupHologram();

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            // Calculate spawn position in front of camera
            Vector3 spawnPos = _mainCamera.transform.position +
                               _mainCamera.transform.forward * _spawnDistance +
                               Vector3.up * _heightOffset;

            // Instantiate the prefab
            _currentHologram = Instantiate(itemData.worldPrefab, spawnPos, Quaternion.identity);
            _currentHologram.name = "Hologram_" + itemData.displayName;

            // Disable physics and colliders
            DisablePhysics(_currentHologram);

            // Apply holographic material
            ApplyHologramMaterial(_currentHologram);

            // Scale and center the object
            ScaleAndCenterHologram(_currentHologram);
        }

        private void DisablePhysics(GameObject obj)
        {
            // Disable rigidbodies
            foreach (var rb in obj.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Disable colliders
            foreach (var col in obj.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // Disable MonoBehaviours
            foreach (var mb in obj.GetComponentsInChildren<MonoBehaviour>())
            {
                mb.enabled = false;
            }
        }

        private void ApplyHologramMaterial(GameObject obj)
        {
            if (_hologramMaterial == null)
            {
                Debug.LogWarning("[HolographicItemExaminer] No hologram material assigned. Using default appearance.");
                return;
            }

            // Apply hologram material to all renderers while preserving original textures
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                Material[] originalMaterials = renderer.sharedMaterials;
                Material[] newMaterials = new Material[originalMaterials.Length];

                for (int i = 0; i < newMaterials.Length; i++)
                {
                    // Create a new instance of the hologram material
                    newMaterials[i] = new Material(_hologramMaterial);

                    // Copy the original texture if it exists
                    if (originalMaterials[i] != null)
                    {
                        // Try to get main texture from various common property names
                        Texture mainTex = originalMaterials[i].mainTexture;
                        if (mainTex == null && originalMaterials[i].HasProperty("_BaseMap"))
                        {
                            mainTex = originalMaterials[i].GetTexture("_BaseMap");
                        }
                        if (mainTex == null && originalMaterials[i].HasProperty("_MainTex"))
                        {
                            mainTex = originalMaterials[i].GetTexture("_MainTex");
                        }

                        if (mainTex != null)
                        {
                            newMaterials[i].mainTexture = mainTex;
                            if (newMaterials[i].HasProperty("_MainTex"))
                            {
                                newMaterials[i].SetTexture("_MainTex", mainTex);
                            }
                        }
                    }
                }
                renderer.materials = newMaterials;
            }
        }

        private void ScaleAndCenterHologram(GameObject obj)
        {
            // Get bounds of all renderers
            Bounds bounds = GetObjectBounds(obj);
            if (bounds.size == Vector3.zero) return;

            // Scale to fit desired size
            float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            if (maxExtent > 0)
            {
                float desiredSize = _hologramScale;
                float scale = desiredSize / maxExtent;
                obj.transform.localScale *= scale;
            }

            // Recalculate bounds after scaling
            bounds = GetObjectBounds(obj);

            // Center the object
            Vector3 targetPosition = _mainCamera.transform.position +
                                     _mainCamera.transform.forward * _spawnDistance +
                                     Vector3.up * _heightOffset;

            Vector3 offset = targetPosition - bounds.center;
            obj.transform.position += offset;
        }

        private Bounds GetObjectBounds(GameObject obj)
        {
            Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
            bool hasBounds = false;

            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private void UpdateHologramPosition()
        {
            if (_currentHologram == null || _mainCamera == null) return;

            // Keep hologram at fixed distance in front of camera
            Vector3 targetPosition = _mainCamera.transform.position +
                                     _mainCamera.transform.forward * _spawnDistance +
                                     Vector3.up * _heightOffset;

            // Smoothly move to target position
            _currentHologram.transform.position = Vector3.Lerp(
                _currentHologram.transform.position,
                targetPosition,
                Time.unscaledDeltaTime * 10f
            );
        }

        private void UpdateUIOrientation()
        {
            if (_worldCanvas == null || _mainCamera == null || _currentHologram == null) return;

            // Position canvas at hologram location
            Bounds bounds = GetObjectBounds(_currentHologram);

            // Position title above the hologram
            if (_titleText != null)
            {
                Vector3 titleWorldPos = bounds.center + _titleOffset;
                _titleText.transform.position = titleWorldPos;
                _titleText.transform.rotation = Quaternion.LookRotation(
                    _titleText.transform.position - _mainCamera.transform.position
                );
            }

            // Position description to the right
            if (_descriptionBackground != null)
            {
                Vector3 descWorldPos = bounds.center +
                    _mainCamera.transform.right * _descriptionOffset.x +
                    Vector3.up * _descriptionOffset.y;
                _descriptionBackground.transform.position = descWorldPos;
                _descriptionBackground.transform.rotation = Quaternion.LookRotation(
                    _descriptionBackground.transform.position - _mainCamera.transform.position
                );
            }
        }

        private void UpdateUIText(InventoryItemData itemData)
        {
            if (_titleText != null)
            {
                _titleText.text = itemData.displayName;
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = !string.IsNullOrEmpty(itemData.description)
                    ? itemData.description
                    : "No description available.";
            }
        }

        private void ShowUI()
        {
            if (_worldCanvas != null)
            {
                _worldCanvas.gameObject.SetActive(true);
                Debug.Log("[HolographicItemExaminer] World canvas activated");
            }
            else
            {
                Debug.LogWarning("[HolographicItemExaminer] World canvas is null!");
            }

            // Also ensure title and description are active
            if (_titleText != null)
            {
                _titleText.gameObject.SetActive(true);
            }

            if (_descriptionText != null)
            {
                _descriptionText.gameObject.SetActive(true);
            }

            if (_descriptionBackground != null)
            {
                _descriptionBackground.gameObject.SetActive(true);
            }
        }

        private void HideUI()
        {
            if (_worldCanvas != null)
            {
                _worldCanvas.gameObject.SetActive(false);
            }

            // Also hide individual elements in case they're not children of the canvas
            if (_titleText != null)
            {
                _titleText.gameObject.SetActive(false);
            }

            if (_descriptionBackground != null)
            {
                _descriptionBackground.gameObject.SetActive(false);
            }
        }

        private void HandleRotationInput()
        {
            if (_currentHologram == null) return;

            // Start dragging on left mouse button
            if (Input.GetMouseButtonDown(0))
            {
                _isDragging = true;
                _lastMousePosition = Input.mousePosition;
            }

            // Stop dragging
            if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }

            // Apply rotation while dragging
            if (_isDragging)
            {
                Vector2 delta = (Vector2)Input.mousePosition - _lastMousePosition;

                // Rotate around camera-relative axes for intuitive control
                // Horizontal drag rotates around camera's up axis
                // Vertical drag rotates around camera's right axis
                Vector3 cameraUp = _mainCamera.transform.up;
                Vector3 cameraRight = _mainCamera.transform.right;

                Quaternion yRotation = Quaternion.AngleAxis(-delta.x * _rotationSpeed * Time.unscaledDeltaTime, cameraUp);
                Quaternion xRotation = Quaternion.AngleAxis(delta.y * _rotationSpeed * Time.unscaledDeltaTime, cameraRight);

                _currentRotation = xRotation * yRotation * _currentRotation;

                ApplyRotation();
                _lastMousePosition = Input.mousePosition;
            }
        }

        private void ApplyRotation()
        {
            if (_currentHologram == null) return;
            _currentHologram.transform.rotation = _currentRotation;
        }

        private void FreezePlayer(bool freeze)
        {
            // Use InputManager to disable/enable player input
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(!freeze);
            }
        }

        private void CleanupHologram()
        {
            if (_currentHologram != null)
            {
                Destroy(_currentHologram);
                _currentHologram = null;
            }
        }

        #endregion
    }
}
