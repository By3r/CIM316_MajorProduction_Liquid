using UnityEngine;

namespace Liquid.Audio
{
    // Debugs the radius in which the sound can reach enemies by..
    // Attach this to the player. :)
    public class EnemyNoiseListener : MonoBehaviour, INoiseListener
    {
        #region Variables
        [Tooltip("If true this component will register itself with the NoiseManager automatically.")]
        [SerializeField] private bool autoRegisterWithManager = true;

        private NoiseEvent? _lastNoiseEvent;
        #endregion

        private void OnEnable()
        {
            if (autoRegisterWithManager && NoiseManager.Instance != null)
            {
                NoiseManager.Instance.RegisterListener(this);
            }
        }

        private void OnDisable()
        {
            if (NoiseManager.Instance != null)
            {
                NoiseManager.Instance.UnregisterListener(this);
            }
        }

        /// <summary>
        /// Called by NoiseManager when any noise event reaches this listener.
        /// </summary>
        public void OnNoiseHeard(NoiseEvent noiseEvent)
        {
            _lastNoiseEvent = noiseEvent;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_lastNoiseEvent.HasValue)
            {
                return;
            }

            NoiseEvent noiseEvent = _lastNoiseEvent.Value;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(noiseEvent.worldPosition, noiseEvent.finalRadius);
        }
#endif
    }
}