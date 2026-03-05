using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Scripts.Systems.Inventory.UI
{
    public class ItemExaminer : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private GameObject _examinePanel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private RawImage _itemDisplayImage;

        [Header("Buttons")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _minimizeButton;

        [Header("Render Setup")]
        [SerializeField] private Camera _examineCamera;
        [SerializeField] private Transform _itemSpawnPoint;
        [SerializeField] private RenderTexture _renderTexture;

        [Header("Rotation Settings")]
        [SerializeField] private float _rotationSpeed = 0.5f;

        #endregion

        #region Private Fields

        private GameObject _currentItem;
        private bool _isOpen;
        private bool _isDragging;
        private float _lastMouseX;
        private float _currentYRotation;

        #endregion

        #region Properties

        public bool IsOpen => _isOpen;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_examinePanel != null)
            {
                _examinePanel.SetActive(false);
            }

            if (_itemDisplayImage != null && _renderTexture != null)
            {
                _itemDisplayImage.texture = _renderTexture;
            }

            if (_examineCamera != null && _renderTexture != null)
            {
                _examineCamera.targetTexture = _renderTexture;
                _examineCamera.enabled = false;
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }

            if (_minimizeButton != null)
            {
                _minimizeButton.onClick.AddListener(Hide);
            }
        }

        private void Update()
        {
            if (!_isOpen) return;

            HandleRotationInput();
        }

        private void OnDestroy()
        {
            CleanupItem();

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Hide);
            }

            if (_minimizeButton != null)
            {
                _minimizeButton.onClick.RemoveListener(Hide);
            }
        }

        #endregion

        #region Public Methods

        public void Show(InventoryItemData itemData)
        {
            if (itemData == null)
            {
                Debug.LogWarning("[ItemExaminer] Cannot examine - itemData is null.");
                return;
            }

            if (itemData.worldPrefab == null)
            {
                Debug.LogWarning($"[ItemExaminer] Cannot examine '{itemData.displayName}' - no worldPrefab assigned.");
                return;
            }

            _isOpen = true;

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

            SpawnItem(itemData);

            if (_examineCamera != null)
            {
                _examineCamera.enabled = true;
            }

            if (_examinePanel != null)
            {
                _examinePanel.SetActive(true);
            }
            else
            {
                Debug.LogError("[ItemExaminer] _examinePanel is not assigned!");
            }

            _currentYRotation = 0f;
        }

        public void Hide()
        {
            _isOpen = false;

            if (_examinePanel != null)
            {
                _examinePanel.SetActive(false);
            }

            if (_examineCamera != null)
            {
                _examineCamera.enabled = false;
            }

            CleanupItem();
        }

        #endregion

        #region Private Methods

        private void SpawnItem(InventoryItemData itemData)
        {
            CleanupItem();

            if (_itemSpawnPoint == null)
            {
                Debug.LogWarning("[ItemExaminer] No spawn point assigned!");
                return;
            }

            _currentItem = Instantiate(itemData.worldPrefab, _itemSpawnPoint.position, Quaternion.identity);
            _currentItem.name = "ExamineItem_" + itemData.displayName;

            DisablePhysics(_currentItem);
            CenterItem(_currentItem);
        }

        private void DisablePhysics(GameObject obj)
        {
            foreach (var rb in obj.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            foreach (var col in obj.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            foreach (var mb in obj.GetComponentsInChildren<MonoBehaviour>())
            {
                mb.enabled = false;
            }
        }

        private void CenterItem(GameObject obj)
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

            if (!hasBounds) return;

            Vector3 offset = _itemSpawnPoint.position - bounds.center;
            obj.transform.position += offset;
        }

        private void HandleRotationInput()
        {
            if (_currentItem == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (IsMouseOverDisplay())
                {
                    _isDragging = true;
                    _lastMouseX = Input.mousePosition.x;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }

            if (_isDragging)
            {
                float deltaX = Input.mousePosition.x - _lastMouseX;
                _currentYRotation -= deltaX * _rotationSpeed;
                _currentItem.transform.rotation = Quaternion.Euler(0f, _currentYRotation, 0f);
                _lastMouseX = Input.mousePosition.x;
            }
        }

        private bool IsMouseOverDisplay()
        {
            if (_itemDisplayImage == null) return false;

            RectTransform rect = _itemDisplayImage.rectTransform;

            // Input.mousePosition is in main camera screen space, so always use
            // Camera.main for the projection — NOT the canvas's Event Camera
            // (which is the Visor Overlay Camera used for rendering only).
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, Camera.main);
        }

        private void CleanupItem()
        {
            if (_currentItem != null)
            {
                Destroy(_currentItem);
                _currentItem = null;
            }
        }

        #endregion
    }
}
