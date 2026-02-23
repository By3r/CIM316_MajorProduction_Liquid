using UnityEngine;
using PackageDoor;

public class DoorTrigger : MonoBehaviour
{
    private Door door;

    void Start()
    {
        door = GetComponentInParent<Door>();
    }

    void OnTriggerEnter(Collider c)
    {

        door.openDoor(c);

    }

    void OnTriggerExit(Collider c)
    {
        door.closeDoor(c);
    }
}
