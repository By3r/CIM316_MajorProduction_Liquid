using UnityEngine;

namespace Liquid.Audio
{
    // Simple on-screen bar debug to visualize intensity and level, this is meant to demonstrate the prototype's idea.
    // This class is temporary and will be removed in the future.
    public class NoiseDebugHUD : MonoBehaviour
    {
        #region Variables
        [SerializeField] private NoiseEmitter source;
        [SerializeField] private Vector2 screenOffset = new Vector2(20, 20);
        [SerializeField] private Vector2 barSize = new Vector2(260, 20);

        [Header("Colours")]
        [SerializeField] private Color lowColor = new Color(0.2f, 0.55f, 1f, 1f);   
        [SerializeField] private Color mediumColor = new Color(1f, 0.9f, 0.1f, 1f); 
        [SerializeField] private Color highColor = new Color(1f, 0.25f, 0.2f, 1f);  
        [SerializeField] private Color backgroundColor = new Color(1f, 1f, 1f, 0.15f);
        #endregion

        private void OnGUI()
        {
            if (source == null) return;

            float intensity = Mathf.Clamp01(source.CurrentIntensity);
            string label = $"Noise: {source.CurrentLevel}  ({intensity:0.00})";

            Rect labelRect = new Rect(screenOffset.x, screenOffset.y, 400, 22);
            GUI.Label(labelRect, label);

            Rect backRect = new Rect(screenOffset.x, screenOffset.y + 22, barSize.x, barSize.y);

            // Background bar
            Color prev = GUI.color;
            GUI.color = backgroundColor;
            GUI.Box(backRect, GUIContent.none);
            GUI.color = prev;

            // Coloured fill only when level is not None
            NoiseLevel level = source.CurrentLevel;
            if (level != NoiseLevel.Low)
            {
                Color fillColor = prev;
                switch (level)
                {
                    case NoiseLevel.Medium: fillColor = lowColor; break;
                    case NoiseLevel.High: fillColor = mediumColor; break;
                    case NoiseLevel.Maximum: fillColor = highColor; break;
                }

                Rect fillRect = new Rect(backRect.x, backRect.y, barSize.x * intensity, backRect.height);
                GUI.color = fillColor;
                GUI.DrawTexture(fillRect, Texture2D.whiteTexture); // Apply fill properties
                GUI.color = prev;
            }
        }
    }
}