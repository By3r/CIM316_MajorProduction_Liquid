using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Automatically makes ALL Unity Lights lethal to Lurkers by attaching a trigger hazard to each light.
/// Works with procedural generation: newly spawned lights get picked up too.
/// </summary>
public sealed class LethalLightSystem : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("How often to rescan for new lights (seconds).")]
    [SerializeField] private float rescanInterval = 1.0f;

    [Header("Lethal Threshold")]
    [Tooltip("Only lights with intensity >= this are considered lethal.")]
    [SerializeField] private float lethalIntensityThreshold = 0.5f;

    [Header("Hazard Shape")]
    [Tooltip("Default radius if we can't infer range from the light.")]
    [SerializeField] private float fallbackRadius = 6f;

    [Tooltip("Extra multiplier applied to computed radius (tuning knob).")]
    [SerializeField] private float radiusMultiplier = 1.0f;

    [Header("Damage")]
    [Tooltip("Damage per second when inside lethal light.")]
    [SerializeField] private float damagePerSecond = 25f;

    private readonly HashSet<int> _processedLightInstanceIds = new HashSet<int>();
    private float _nextScanTime;

    private void Update()
    {
        if (Time.time < _nextScanTime)
        {
            return;
        }

        _nextScanTime = Time.time + Mathf.Max(0.1f, rescanInterval);
        ScanAndAttach();
    }

    private void ScanAndAttach()
    {
        Light[] lights = Object.FindObjectsByType<Light>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            if (l == null)
            {
                continue;
            }

            int id = l.GetInstanceID();
            if (_processedLightInstanceIds.Contains(id))
            {
                continue;
            }

            _processedLightInstanceIds.Add(id);
            CreateHazardForLight(l);
        }
    }

    private void CreateHazardForLight(Light light)
    {
        GameObject hazardObj = new GameObject("LurkerLightHazard");
        hazardObj.transform.SetParent(light.transform, worldPositionStays: false);
        hazardObj.transform.localPosition = Vector3.zero;
        hazardObj.transform.localRotation = Quaternion.identity;

        // Use a SphereCollider by default. This covers point lights and is "good enough" for spot lights too.
        SphereCollider trigger = hazardObj.AddComponent<SphereCollider>();
        trigger.isTrigger = true;

        float radius = ComputeRadius(light);
        trigger.radius = Mathf.Max(0.25f, radius);

        // Add hazard logic.
        LurkerLightHazard hazard = hazardObj.AddComponent<LurkerLightHazard>();
        hazard.ConfigureFromManager(
            lethalIntensityThreshold,
            damagePerSecond);

        // Link to the source light so it can read intensity/enabled state.
        hazard.BindToLight(light);
    }

    private float ComputeRadius(Light light)
    {
        // Point/Spot light range exists; directional has no range.
        float baseRadius = fallbackRadius;

        if (light.type == LightType.Point || light.type == LightType.Spot)
        {
            baseRadius = Mathf.Max(0.25f, light.range);
        }

        // Directional: you probably only want this lethal if you simulate sunlight / global light.
        // We'll use fallbackRadius, but you can change this behavior easily later.
        return baseRadius * Mathf.Max(0.01f, radiusMultiplier);
    }
}