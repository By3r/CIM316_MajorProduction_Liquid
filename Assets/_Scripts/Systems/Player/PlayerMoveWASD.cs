using UnityEngine;

public class PlayerMoveWASD : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;

    [Header("Bounds")]
    [SerializeField] float xBound = 5f;
    [SerializeField] float zBound = 5f;

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 move = new Vector3(h, 0f, v).normalized * moveSpeed * Time.deltaTime;

        Vector3 p = transform.position + move;
        p.x = Mathf.Clamp(p.x, -xBound, xBound);
        p.z = Mathf.Clamp(p.z, -zBound, zBound);
        transform.position = p;
    }
}