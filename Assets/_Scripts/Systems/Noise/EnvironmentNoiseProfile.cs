using UnityEngine;

public enum NoiseCategory
{
    Footsteps,
    Sprint,
    Jump,
    Gunshot,
    ObjectImpact,
    Other
}

[CreateAssetMenu(fileName = "EnvNoiseProfile", menuName = "Audio/Environment Noise Profile")]

public class EnvironmentNoiseProfile : ScriptableObject
{
    #region Variables
    [Header("Meta")]
    [SerializeField] private string environmentName = "Constructed Mine";

    [Header("Global behaviour")]
    [Tooltip("Multiplier for ALL noise radii in this environment. < 1 = muffled, > 1 = carries far.")]
    [SerializeField] private float globalRadiusMultiplier = 1f;

    [Tooltip("Background noise level. Higher means small noises are harder to pick up.")]
    [Range(0f, 1f)]
    [SerializeField] private float ambientNoiseLevel = 0f;

    [Header("Per-category radius multipliers")]
    [SerializeField] private float footstepsRadiusMultiplier = 1f;
    [SerializeField] private float sprintRadiusMultiplier = 1.2f;
    [SerializeField] private float jumpRadiusMultiplier = 1.1f;
    [SerializeField] private float gunshotRadiusMultiplier = 2.5f;
    [SerializeField] private float objectImpactRadiusMultiplier = 1.5f;
    #endregion
    public float GlobalRadiusMultiplier => globalRadiusMultiplier;
    public float AmbientNoiseLevel => ambientNoiseLevel;

    public float GetRadiusMultiplier(NoiseCategory category)
    {
        switch (category)
        {
            case NoiseCategory.Footsteps: return footstepsRadiusMultiplier;
            case NoiseCategory.Sprint: return sprintRadiusMultiplier;
            case NoiseCategory.Jump: return jumpRadiusMultiplier;
            case NoiseCategory.Gunshot: return gunshotRadiusMultiplier;
            case NoiseCategory.ObjectImpact: return objectImpactRadiusMultiplier;
            default: return 1f;
        }
    }
}
