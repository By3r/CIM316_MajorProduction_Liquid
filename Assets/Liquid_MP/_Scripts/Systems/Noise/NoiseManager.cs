using System.Collections.Generic;
using UnityEngine;

namespace Liquid.Audio
{
    public struct NoiseEvent
    {
        public Vector3 worldPosition;
        public float baseRadius;
        public float finalRadius;
        public NoiseLevel level;
        public NoiseCategory category;
        public EnvironmentNoiseProfile environmentProfile;
        public float intensity01;
        public float perceivedLoudness01;
    }

    public interface INoiseListener
    {
        void OnNoiseHeard(NoiseEvent noiseEvent);
    }

    [DisallowMultipleComponent]
    public class NoiseManager : MonoBehaviour
    {
        [Header("Base Radius Per Level")]
        [SerializeField] private float lowBaseRadius = 4f;
        [SerializeField] private float mediumBaseRadius = 8f;
        [SerializeField] private float highBaseRadius = 12f;
        [SerializeField] private float maximumBaseRadius = 18f;

        [Header("Intensity Per Level (0..1)")]
        [SerializeField] private float lowIntensity01 = 0.25f;
        [SerializeField] private float mediumIntensity01 = 0.45f;
        [SerializeField] private float highIntensity01 = 0.7f;
        [SerializeField] private float maximumIntensity01 = 1f;

        [Header("Category Radius Multipliers")]
        [SerializeField] private float footstepsMultiplier = 1f;
        [SerializeField] private float sprintMultiplier = 1.3f;
        [SerializeField] private float jumpMultiplier = 1.1f;
        [SerializeField] private float gunshotMultiplier = 2f;
        [SerializeField] private float objectImpactMultiplier = 1.5f;
        [SerializeField] private float otherMultiplier = 1f;

        [Header("Occlusion")]
        [SerializeField] private LayerMask occlusionMask;
        [Range(0f, 1f)]
        [SerializeField] private float occludedLoudnessMultiplier = 0.35f;

        [Header("Distance Falloff")]
        [SerializeField] private bool useDistanceFalloff = true;
        [SerializeField] private AnimationCurve falloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        public static NoiseManager Instance { get; private set; }

        private readonly List<INoiseListener> _listeners = new List<INoiseListener>();
        private readonly List<RoomNoisePreset> _rooms = new List<RoomNoisePreset>(64);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            RefreshRooms();
        }

        public void RefreshRooms()
        {
            _rooms.Clear();
            _rooms.AddRange(FindObjectsByType<RoomNoisePreset>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        private RoomNoisePreset FindRoomAt(Vector3 position)
        {
            for (int i = 0; i < _rooms.Count; i++)
                if (_rooms[i] != null && _rooms[i].ContainsPoint(position))
                    return _rooms[i];
            return null;
        }

        public void RegisterListener(INoiseListener listener)
        {
            if (listener != null && !_listeners.Contains(listener)) _listeners.Add(listener);
        }

        public void UnregisterListener(INoiseListener listener)
        {
            if (listener != null) _listeners.Remove(listener);
        }

        /// <summary>
        /// Call this from player or object scripts to emit a noise.
        /// Automatically detects which room the noise is in.
        /// Optionally pass an environmentProfile to override the room profile.
        /// </summary>
        public void EmitNoise(Vector3 position, NoiseLevel level, NoiseCategory category, EnvironmentNoiseProfile overrideProfile = null)
        {
            float baseRadius = GetBaseRadiusForLevel(level) * GetCategoryMultiplier(category);
            float finalRadius = baseRadius;
            float intensity01 = GetIntensityForLevel(level);
            float ambient01 = 0f;

            // Auto-detect room if no override profile given
            EnvironmentNoiseProfile profile = overrideProfile;
            RoomNoisePreset room = FindRoomAt(position);

            if (profile == null && room != null)
                profile = room.ActiveProfile;

            if (profile != null)
            {
                finalRadius *= Mathf.Max(0.01f, profile.GlobalRadiusMultiplier);
                finalRadius *= Mathf.Max(0.01f, profile.GetRadiusMultiplier(category));
                ambient01 = Mathf.Clamp01(profile.AmbientNoiseLevel);
            }

            // Apply per-room tuning on top
            if (room != null)
            {
                finalRadius *= room.RoomRadiusMultiplier;
                ambient01 = Mathf.Clamp01(ambient01 + room.GetAmbient01());
            }

            finalRadius = Mathf.Max(0.01f, finalRadius);
            intensity01 = Mathf.Clamp01(intensity01);

            NoiseEvent noiseEvent = new NoiseEvent
            {
                worldPosition = position,
                baseRadius = baseRadius,
                finalRadius = finalRadius,
                level = level,
                category = category,
                environmentProfile = profile,
                intensity01 = intensity01
            };

            if (showDebugLogs)
                Debug.Log($"[Noise] {level} {category} at {position} | base={baseRadius:0.0} final={finalRadius:0.0} intensity={intensity01:0.00} ambient={ambient01:0.00} room={room?.name ?? "None"} env={profile?.name ?? "None"}");

            NotifyListeners(noiseEvent, ambient01);
        }

        private void NotifyListeners(NoiseEvent noiseEvent, float ambient01)
        {
            float maxDistSqr = noiseEvent.finalRadius * noiseEvent.finalRadius;

            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                INoiseListener listener = _listeners[i];
                if (listener == null) { _listeners.RemoveAt(i); continue; }
                if (!(listener is Component component) || component == null) continue;

                float distSqr = (component.transform.position - noiseEvent.worldPosition).sqrMagnitude;
                if (distSqr > maxDistSqr) continue;

                float dist = Mathf.Sqrt(distSqr);
                float perceived = ComputePerceivedLoudness(noiseEvent, component.transform.position, dist, ambient01);

                EnemyNoiseHearingProfile hearingProfile = component.GetComponent<EnemyNoiseHearingProfile>();
                if (hearingProfile != null)
                {
                    perceived *= Mathf.Max(0.01f, hearingProfile.HearingSensitivity);
                    perceived *= Mathf.Max(0.01f, hearingProfile.GetCategoryMultiplier(noiseEvent.category));
                    if (perceived < hearingProfile.MinPerceivedLoudness) continue;
                }
                else
                {
                    if (perceived < 0.08f) continue;
                }

                noiseEvent.perceivedLoudness01 = perceived;
                listener.OnNoiseHeard(noiseEvent);
            }
        }

        private float ComputePerceivedLoudness(NoiseEvent noiseEvent, Vector3 listenerPos, float distance, float ambient01)
        {
            float loudness = noiseEvent.intensity01;

            if (useDistanceFalloff)
            {
                float t = Mathf.Clamp01(distance / noiseEvent.finalRadius);
                loudness *= Mathf.Clamp01(falloffCurve != null ? falloffCurve.Evaluate(t) : (1f - t));
            }

            if (occlusionMask.value != 0)
            {
                Vector3 origin = noiseEvent.worldPosition + Vector3.up * 0.5f;
                Vector3 target = listenerPos + Vector3.up * 1.2f;
                Vector3 dir = target - origin;
                float len = dir.magnitude;
                if (len > 0.001f && Physics.Raycast(origin, dir / len, len, occlusionMask, QueryTriggerInteraction.Ignore))
                    loudness *= occludedLoudnessMultiplier;
            }

            loudness = Mathf.Max(0f, loudness - ambient01 * 0.6f);
            return Mathf.Clamp01(loudness);
        }

        private float GetBaseRadiusForLevel(NoiseLevel level)
        {
            switch (level)
            {
                case NoiseLevel.Low: return lowBaseRadius;
                case NoiseLevel.Medium: return mediumBaseRadius;
                case NoiseLevel.High: return highBaseRadius;
                case NoiseLevel.Extreme: return maximumBaseRadius;
                default: return lowBaseRadius;
            }
        }

        private float GetIntensityForLevel(NoiseLevel level)
        {
            switch (level)
            {
                case NoiseLevel.Low: return lowIntensity01;
                case NoiseLevel.Medium: return mediumIntensity01;
                case NoiseLevel.High: return highIntensity01;
                case NoiseLevel.Extreme: return maximumIntensity01;
                default: return lowIntensity01;
            }
        }

        private float GetCategoryMultiplier(NoiseCategory category)
        {
            switch (category)
            {
                case NoiseCategory.Footsteps: return footstepsMultiplier;
                case NoiseCategory.Sprint: return sprintMultiplier;
                case NoiseCategory.Jump: return jumpMultiplier;
                case NoiseCategory.Gunshot: return gunshotMultiplier;
                case NoiseCategory.ObjectImpact: return objectImpactMultiplier;
                default: return otherMultiplier;
            }
        }
    }
}