using TMPro;
using UnityEngine;

public class EnemyDebugHUD : MonoBehaviour
{
    #region Variables
    [Header("UI")]
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private bool showHelpLine = true;
    #endregion

    private void Reset()
    {
        debugText = GetComponentInChildren<TMP_Text>();
    }

    private void Update()
    {
        if (debugText == null)
        {
            return;
        }

        EnemyDebugFocusManager manager = EnemyDebugFocusManager.Instance;
        if (manager == null || manager.FocusedTarget == null)
        {
            debugText.text = showHelpLine ? "Enemy Debug: (no focused enemy)\n\n Press TAB to cycle. Click an enemy to focus." : "";
            return;
        }

        string header = "";
        if (showHelpLine)
        {
            header = "Enemy Debug (focused only)\nTAB = cycle | Click = focus\n\n";
        }

        debugText.text = header + manager.FocusedTarget.GetDebugText();
    }
}