using UnityEngine;

namespace Liquid.Audio
{
    // Debugs the radius in which the sound can reach enemies by..
    // Attach this to the player. :)
    public class EnemyNoiseListener : MonoBehaviour
    {
        #region Variables
        [SerializeField] private NoiseEmitter playerNoiseSource;

        [Tooltip("At intensity 1.0, this is the hearing radius in meters")]
        [SerializeField] private float maxHearingDistance = 18f;
        #endregion

        private void OnEnable()
        {
            if (playerNoiseSource != null)
            {
                playerNoiseSource.NoiseChanged += OnPlayerNoiseChanged;
            }
        }

        private void OnDisable()
        {
            if (playerNoiseSource != null)
            {
                playerNoiseSource.NoiseChanged -= OnPlayerNoiseChanged;
            }
        }

        private void OnPlayerNoiseChanged(NoiseLevel level, float intensity)
        {
            float radius = intensity * maxHearingDistance;
            Debug.Log($"Player noise {level} → estimated hearing radius {radius:0.0} m", this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (playerNoiseSource == null) return;

            float radius = Mathf.Clamp01(playerNoiseSource.CurrentIntensity) * maxHearingDistance;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}