/*using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    /// <summary>
    /// Configures a Canvas with responsive scaling and graphic raycasting settings.
    /// Ensures UI elements scale appropriately across different screen sizes.
    /// Currently commented out as it may not be in active use.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class UnscaledTimeCanvas : MonoBehaviour
    {
        #region Initialization

        private void Awake()
        {
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void OnEnable()
        {
            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.ignoreReversedGraphics = true;
            }
        }

        #endregion
    }
}*/

// ! I was using this, and I think its logic might be useful later on, but we don't need this for now.