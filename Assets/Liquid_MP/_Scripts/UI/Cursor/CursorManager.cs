using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager CursorInstance { get; private set; }

    #region Variables
    [Header("Cursor Textures")]
    [SerializeField] private Texture2D defaultCursorTexture;
    [SerializeField] private Texture2D highlightCursorTexture;
    [SerializeField] private Texture2D clickCursorTexture;

    [Tooltip("Pixel position inside the default texture that acts as the click point.")]
    [SerializeField] private Vector2 defaultCursorHotspot = Vector2.zero;

    [Tooltip("Pixel position inside the highlight texture that acts as the click point.")]
    [SerializeField] private Vector2 highlightCursorHotspot = Vector2.zero;

    [Tooltip("Pixel position inside the click texture that acts as the click point.")]
    [SerializeField] private Vector2 clickCursorHotspot = Vector2.zero;

    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;
    #endregion

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

        Cursor.SetCursor(defaultCursorTexture, defaultCursorHotspot, cursorMode);
    }

    public void SetHighlightCursor()
    {
        if (highlightCursorTexture == null)
        {
            Debug.LogWarning("Highlight cursor texture not assigned.");
            return;
        }

        Cursor.SetCursor(highlightCursorTexture, highlightCursorHotspot, cursorMode);
    }

    public void SetClickCursor()
    {
        if (clickCursorTexture == null)
        {
            Debug.LogWarning("Click cursor texture not assigned.");
            return;
        }

        Cursor.SetCursor(clickCursorTexture, clickCursorHotspot, cursorMode);

        Invoke("SetDefaultCursor", 0.15f);
    }

    public void SetCursor(Texture2D texture, Vector2 hotspot)
    {
        if (texture == null)
        {
            Debug.LogWarning("Tried to set a null cursor texture.");
            return;
        }

        Cursor.SetCursor(texture, hotspot, cursorMode);
    }
    #endregion
}