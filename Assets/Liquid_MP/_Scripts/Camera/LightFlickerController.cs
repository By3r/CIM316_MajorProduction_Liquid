using System;
using UnityEngine;

public class LightFlickerController : MonoBehaviour
{
    [Header("References")]
    public Light[] Lights;

    [Header("Timing")]
    public float MinTimeBetweenChanges = 0.2f;
    public float MaxTimeBetweenChanges = 1.25f;

    [Header("Intensity Multipliers")]
    public float DarknessMultiplier = 0f;
    public float MinNormalMultiplier = 0.35f;
    public float MaxNormalMultiplier = 1f;

    [Header("Behavior")]
    [Range(0f, 1f)]
    public float DarknessChance = 0.2f;

    public float NormalIntensityChangeSpeed = 2f;
    public float DarknessIntensityChangeSpeed = 20f;

    public float CurrentMultiplier => currentMultiplier;
    public bool IsBlackout => currentMultiplier <= DarknessMultiplier + blackoutThreshold;

    public event Action OnBlackoutStarted;
    public event Action OnBlackoutEnded;

    private float[] baseIntensities;
    private float targetMultiplier;
    private float currentMultiplier;
    private float nextChangeTime;
    private bool wasBlackout;

    private const float blackoutThreshold = 0.001f;

    private void Awake()
    {
        if (Lights == null || Lights.Length == 0)
        {
            Debug.LogError("LightFlickerController: No lights assigned.");
            enabled = false;
            return;
        }

        baseIntensities = new float[Lights.Length];

        for (int i = 0; i < Lights.Length; i++)
        {
            if (Lights[i] == null)
            {
                Debug.LogError("LightFlickerController: One or more assigned lights are null.");
                enabled = false;
                return;
            }

            baseIntensities[i] = Lights[i].intensity;
        }

        currentMultiplier = 1f;
        targetMultiplier = currentMultiplier;
        wasBlackout = IsBlackout;

        ScheduleNextChange();
        ApplyIntensity();
    }

    private void Update()
    {
        float currentSpeed = targetMultiplier <= DarknessMultiplier + blackoutThreshold
            ? DarknessIntensityChangeSpeed
            : NormalIntensityChangeSpeed;

        currentMultiplier = Mathf.MoveTowards(
            currentMultiplier,
            targetMultiplier,
            currentSpeed * Time.deltaTime
        );

        ApplyIntensity();
        UpdateBlackoutState();

        if (Time.time >= nextChangeTime)
        {
            PickNextIntensity();
            ScheduleNextChange();
        }
    }

    private void PickNextIntensity()
    {
        bool goDark = UnityEngine.Random.value <= DarknessChance;

        if (goDark)
        {
            targetMultiplier = DarknessMultiplier;
        }
        else
        {
            targetMultiplier = UnityEngine.Random.Range(MinNormalMultiplier, MaxNormalMultiplier);
        }
    }

    private void ApplyIntensity()
    {
        for (int i = 0; i < Lights.Length; i++)
        {
            Lights[i].intensity = baseIntensities[i] * currentMultiplier;
        }
    }

    private void ScheduleNextChange()
    {
        nextChangeTime = Time.time + UnityEngine.Random.Range(MinTimeBetweenChanges, MaxTimeBetweenChanges);
    }

    private void UpdateBlackoutState()
    {
        bool isBlackout = IsBlackout;

        if (!wasBlackout && isBlackout)
        {
            OnBlackoutStarted?.Invoke();
        }
        else if (wasBlackout && !isBlackout)
        {
            OnBlackoutEnded?.Invoke();
        }

        wasBlackout = isBlackout;
    }
}