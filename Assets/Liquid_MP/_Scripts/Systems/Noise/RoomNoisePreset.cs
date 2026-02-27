using UnityEngine;

[DisallowMultipleComponent]
public class RoomNoisePreset : MonoBehaviour
{
    [Header("Environment selection")]
    [Tooltip("If true, a random profile from 'randomEnvironments' will be picked on Awake.")]
    [SerializeField] private bool pickRandomEnvironment = true;

    [Tooltip("If true, random selection is deterministic using 'randomSeed'.")]
    [SerializeField] private bool useDeterministicSeed = true;

    [SerializeField] private int randomSeed = 12345;

    [Tooltip("Possible environments this room can randomly use.")]
    [SerializeField] private EnvironmentNoiseProfile[] randomEnvironments;

    [Tooltip("If random is false, use this specific environment.")]
    [SerializeField] private EnvironmentNoiseProfile fixedEnvironment;

    [Header("Per-room tuning")]
    [Tooltip("Fine-tune how far noises travel in THIS room (on top of the environment profile).")]
    [SerializeField] private float roomRadiusMultiplier = 1f;

    [Tooltip("Optional: additional ambient noise in this room (adds to profile ambient).")]
    [Range(0f, 1f)]
    [SerializeField] private float roomAmbientAdd01 = 0f;

    [Header("Bounds")]
    [Tooltip("If set, used to determine if a point is inside this room. If null, tries to find a Collider on this GameObject.")]
    [SerializeField] private Collider boundsCollider;

    public EnvironmentNoiseProfile ActiveProfile { get; private set; }
    public float RoomRadiusMultiplier => roomRadiusMultiplier;
    public float RoomAmbientAdd01 => roomAmbientAdd01;

    private void Awake()
    {
        if (boundsCollider == null)
        {
            boundsCollider = GetComponent<Collider>();
        }

        SelectProfile();
    }

    private void SelectProfile()
    {
        if (pickRandomEnvironment)
        {
            if (randomEnvironments != null && randomEnvironments.Length > 0)
            {
                int index;

                if (useDeterministicSeed)
                {
                    // Deterministic pick per room instance (stable across sessions if seed is stable)
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
        }
        else
        {
            ActiveProfile = fixedEnvironment;
        }

        if (ActiveProfile == null)
        {
            Debug.LogWarning(
                $"[RoomNoisePreset] No EnvironmentNoiseProfile assigned on '{name}'. Noise will use base values only.",
                this);
        }
    }

    /// <summary>
    /// Returns true if the given point lies inside this room's collider bounds (approx).
    /// </summary>
    public bool ContainsPoint(Vector3 worldPosition)
    {
        if (boundsCollider == null)
            return false;

        return boundsCollider.bounds.Contains(worldPosition);
    }

    /// <summary>
    /// Returns the final radius for a noise made in this room.
    /// </summary>
    public float GetFinalRadius(float baseRadius, NoiseCategory category)
    {
        float envMultiplier = 1f;

        if (ActiveProfile != null)
        {
            envMultiplier =
                ActiveProfile.GlobalRadiusMultiplier *
                ActiveProfile.GetRadiusMultiplier(category);
        }

        return baseRadius * envMultiplier * roomRadiusMultiplier;
    }

    public float GetAmbient01()
    {
        float envAmbient = ActiveProfile != null ? ActiveProfile.AmbientNoiseLevel : 0f;
        return Mathf.Clamp01(envAmbient + roomAmbientAdd01);
    }
}