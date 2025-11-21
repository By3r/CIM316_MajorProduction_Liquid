using UnityEngine;
using UnityEngine.InputSystem;

public class FauxPlayerMovement : MonoBehaviour
{
    /// <summary>
    /// TO BE DELETED. Just used for OLD demo purposes.
    /// </summary>
    #region Variables
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Boundary Settings")]
    [SerializeField] private float maxXDistance = 10f;
    [SerializeField] private float maxZDistance = 10f;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    #endregion

    private void OnEnable()
    {
        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.Disable();
        }
    }

    private void Update()
    {
        HandleMovement();
        HandleBoundaries();
    }

    private void HandleMovement()
    {
        if (moveAction == null || moveAction.action == null)
            return;

        Vector2 input = moveAction.action.ReadValue<Vector2>();

        Vector3 moveDirection = new Vector3(input.x, 0f, input.y);

        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);
    }

    private void HandleBoundaries()
    {
        Vector3 position = transform.position;

        if (Mathf.Abs(position.x) >= maxXDistance)
        {
            position.x = 0f;
        }

        if (Mathf.Abs(position.z) >= maxZDistance)
        {
            position.z = 0f;
        }

        transform.position = position;
    }
}
