using UnityEngine;

public class FloatingCamera : MonoBehaviour
{
    #region Variables
    [Header("Movement Limits")]
    public float MinX = -1f;
    public float MaxX = 1f;

    public float MinY = 1.5f;
    public float MaxY = 3f;

    [Header("Rotation Limits")]
    public float MinRotX = -10f;
    public float MaxRotX = 10f;

    public float MinRotZ = -10f;
    public float MaxRotZ = 10f;

    [Header("Speed")]
    public float MoveSpeed = 0.2f;
    public float RotationSpeed = 0.2f;

    private Vector3 StartLocalPosition;
    private Vector3 StartLocalEulerAngles;

    private float PositionNoiseSeedX;
    private float PositionNoiseSeedY;
    private float RotationNoiseSeedX;
    private float RotationNoiseSeedZ;
    #endregion

    private void Start()
    {
        StartLocalPosition = transform.localPosition;
        StartLocalEulerAngles = transform.localEulerAngles;

        PositionNoiseSeedX = Random.Range(0f, 1000f);
        PositionNoiseSeedY = Random.Range(0f, 1000f);
        RotationNoiseSeedX = Random.Range(0f, 1000f);
        RotationNoiseSeedZ = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        UpdatePosition();
        UpdateRotation();
    }

    private void UpdatePosition()
    {
        float timeX = Time.time * MoveSpeed + PositionNoiseSeedX;
        float timeY = Time.time * MoveSpeed + PositionNoiseSeedY;

        float noiseX = Mathf.PerlinNoise(timeX, 0f);
        float noiseY = Mathf.PerlinNoise(timeY, 0f);

        float targetX = Mathf.Lerp(MinX, MaxX, noiseX);
        float targetY = Mathf.Lerp(MinY, MaxY, noiseY);

        Vector3 localPosition = transform.localPosition;
        localPosition.x = StartLocalPosition.x + targetX;
        localPosition.y = StartLocalPosition.y + targetY;

        transform.localPosition = localPosition;
    }

    private void UpdateRotation()
    {
        float timeRotX = Time.time * RotationSpeed + RotationNoiseSeedX;
        float timeRotZ = Time.time * RotationSpeed + RotationNoiseSeedZ;

        float noiseRotX = Mathf.PerlinNoise(timeRotX, 0f);
        float noiseRotZ = Mathf.PerlinNoise(timeRotZ, 0f);

        float rotX = Mathf.Lerp(MinRotX, MaxRotX, noiseRotX);
        float rotZ = Mathf.Lerp(MinRotZ, MaxRotZ, noiseRotZ);

        transform.localRotation = Quaternion.Euler(StartLocalEulerAngles.x + rotX, StartLocalEulerAngles.y, StartLocalEulerAngles.z + rotZ);
    }
}