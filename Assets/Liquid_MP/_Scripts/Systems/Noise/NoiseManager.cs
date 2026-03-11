using System.Collections.Generic;
using UnityEngine;

namespace Liquid.Audio
{
    public struct NoiseEvent
    {
        public Vector3 WorldPosition;
        public float BaseNoise;
        public float FinalNoise;
        public NoiseLevel Level;
        public NoiseCategory Category;
        public EnvironmentNoiseProfile EnvironmentProfile;
        public RoomNoisePreset Room;

        public float FinalRadius;

        public float Intensity01;

        public Vector3 worldPosition { get => WorldPosition; set => WorldPosition = value; }
        public float finalNoise { get => FinalNoise; set => FinalNoise = value; }
        public NoiseLevel level { get => Level; set => Level = value; }
        public NoiseCategory category { get => Category; set => Category = value; }
        public EnvironmentNoiseProfile environmentProfile { get => EnvironmentProfile; set => EnvironmentProfile = value; }
        public float finalRadius { get => FinalRadius; set => FinalRadius = value; }
        public float intensity01 { get => Intensity01; set => Intensity01 = value; }

        public float perceivedLoudness01 { get => Intensity01; set => Intensity01 = value; }
    }

    public interface INoiseListener
    {
        void OnNoiseHeard(NoiseEvent noiseEvent);
    }

    [DisallowMultipleComponent]
    public class NoiseManager : MonoBehaviour
    {
        public static NoiseManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        #region Variables
        [Header("Base Noise Values Per Action")]
        [SerializeField] private float _footstepBase = 1.0f;
        [SerializeField] private float _sprintBase = 2.0f;
        [SerializeField] private float _jumpBase = 1.5f;
        [SerializeField] private float _gunshotBase = 4.0f;
        [SerializeField] private float _objectImpactBase = 2.5f;
        [SerializeField] private float _commDeviceBase = 2.0f;
        [SerializeField] private float _otherBase = 1.0f;

        [Header("Detection Radius Per Level")]
        [SerializeField] private float _noneRadius = 2f;
        [SerializeField] private float _lowRadius = 4f;
        [SerializeField] private float _mediumRadius = 8f;
        [SerializeField] private float _highRadius = 14f;
        [SerializeField] private float _extremeRadius = 22f;

        [Header("Occlusion")]
        [SerializeField] private LayerMask _occlusionMask;
        [Range(0f, 1f)]
        [SerializeField] private float _occludedMultiplier = 0.35f;

        [Header("UI Decay")]
        [Tooltip("How fast LastFinalNoise decays toward 0 when no noise is emitted.")]
        [SerializeField] private float _noiseDecaySpeed = 3f;

        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = false;

        public float LastFinalNoise { get; private set; }
        public RoomNoisePreset LastRoom { get; private set; }
        public EnvironmentNoiseProfile LastProfile { get; private set; }

        private readonly List<INoiseListener> _listeners = new List<INoiseListener>(32);
        private readonly List<RoomNoisePreset> _rooms = new List<RoomNoisePreset>(64);
        #endregion

        private void Start() => RefreshRooms();

        private void Update()
        {
            LastFinalNoise = Mathf.MoveTowards(LastFinalNoise, 0f, Time.deltaTime * _noiseDecaySpeed);
        }


        #region Room Management.
        public void RefreshRooms()
        {
            _rooms.Clear();
            _rooms.AddRange(FindObjectsByType<RoomNoisePreset>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (_showDebugLogs)
                Debug.Log($"[NoiseManager] RefreshRooms: found {_rooms.Count} rooms.");
        }

        private RoomNoisePreset FindRoomAt(Vector3 position)
        {
            for (int i = 0; i < _rooms.Count; i++)
                if (_rooms[i] != null && _rooms[i].ContainsPoint(position))
                    return _rooms[i];
            return null;
        }

        #endregion

        #region Listener Registration.
        public void RegisterListener(INoiseListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void UnregisterListener(INoiseListener listener)
        {
            if (listener != null)
                _listeners.Remove(listener);
        }

        #endregion

        #region Public Functions.
        /// <summary>Emits noise using the category's configured base value.</summary>
        public void EmitNoise(Vector3 position, NoiseCategory category)
        {
            EmitNoiseInternal(position, GetBaseNoise(category), category);
        }

        /// <summary>Emits noise with a custom base value.</summary>
        public void EmitNoise(Vector3 position, float customBaseNoise, NoiseCategory category)
        {
            EmitNoiseInternal(position, customBaseNoise, category);
        }
        #endregion

        #region Internals.
        private void EmitNoiseInternal(Vector3 position, float baseNoise, NoiseCategory category)
        {
            RoomNoisePreset room = FindRoomAt(position);

            float finalNoise = room != null ? room.ApplyMultiplier(baseNoise, category) : baseNoise;

            NoiseLevel level = LevelFromFloat(finalNoise);
            float radius = GetRadius(level) * (room != null ? room.RoomRadiusMultiplier : 1f);
            float intensity01 = Mathf.Clamp01(finalNoise / 4f);

            LastFinalNoise = Mathf.Max(LastFinalNoise, finalNoise);
            LastRoom = room;
            LastProfile = room?.ActiveProfile;

            NoiseEvent ev = new NoiseEvent
            {
                WorldPosition = position,
                BaseNoise = baseNoise,
                FinalNoise = finalNoise,
                Level = level,
                Category = category,
                EnvironmentProfile = room?.ActiveProfile,
                Room = room,
                FinalRadius = radius,
                Intensity01 = intensity01,
            };

            if (_showDebugLogs)
                Debug.Log($"[NoiseManager] {category} | base={baseNoise:0.00} final={finalNoise:0.00} " +
                          $"level={level} radius={radius:0.0} room={room?.name ?? "None"}");

            NotifyListeners(ev, radius);
        }

        private void NotifyListeners(NoiseEvent ev, float radius)
        {
            float radiusSqr = radius * radius;

            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                INoiseListener listener = _listeners[i];
                if (listener == null) { _listeners.RemoveAt(i); continue; }
                if (!(listener is Component c) || c == null) continue;

                if ((c.transform.position - ev.WorldPosition).sqrMagnitude > radiusSqr) continue;

                if (_occlusionMask.value != 0)
                {
                    Vector3 origin = ev.WorldPosition + Vector3.up * 0.5f;
                    Vector3 target = c.transform.position + Vector3.up * 1.2f;
                    Vector3 dir = target - origin;
                    float len = dir.magnitude;
                    if (len > 0.001f && Physics.Raycast(origin, dir / len, len, _occlusionMask, QueryTriggerInteraction.Ignore))
                    {
                        NoiseEvent occ = ev;
                        occ.FinalNoise *= _occludedMultiplier;
                        occ.Intensity01 = Mathf.Clamp01(occ.FinalNoise / 4f);
                        occ.Level = LevelFromFloat(occ.FinalNoise);
                        listener.OnNoiseHeard(occ);
                        continue;
                    }
                }

                listener.OnNoiseHeard(ev);
            }
        }

        /// <summary>
        /// Converts a final noise float to a NoiseLevel.
        /// </summary>
        public static NoiseLevel LevelFromFloat(float value)
        {
            if (value <= 0f) return NoiseLevel.None;
            if (value <= 1.0f) return NoiseLevel.Low;
            if (value <= 2.0f) return NoiseLevel.Medium;
            if (value <= 3.0f) return NoiseLevel.High;
            return NoiseLevel.Extreme;
        }

        private float GetBaseNoise(NoiseCategory category)
        {
            switch (category)
            {
                case NoiseCategory.Footsteps: return _footstepBase;
                case NoiseCategory.Sprint: return _sprintBase;
                case NoiseCategory.Jump: return _jumpBase;
                case NoiseCategory.Gunshot: return _gunshotBase;
                case NoiseCategory.ObjectImpact: return _objectImpactBase;
                case NoiseCategory.CommDevice: return _commDeviceBase;
                default: return _otherBase;
            }
        }

        private float GetRadius(NoiseLevel level)
        {
            switch (level)
            {
                case NoiseLevel.None: return _noneRadius;
                case NoiseLevel.Low: return _lowRadius;
                case NoiseLevel.Medium: return _mediumRadius;
                case NoiseLevel.High: return _highRadius;
                case NoiseLevel.Extreme: return _extremeRadius;
                default: return _noneRadius;
            }
        }
        #endregion
    }
}