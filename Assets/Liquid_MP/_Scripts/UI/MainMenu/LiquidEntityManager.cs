using UnityEngine;

public class LiquidEntityManager : MonoBehaviour
{
    #region Variables
    [Header("References")]
    [SerializeField] private LightFlickerController lightFlickerController;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform flashlightTransform;
    [SerializeField] private Light flashlightLight;
    [SerializeField] private GameObject entityVisual;
    [SerializeField] private Collider entityCollider;
    [SerializeField] private Animator entityAnimator;

    [Header("Flashlight Detection")]
    [SerializeField] private float flashlightCheckDistance = 100f;
    [SerializeField] private LayerMask lineOfSightMask;
    [SerializeField] private float additionalFlashlightAngle = 2f;

    [Header("Behavior")]
    [SerializeField] private bool spawnOnFirstBlackout = true;
    [SerializeField] private bool allowDisappearWhenHit = true;
    [SerializeField][Range(0f, 1f)] private float disappearChanceWhenHit = 0.35f;

    private bool isVisible;
    private int currentSpawnIndex = -1;
    private int queuedSpawnIndex = -1;
    private int lastSpawnIndex = -1;
    private bool wasHitByFlashlightThisVisibleCycle;
    #endregion

    private void Awake()
    {
        if (entityVisual == null)
        {
            entityVisual = gameObject;
        }

        if (entityCollider == null)
        {
            entityCollider = GetComponent<Collider>();
        }

        if (entityAnimator == null)
        {
            entityAnimator = GetComponentInChildren<Animator>();
        }

        SetEntityVisible(false);
        QueueNextSpawnPointExcluding(-1, -1);
    }

    private void OnEnable()
    {
        if (lightFlickerController != null)
        {
            lightFlickerController.OnBlackoutStarted += HandleBlackoutStarted;
        }
    }

    private void OnDisable()
    {
        if (lightFlickerController != null)
        {
            lightFlickerController.OnBlackoutStarted -= HandleBlackoutStarted;
        }
    }

    private void Update()
    {
        if (!isVisible)
        {
            return;
        }

        CheckFlashlightHit();
    }

    private void HandleBlackoutStarted()
    {
        if (!spawnOnFirstBlackout && currentSpawnIndex == -1)
        {
            return;
        }

        if (!isVisible)
        {
            SpawnAtQueuedOrRandomPoint();
            return;
        }

        RelocateOrDisappear();
    }

    private void CheckFlashlightHit()
    {
        if (flashlightTransform == null || entityCollider == null)
        {
            return;
        }

        Vector3 targetPoint = entityCollider.bounds.center;
        Vector3 toTarget = targetPoint - flashlightTransform.position;
        float distanceToTarget = toTarget.magnitude;

        if (distanceToTarget > flashlightCheckDistance || distanceToTarget <= 0.0001f)
        {
            wasHitByFlashlightThisVisibleCycle = false;
            return;
        }

        Vector3 directionToTarget = toTarget / distanceToTarget;

        float maxAngle = 10f;

        if (flashlightLight != null && flashlightLight.type == LightType.Spot)
        {
            maxAngle = (flashlightLight.spotAngle * 0.5f) + additionalFlashlightAngle;
        }

        float angleToTarget = Vector3.Angle(flashlightTransform.forward, directionToTarget);

        if (angleToTarget > maxAngle)
        {
            wasHitByFlashlightThisVisibleCycle = false;
            return;
        }

        Vector3 rayOrigin = flashlightTransform.position;
        Vector3 rayDirection = directionToTarget;

        bool isCurrentlyHit = false;

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, flashlightCheckDistance, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == entityCollider || hit.collider.transform.IsChildOf(transform))
            {
                isCurrentlyHit = true;

                if (!wasHitByFlashlightThisVisibleCycle)
                {
                    wasHitByFlashlightThisVisibleCycle = true;
                    RelocateOrDisappear();
                }
            }
        }

        if (!isCurrentlyHit)
        {
            wasHitByFlashlightThisVisibleCycle = false;
        }
    }

    private void SpawnAtQueuedOrRandomPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points assigned.");
            return;
        }

        int nextIndex = queuedSpawnIndex;

        if (nextIndex < 0 || nextIndex >= spawnPoints.Length)
        {
            nextIndex = GetRandomSpawnIndexExcluding(currentSpawnIndex, lastSpawnIndex);
        }

        MoveToSpawnPoint(nextIndex);
        SetEntityVisible(true);
        wasHitByFlashlightThisVisibleCycle = false;
        QueueNextSpawnPointExcluding(currentSpawnIndex, lastSpawnIndex);
    }

    private void RelocateOrDisappear()
    {
        if (allowDisappearWhenHit && Random.value <= disappearChanceWhenHit)
        {
            QueueNextSpawnPointExcluding(currentSpawnIndex, lastSpawnIndex);
            SetEntityVisible(false);
            currentSpawnIndex = -1;
            wasHitByFlashlightThisVisibleCycle = false;
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return;
        }

        int nextIndex = GetRandomSpawnIndexExcluding(currentSpawnIndex, lastSpawnIndex);

        if (nextIndex == -1)
        {
            QueueNextSpawnPointExcluding(currentSpawnIndex, lastSpawnIndex);
            SetEntityVisible(false);
            currentSpawnIndex = -1;
            wasHitByFlashlightThisVisibleCycle = false;
            return;
        }

        MoveToSpawnPoint(nextIndex);
        SetEntityVisible(true);
        wasHitByFlashlightThisVisibleCycle = false;
        QueueNextSpawnPointExcluding(currentSpawnIndex, lastSpawnIndex);
    }

    private void MoveToSpawnPoint(int spawnIndex)
    {
        if (spawnIndex < 0 || spawnIndex >= spawnPoints.Length)
        {
            return;
        }

        lastSpawnIndex = currentSpawnIndex;
        currentSpawnIndex = spawnIndex;

        Vector3 targetPosition = spawnPoints[spawnIndex].position;
        Quaternion targetRotation = spawnPoints[spawnIndex].rotation;

        if (entityAnimator != null && entityAnimator.applyRootMotion)
        {
            entityAnimator.applyRootMotion = false;
        }

        entityVisual.transform.position = targetPosition;
    }

    private void QueueNextSpawnPointExcluding(int excludedIndexA, int excludedIndexB)
    {
        queuedSpawnIndex = GetRandomSpawnIndexExcluding(excludedIndexA, excludedIndexB);
    }

    private int GetRandomSpawnIndexExcluding(int excludedIndexA, int excludedIndexB)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return -1;
        }

        if (spawnPoints.Length == 1)
        {
            return (excludedIndexA == 0 || excludedIndexB == 0) ? -1 : 0;
        }

        int[] validIndices = new int[spawnPoints.Length];
        int validCount = 0;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (i == excludedIndexA || i == excludedIndexB)
            {
                continue;
            }

            validIndices[validCount] = i;
            validCount++;
        }

        if (validCount == 0)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (i == excludedIndexA)
                {
                    continue;
                }

                validIndices[validCount] = i;
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return -1;
        }

        return validIndices[Random.Range(0, validCount)];
    }

    private void SetEntityVisible(bool visible)
    {
        isVisible = visible;

        if (entityVisual != null)
        {
            entityVisual.SetActive(visible);
        }

        if (entityCollider != null)
        {
            entityCollider.enabled = visible;
        }
    }
}