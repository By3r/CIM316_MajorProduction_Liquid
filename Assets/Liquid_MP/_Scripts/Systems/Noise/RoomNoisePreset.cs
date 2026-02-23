using UnityEngine;

public class RoomNoisePreset : MonoBehaviour
{
    [Header("Environment selection")]
    [Tooltip("If true, a random profile from 'randomEnvironments' will be picked on Awake.")]
    [SerializeField] private bool pickRandomEnvironment = true;

    [Tooltip("Possible environments this room can randomly use.")]
    [SerializeField] private EnvironmentNoiseProfile[] randomEnvironments;

    [Tooltip("If random is false, use this specific environment.")]
    [SerializeField] private EnvironmentNoiseProfile fixedEnvironment;

    [Header("Per-room tuning")]
    [Tooltip("Fine-tune how far noises travel in THIS room (on top of the environment profile).")]
    [SerializeField] private float roomRadiusMultiplier = 1f;

    public EnvironmentNoiseProfile ActiveProfile { get; private set; }
    public float RoomRadiusMultiplier => roomRadiusMultiplier;

    private void Awake()
    {
        if (pickRandomEnvironment)
        {
            if (randomEnvironments != null && randomEnvironments.Length > 0)
            {
                int index = Random.Range(0, randomEnvironments.Length);
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
                $"[RoomNoisePreset] No EnvironmentNoiseProfile assigned on '{name}'. " +
                "Noise will use base values only.");
        }
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
}