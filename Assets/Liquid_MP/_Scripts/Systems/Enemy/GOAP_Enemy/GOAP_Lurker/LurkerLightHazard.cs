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
        EnsureTrigger();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureTrigger();
    }
#endif

    private void EnsureTrigger()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
        {
            c.isTrigger = true;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (sourceLight == null || !sourceLight.enabled)
        {
            return;
        }

        float intensity01 = Mathf.Clamp01(sourceLight.intensity);
        if (intensity01 < brightThreshold01)
        {
            return;
        }

        // Support lurker collider being on child objects
        LurkerEnemy lurker = other.GetComponent<LurkerEnemy>();
        if (lurker == null)
        {
            lurker = other.GetComponentInParent<LurkerEnemy>();
        }

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