using System.Collections.Generic;
using UnityEngine;

namespace Liquid.Audio
{
    #region Noise event for the Enemies to react to.
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
    #endregion

    #region Know what noise was emitted.
    public interface INoiseListener
    {
        void OnNoiseHeard(NoiseEvent noiseEvent);
    }
    #endregion

    [DisallowMultipleComponent]
    public class NoiseManager : MonoBehaviour
    {
        #region Variables
        [Header("Base radius per level (before category / environment)")]
        [Tooltip("Meters at NoiseLevel.Low, before category and environment multipliers.")]
        [SerializeField] private float lowBaseRadius = 4f;
        [SerializeField] private float mediumBaseRadius = 8f;
        [SerializeField] private float highBaseRadius = 12f;
        [SerializeField] private float maximumBaseRadius = 18f;

        [Header("Intensity per level (0..1)")]
        [SerializeField] private float lowIntensity01 = 0.25f;
        [SerializeField] private float mediumIntensity01 = 0.45f;
        [SerializeField] private float highIntensity01 = 0.7f;
        [SerializeField] private float maximumIntensity01 = 1f;

        [Header("Category radius multipliers")]
        [SerializeField] private float footstepsMultiplier = 1f;
        [SerializeField] private float sprintMultiplier = 1.3f;
        [SerializeField] private float jumpMultiplier = 1.1f;
        [SerializeField] private float gunshotMultiplier = 2f;
        [SerializeField] private float objectImpactMultiplier = 1.5f;
        [SerializeField] private float otherMultiplier = 1f;

        [Header("Occlusion")]
        [Tooltip("Layers that can block sound (walls, doors, props).")]
        [SerializeField] private LayerMask occlusionMask;

        [Tooltip("If occluded, perceived loudness is multiplied by this.")]
        [Range(0f, 1f)]
        [SerializeField] private float occludedLoudnessMultiplier = 0.35f;

        [Header("Distance Falloff")]
        [Tooltip("If true, perceived loudness falls off with distance instead of being binary-in-radius.")]
        [SerializeField] private bool useDistanceFalloff = true;

        [Tooltip("Curve where X = normalized distance (0 near, 1 at max radius), Y = loudness multiplier.")]
        [SerializeField] private AnimationCurve falloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        [Header("Noise Zones")]
        [SerializeField] private NoiseZoneTracker zoneTracker;
        #endregion

        public static NoiseManager Instance { get; private set; }
        private readonly List<RoomNoisePreset> _rooms = new List<RoomNoisePreset>(64);

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
            {
                var r = _rooms[i];
                if (r != null && r.ContainsPoint(position))
                    return r;
            }
            return null;
        }
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple instances found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        #region Listeners
        private readonly List<INoiseListener> _listeners = new List<INoiseListener>();

        public void RegisterListener(INoiseListener listener)
        {
            if (listener == null) return;
            if (!_listeners.Contains(listener)) _listeners.Add(listener);
        }

        public void UnregisterListener(INoiseListener listener)
        {
            if (listener == null) return;
            _listeners.Remove(listener);
        }
        #endregion

        #region Public Functions
        /// <summary>
        /// Main entry point: call this when something makes noise.
        /// - position: where the noise happened in world space
        /// - level: how loud it is (Low, Medium, High, Maximum)
        /// - category: type of noise.
        /// - environmentProfile: /OPTIONAL/ environment preset to apply global + ambient multipliers.
        /// </summary>
        public void EmitNoise(Vector3 position, NoiseLevel level, NoiseCategory category, EnvironmentNoiseProfile environmentProfile = null)
        {
            float baseRadius = GetBaseRadiusForLevel(level);
            baseRadius *= GetCategoryRadiusMultiplier(category);

            float finalRadius = baseRadius;
            float intensity01 = GetIntensityForLevel(level);

            // Environment affects how far it carries and how masked it is
            float ambient01 = 0f;

            if (environmentProfile != null)
            {
                finalRadius *= Mathf.Max(0.01f, environmentProfile.GlobalRadiusMultiplier);
                finalRadius *= Mathf.Max(0.01f, environmentProfile.GetRadiusMultiplier(category));
                ambient01 = Mathf.Clamp01(environmentProfile.AmbientNoiseLevel);
            }

            // Clamp and build event
            finalRadius = Mathf.Max(0.01f, finalRadius);
            intensity01 = Mathf.Clamp01(intensity01);

            if (zoneTracker != null)
                zoneTracker.Apply(category, ref finalRadius, ref intensity01, ref ambient01);

            NoiseEvent noiseEvent = new NoiseEvent
            {
                worldPosition = position,
                baseRadius = baseRadius,
                finalRadius = finalRadius,
                level = level,
                category = category,
                environmentProfile = environmentProfile,
                intensity01 = intensity01
            };

            if (showDebugLogs)
            {
                string envName = environmentProfile != null ? environmentProfile.name : "None";
                Debug.Log($"[Noise] Emit {level} {category} at {position} base={baseRadius:0.0} final={finalRadius:0.0} intensity={intensity01:0.00} ambient={ambient01:0.00} env={envName}");
            }

            NotifyListeners(noiseEvent, ambient01);
        }
        #endregion

        #region Internals
        private float GetBaseRadiusForLevel(NoiseLevel level)
        {
            switch (level)
            {
                case NoiseLevel.Low: return lowBaseRadius;
                case NoiseLevel.Medium: return mediumBaseRadius;
                case NoiseLevel.High: return highBaseRadius;
                case NoiseLevel.Maximum: return maximumBaseRadius;
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
                case NoiseLevel.Maximum: return maximumIntensity01;
                default: return lowIntensity01;
            }
        }

        private float GetCategoryRadiusMultiplier(NoiseCategory category)
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

        private void NotifyListeners(NoiseEvent noiseEvent, float ambient01)
        {
            float maxDistanceSqr = noiseEvent.finalRadius * noiseEvent.finalRadius;

            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                INoiseListener listener = _listeners[i];
                if (listener == null)
                {
                    _listeners.RemoveAt(i);
                    continue;
                }

                if (!(listener is Component component) || component == null)
                    continue;

                Vector3 listenerPos = component.transform.position;
                float distSqr = (listenerPos - noiseEvent.worldPosition).sqrMagnitude;

                if (distSqr > maxDistanceSqr)
                    continue;

                float dist = Mathf.Sqrt(distSqr);
                float perceived = ComputePerceivedLoudness(noiseEvent, listenerPos, dist, ambient01);

                // Per-enemy profile
                NoiseHearingProfile profile = component.GetComponent<NoiseHearingProfile>();
                if (profile != null)
                {
                    perceived *= Mathf.Max(0.01f, profile.HearingSensitivity);
                    perceived *= Mathf.Max(0.01f, profile.GetCategoryMultiplier(noiseEvent.category));

                    if (perceived < profile.MinPerceivedLoudness)
                        continue;
                }
                else
                {
                    // Sensible default threshold if no profile
                    if (perceived < 0.08f)
                        continue;
                }

                // Deliver event
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
                float falloff = falloffCurve != null ? falloffCurve.Evaluate(t) : (1f - t);
                loudness *= Mathf.Clamp01(falloff);
            }

            // Occlusion
            if (occlusionMask.value != 0)
            {
                Vector3 origin = noiseEvent.worldPosition + Vector3.up * 0.5f;
                Vector3 target = listenerPos + Vector3.up * 1.2f;
                Vector3 dir = (target - origin);
                float len = dir.magnitude;

                if (len > 0.001f)
                {
                    dir /= len;
                    if (Physics.Raycast(origin, dir, len, occlusionMask, QueryTriggerInteraction.Ignore))
                    {
                        loudness *= occludedLoudnessMultiplier;
                    }
                }
            }

            loudness = Mathf.Max(0f, loudness - ambient01 * 0.6f);

            return Mathf.Clamp01(loudness);
        }
        #endregion
    }
}