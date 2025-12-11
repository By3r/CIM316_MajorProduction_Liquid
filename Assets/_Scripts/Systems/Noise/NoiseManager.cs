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
        public RoomNoisePreset roomContext;
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

        [Header("Category multipliers")]
        [SerializeField] private float footstepsMultiplier = 1f;
        [SerializeField] private float sprintMultiplier = 1.3f;
        [SerializeField] private float jumpMultiplier = 1.1f;
        [SerializeField] private float gunshotMultiplier = 2f;
        [SerializeField] private float objectImpactMultiplier = 1.5f;
        [SerializeField] private float otherMultiplier = 1f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        #endregion

        public static NoiseManager Instance { get; private set; }

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
            if (listener == null)
            {
                return;
            }

            if (!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
            }
        }

        public void UnregisterListener(INoiseListener listener)
        {
            if (listener == null)
            {
                return;
            }

            _listeners.Remove(listener);
        }
        #endregion

        #region Public Functions
        /// <summary>
        /// Main entry point: call this when something makes noise.
        /// - position: where the noise happened in world space
        /// - level: how loud it is (Low, Medium, High, Maximum)
        /// - category: type of noise.
        /// - roomContext: /OPTIONAL/ room preset to apply environment multipliers.
        /// </summary>
        public void EmitNoise(Vector3 position, NoiseLevel level, NoiseCategory category, RoomNoisePreset roomContext = null)
        {
            float baseRadius = GetBaseRadiusForLevel(level);
            baseRadius *= GetCategoryMultiplier(category);

            float finalRadius = baseRadius;

            if (roomContext != null)
            {
                finalRadius = roomContext.GetFinalRadius(baseRadius, category);
            }

            NoiseEvent noiseEvent = new NoiseEvent
            { worldPosition = position, baseRadius = baseRadius, finalRadius = finalRadius, level = level, category = category, roomContext = roomContext };

            if (showDebugLogs)
            {
                string roomName = roomContext != null ? roomContext.name : "None";
                Debug.Log($"Emitting {level} {category} at {position} " + $"baseRadius={baseRadius:0.0}, finalRadius={finalRadius:0.0}, room={roomName}");
            }

            NotifyListeners(noiseEvent);
        }
        #endregion

        #region Noise Emitters
        private float GetBaseRadiusForLevel(NoiseLevel level)
        {
            switch (level)
            {
                case NoiseLevel.Low:
                    return lowBaseRadius;
                case NoiseLevel.Medium:
                    return mediumBaseRadius;
                case NoiseLevel.High:
                    return highBaseRadius;
                case NoiseLevel.Maximum:
                    return maximumBaseRadius;
                default:
                    return lowBaseRadius;
            }
        }

        private float GetCategoryMultiplier(NoiseCategory category)
        {
            switch (category)
            {
                case NoiseCategory.Footsteps:
                    return footstepsMultiplier;
                case NoiseCategory.Sprint:
                    return sprintMultiplier;
                case NoiseCategory.Jump:
                    return jumpMultiplier;
                case NoiseCategory.Gunshot:
                    return gunshotMultiplier;
                case NoiseCategory.ObjectImpact:
                    return objectImpactMultiplier;
                case NoiseCategory.Other:
                default:
                    return otherMultiplier;
            }
        }

        private void NotifyListeners(NoiseEvent noiseEvent)
        {
            float maxDistanceSquare = noiseEvent.finalRadius * noiseEvent.finalRadius;

            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                INoiseListener listener = _listeners[i];
                if (listener == null)
                {
                    _listeners.RemoveAt(i);
                    continue;
                }

                if (listener is Component component)
                {
                    float distanceSquare = (component.transform.position - noiseEvent.worldPosition).sqrMagnitude;
                      
                    if (distanceSquare > maxDistanceSquare)
                    {
                        continue;
                    }
                }

                listener.OnNoiseHeard(noiseEvent);
            }
        }

        #endregion
    }
}