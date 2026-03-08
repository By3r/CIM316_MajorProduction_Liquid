using UnityEngine;

namespace _Scripts.Systems.Terminal.UI
{
    /// <summary>
    /// Renders a 3D item prefab onto a RenderTexture with auto-rotation.
    /// Attach to the "Terminal Notification Camera" stage.
    /// Call Show(prefab) to spawn and start rotating, Hide() to clean up.
    /// </summary>
    public class NotificationItemPreview : MonoBehaviour
    {
        [Header("Render Setup")]
        [SerializeField] private Camera _previewCamera;
        [SerializeField] private Transform _itemSpawnPoint;
        [SerializeField] private RenderTexture _renderTexture;

        [Header("Rotation")]
        [SerializeField] private float _rotationSpeed = 60f;

        private GameObject _currentItem;

        public RenderTexture RenderTexture => _renderTexture;

        private void Awake()
        {
            if (_previewCamera != null && _renderTexture != null)
            {
                _previewCamera.targetTexture = _renderTexture;
                _previewCamera.enabled = false;
            }
        }

        private void Update()
        {
            if (_currentItem == null) return;
            _currentItem.transform.Rotate(Vector3.up, _rotationSpeed * Time.unscaledDeltaTime, Space.World);
        }

        public void Show(GameObject prefab)
        {
            Hide();

            if (prefab == null || _itemSpawnPoint == null) return;

            _currentItem = Instantiate(prefab, _itemSpawnPoint.position, Quaternion.identity);
            _currentItem.name = "NotificationPreview";

            DisablePhysicsAndScripts(_currentItem);
            CenterOnSpawnPoint(_currentItem);

            if (_previewCamera != null)
                _previewCamera.enabled = true;
        }

        public void Hide()
        {
            if (_currentItem != null)
            {
                Destroy(_currentItem);
                _currentItem = null;
            }

            if (_previewCamera != null)
                _previewCamera.enabled = false;
        }

        private void DisablePhysicsAndScripts(GameObject obj)
        {
            foreach (var rb in obj.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            foreach (var col in obj.GetComponentsInChildren<Collider>())
                col.enabled = false;

            foreach (var mb in obj.GetComponentsInChildren<MonoBehaviour>())
                mb.enabled = false;
        }

        private void CenterOnSpawnPoint(GameObject obj)
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
    }
}
