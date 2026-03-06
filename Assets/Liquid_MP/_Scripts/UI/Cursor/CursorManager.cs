using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager CursorInstance { get; private set; }

    #region Variables
    [Header("Cursor Textures")]
    [SerializeField] private Texture2D defaultCursorTexture;

    [Tooltip("Pixel position inside the texture that acts as the click point.")]
    [SerializeField] private Vector2 cursorHotspot = Vector2.zero;

    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

    [Header("Cursor Size")]
    [Range(0.5f, 4f)]
    [SerializeField] private float cursorScale = 1f;
    #endregion

    private Texture2D scaledCursor;

    private void Awake()
    {
        if (CursorInstance != null && CursorInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        CursorInstance = this;

        SetDefaultCursor();
    }

    #region Public Functions

    public void HideCursor()
    {
        Cursor.visible = false;
    }

    public void ShowCursor()
    {
        Cursor.visible = true;
    }

    public void ToggleCursor()
    {
        Cursor.visible = !Cursor.visible;
    }

    public void SetDefaultCursor()
    {
        if (defaultCursorTexture == null)
        {
            Debug.LogWarning("Default cursor texture not assigned.");
            return;
        }

        SetCursor(defaultCursorTexture, cursorHotspot);
    }

    public void SetCursor(Texture2D texture, Vector2 hotspot)
    {
        if (texture == null)
        {
            Debug.LogWarning("Tried to set a null cursor texture.");
            return;
        }

        Texture2D finalTexture = texture;

        if (Mathf.Abs(cursorScale - 1f) > 0.01f)
        {
            finalTexture = ScaleTexture(texture, cursorScale);
        }

        Cursor.SetCursor(finalTexture, hotspot * cursorScale, cursorMode);
    }

    public void SetCursorScale(float scale)
    {
        cursorScale = scale;
        SetDefaultCursor();
    }

    #endregion


    #region Texture Scaling

    private Texture2D ScaleTexture(Texture2D source, float scale)
    {
        int newWidth = Mathf.RoundToInt(source.width * scale);
        int newHeight = Mathf.RoundToInt(source.height * scale);

        Texture2D scaled = new Texture2D(newWidth, newHeight, source.format, false);

        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                float u = (float)x / newWidth;
                float v = (float)y / newHeight;

                scaled.SetPixel(x, y, source.GetPixelBilinear(u, v));
            }
        }

        scaled.Apply();
        return scaled;
    }
    #endregion
}