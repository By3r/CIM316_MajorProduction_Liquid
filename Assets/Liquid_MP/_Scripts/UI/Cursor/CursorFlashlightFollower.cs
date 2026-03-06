using UnityEngine;
using UnityEngine.InputSystem;

public class CursorFlashlightFollower : MonoBehaviour
{
    #region Variables
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform spotlightTransform;
    [SerializeField] private LayerMask aimLayerMask;
    [SerializeField] private float rotateSpeed = 12f;
    #endregion

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null || spotlightTransform == null || Mouse.current == null)
        {
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = targetCamera.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, aimLayerMask))
        {
            Vector3 direction = hit.point - spotlightTransform.position;

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            spotlightTransform.rotation = Quaternion.Slerp(spotlightTransform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
        }
    }
}