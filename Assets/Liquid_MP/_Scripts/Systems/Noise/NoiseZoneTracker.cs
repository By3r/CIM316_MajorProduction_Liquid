using System.Collections.Generic;
using UnityEngine;

namespace Liquid.Audio
{
    [DisallowMultipleComponent]
    public sealed class NoiseZoneTracker : MonoBehaviour
    {
        private readonly List<NoiseZone> _zones = new List<NoiseZone>(8);

        private void OnTriggerEnter(Collider other)
        {
            var zone = other.GetComponent<NoiseZone>();
            if (zone != null && !_zones.Contains(zone))
                _zones.Add(zone);
        }

        private void OnTriggerExit(Collider other)
        {
            var zone = other.GetComponent<NoiseZone>();
            if (zone != null)
                _zones.Remove(zone);
        }

        public void Apply(NoiseCategory category, ref float radius, ref float intensity01, ref float ambient01)
        {
            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                var z = _zones[i];
                if (z == null)
                {
                    _zones.RemoveAt(i);
                    continue;
                }

                if (!z.Affects(category))
                    continue;

                radius = z.ApplyRadius(radius);
                intensity01 = z.ApplyIntensity(intensity01);
                ambient01 = Mathf.Clamp01(ambient01 + z.AmbientAdd01);
            }
        }
    }
}