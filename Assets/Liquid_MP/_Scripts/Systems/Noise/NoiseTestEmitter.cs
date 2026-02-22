using UnityEngine;
using Liquid.Audio;

public class NoiseTestEmitter : MonoBehaviour
{
    #region Variables
    [Header("Assign the room you are standing in")]
    [SerializeField] private RoomNoisePreset roomContext;

    [Header("Noise Debug Bar")]
    [Tooltip("How long the bar takes to decay back to 0.")]
    [SerializeField] private float debugDecaySeconds = 1.25f;

    [Tooltip("Max width of the on-screen bar in pixels.")]
    [SerializeField] private float debugBarWidth = 220f;

    [Tooltip("Max height of the on-screen bar in pixels.")]
    [SerializeField] private float debugBarHeight = 18f;

    [Tooltip("Screen padding from the TOP-RIGHT corner.")]
    [SerializeField] private Vector2 debugBarOffset = new Vector2(12f, 12f);

    private float _debugNoise01;
    private float _debugNoiseTarget01;
    private float _lastNoiseTime;

    private static Texture2D _whiteTex;
    #endregion

    private void Awake()
    {
        if (_whiteTex == null)
        {
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }
    }

    private void Update()
    {
        if (NoiseManager.Instance == null)
        {
            return;
        }

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
        {
            EmitAndDebug(NoiseLevel.Low, NoiseCategory.Footsteps);
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            EmitAndDebug(NoiseLevel.Medium, NoiseCategory.Sprint);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            EmitAndDebug(NoiseLevel.High, NoiseCategory.ObjectImpact);
        }

        if (Input.GetKey(KeyCode.Mouse0))
        {
            EmitAndDebug(NoiseLevel.Maximum, NoiseCategory.Gunshot);
        }

        float elapsed = Time.time - _lastNoiseTime;
        float decayT = (debugDecaySeconds <= 0.001f) ? 1f : Mathf.Clamp01(elapsed / debugDecaySeconds);

        float decayed = Mathf.Lerp(_debugNoiseTarget01, 0f, decayT);

        _debugNoise01 = Mathf.MoveTowards(_debugNoise01, decayed, Time.deltaTime * 4f);
    }

    private void EmitAndDebug(NoiseLevel level, NoiseCategory category)
    {
        NoiseManager.Instance.EmitNoise(transform.position, level, category, roomContext);

        _debugNoiseTarget01 = NoiseLevelTo01(level);
        _lastNoiseTime = Time.time;
    }

    private float NoiseLevelTo01(NoiseLevel level)
    {
        switch (level)
        {
            case NoiseLevel.Low: return 0.25f;
            case NoiseLevel.Medium: return 0.45f;
            case NoiseLevel.High: return 0.75f;
            case NoiseLevel.Maximum: return 1.0f;
            default: return 0.3f;
        }
    }

    private void OnGUI()
    {
        float x = Screen.width - debugBarOffset.x - debugBarWidth;
        float y = debugBarOffset.y;

        Rect bg = new Rect(x, y, debugBarWidth, debugBarHeight);
        Rect fill = new Rect(bg.x, bg.y, bg.width * Mathf.Clamp01(_debugNoise01), bg.height);

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(bg, _whiteTex);

        GUI.color = new Color(0.2f, 0.9f, 0.25f, 0.9f);
        GUI.DrawTexture(fill, _whiteTex);

        GUI.color = Color.white;

        float labelWidth = 240f;
        Rect labelRect = new Rect(bg.x + bg.width - labelWidth, bg.y + bg.height + 4f, labelWidth, 18f);
        GUI.Label(labelRect, $"Noise: {_debugNoise01:0.00}");

        GUI.color = Color.white;
    }
}