using Liquid.Audio;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomNoisePreset : MonoBehaviour
{
    [Header("Environment Selection")]
    [SerializeField] private bool pickRandomEnvironment = true;
    [SerializeField] private bool useDeterministicSeed = true;
    [SerializeField] private int randomSeed = 12345;
    [SerializeField] private EnvironmentNoiseProfile[] randomEnvironments;
    [SerializeField] private EnvironmentNoiseProfile fixedEnvironment;

    [Header("Per-Room Tuning")]
    [SerializeField] private float roomRadiusMultiplier = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float roomAmbientAdd01 = 0f;

    [Header("Bounds")]
    [SerializeField] private Collider boundsCollider;

    public EnvironmentNoiseProfile ActiveProfile { get; private set; }
    public float RoomRadiusMultiplier => roomRadiusMultiplier;

    private void Awake()
    {
        if (boundsCollider == null)
            boundsCollider = GetComponent<Collider>();

        SelectProfile();
    }

    private void SelectProfile()
    {
        if (pickRandomEnvironment && randomEnvironments != null && randomEnvironments.Length > 0)
        {
            int index;
            if (useDeterministicSeed)
            {
                var state = Random.state;
                Random.InitState(randomSeed ^ gameObject.GetInstanceID());
                index = Random.Range(0, randomEnvironments.Length);
                Random.state = state;
            }
            else
            {
                index = Random.Range(0, randomEnvironments.Length);
            }
            ActiveProfile = randomEnvironments[index];
        }
        else
        {
            ActiveProfile = fixedEnvironment;
        }

        if (ActiveProfile == null)
            Debug.LogWarning($"[RoomNoisePreset] No EnvironmentNoiseProfile on '{name}'. Base values will be used.", this);
    }

    public bool ContainsPoint(Vector3 worldPosition)
    {
        if (boundsCollider == null) return false;
        return boundsCollider.bounds.Contains(worldPosition);
    }

    /// <summary>
    /// Applies the room's profile multiplier and room radius multiplier to a base noise value.
    /// </summary>
    public float ApplyMultiplier(float baseNoise, NoiseCategory category)
    {
        float profileMult = ActiveProfile != null ? ActiveProfile.GetMultiplier(category) : 1f;
        return baseNoise * profileMult;
    }

    public float GetAmbient01()
    {
        float envAmbient = ActiveProfile != null ? ActiveProfile.AmbientNoiseLevel : 0f;
        return Mathf.Clamp01(envAmbient + roomAmbientAdd01);
    }
}