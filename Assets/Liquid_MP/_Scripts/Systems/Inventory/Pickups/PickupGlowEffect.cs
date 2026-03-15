using UnityEngine;

namespace _Scripts.Systems.Inventory.Pickups
{
    /// <summary>
    /// Adds two subtle visual cues to pickupable items:
    /// 1. A soft ambient emission overlay (shader pass) that pulses gently.
    /// 2. Faint floating dust particles around the object.
    ///
    /// The original material is never modified. The emission is an additive
    /// second material pass. Particles are spawned as a child object.
    ///
    /// Added automatically by <see cref="Pickup"/> when <c>_showGlow</c> is enabled.
    /// </summary>
    public sealed class PickupGlowEffect : MonoBehaviour
    {
        private static GameObject _dustPrefab;

        private static Material _sharedGlowMaterial;

        private Renderer[] _renderers;
        private int[][] _originalMaterialCounts;
        private GameObject _dustInstance;

        private void Awake()
        {
            if (_sharedGlowMaterial == null)
            {
                Shader glowShader = Shader.Find("Liquid/PickupGlow");
                if (glowShader == null)
                {
                    Debug.LogWarning("[PickupGlowEffect] Shader 'Liquid/PickupGlow' not found.");
                    enabled = false;
                    return;
                }

                _sharedGlowMaterial = new Material(glowShader);
                _sharedGlowMaterial.name = "PickupGlow (Shared)";
            }

            ApplyGlow();
            SpawnDustParticles();
        }

        private void OnDestroy()
        {
            RemoveGlow();

            if (_dustInstance != null)
                Destroy(_dustInstance);
        }

        #region Ambient Emission Overlay

        private void ApplyGlow()
        {
            _renderers = GetComponentsInChildren<Renderer>();

            if (_renderers == null || _renderers.Length == 0) return;

            _originalMaterialCounts = new int[_renderers.Length][];

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer rend = _renderers[i];

                if (rend is ParticleSystemRenderer || rend is TrailRenderer) continue;

                Material[] currentMats = rend.sharedMaterials;
                _originalMaterialCounts[i] = new int[] { currentMats.Length };

                Material[] newMats = new Material[currentMats.Length + 1];
                for (int m = 0; m < currentMats.Length; m++)
                    newMats[m] = currentMats[m];

                newMats[currentMats.Length] = _sharedGlowMaterial;
                rend.sharedMaterials = newMats;
            }
        }

        private void RemoveGlow()
        {
            if (_renderers == null || _originalMaterialCounts == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                if (_originalMaterialCounts[i] == null) continue;

                int originalCount = _originalMaterialCounts[i][0];
                Material[] currentMats = _renderers[i].sharedMaterials;

                if (currentMats.Length <= originalCount) continue;

                Material[] restoredMats = new Material[originalCount];
                for (int m = 0; m < originalCount; m++)
                    restoredMats[m] = currentMats[m];

                _renderers[i].sharedMaterials = restoredMats;
            }
        }

        #endregion

        #region Dust Particles

        private void SpawnDustParticles()
        {
            if (_dustPrefab == null)
                _dustPrefab = Resources.Load<GameObject>("P_PickupDust");

            if (_dustPrefab == null) return;

            Bounds bounds = CalculateBounds();

            _dustInstance = Instantiate(_dustPrefab, transform);
            _dustInstance.transform.localPosition = bounds.center - transform.position;

            // Scale the shape to match the object bounds
            var ps = _dustInstance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var shape = ps.shape;
                shape.scale = bounds.size * 1.2f;
            }
        }

        private Bounds CalculateBounds()
        {
            Bounds bounds = new Bounds(transform.position, Vector3.one * 0.2f);
            bool initialized = false;

            foreach (var rend in GetComponentsInChildren<Renderer>())
            {
                if (rend is ParticleSystemRenderer) continue;

                if (!initialized)
                {
                    bounds = rend.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(rend.bounds);
                }
            }

            return bounds;
        }

        #endregion
    }
}
