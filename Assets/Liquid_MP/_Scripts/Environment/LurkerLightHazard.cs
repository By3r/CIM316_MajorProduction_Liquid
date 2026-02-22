using UnityEngine;

/// <summary>
/// Reactive light hazard bound to a Unity Light. Automatically applies damage to LurkerEnemy while inside.
/// The LethalLightSystem spawns this as a child of each Light.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class LurkerLightHazard : MonoBehaviour
{
    [Header("Runtime (read-only)")]
    [SerializeField] private Light sourceLight;

    [SerializeField] private float brightThreshold01 = 0.5f;
    [SerializeField] private float damagePerSecond = 25f;

    public void BindToLight(Light light)
    {
        sourceLight = light;
    }

    public void ConfigureFromManager(float brightThreshold, float dps)
    {
        brightThreshold01 = Mathf.Clamp01(brightThreshold);
        damagePerSecond = Mathf.Max(0f, dps);
    }

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
        {
            c.isTrigger = true;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
        {
            c.isTrigger = true;
        }
    }
#endif

    private void OnTriggerStay(Collider other)
    {
        if (sourceLight == null)
        {
            return;
        }

        if (!sourceLight.enabled)
        {
            return;
        }

        float intensity = sourceLight.intensity;

        // Normalize/interpretation: we treat "intensity" as 0..1-ish for gameplay.
        // If your intensities are higher (common), you can clamp or map in one place here.
        float intensity01 = Mathf.Clamp01(intensity);

        if (intensity01 < brightThreshold01)
        {
            return;
        }

        LurkerEnemy lurker = other.GetComponent<LurkerEnemy>();
        if (lurker == null)
        {
            return;
        }

        lurker.NotifyHitByLight(
            lightSourceWorldPos: sourceLight.transform.position,
            intensity01: intensity01,
            damagePerSecond: damagePerSecond);
    }
}