using UnityEngine;

namespace Liquid.Audio
{
    // Reads inputs and bumps the player's noise level accordingly.

    [RequireComponent(typeof(NoiseEmitter))]
    public class PlayerNoiseInput : MonoBehaviour
    {
        #region Variables
        [SerializeField] private NoiseEmitter emitter;
        #endregion

        private void Awake()
        {
            if (emitter == null) emitter = GetComponent<NoiseEmitter>();
        }

        private void Update()
        {
            if (emitter == null) return;

            #region Temporary noise emitting logic.
            bool moving = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;
            bool sprinting = moving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            bool firing = Input.GetMouseButton(0);
            #endregion

            if (firing)
            {
                emitter.SetNoiseLevel(NoiseLevel.Maximum);
                return;
            }

            if (sprinting)
            {
                emitter.SetNoiseLevel(NoiseLevel.High);
                return;
            }

            if (moving)
            {
                emitter.SetNoiseLevel(NoiseLevel.Medium);
                return;
            }
        }
    }
}