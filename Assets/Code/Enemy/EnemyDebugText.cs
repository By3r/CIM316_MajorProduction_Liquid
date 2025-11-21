using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays runtime debug info for a GenericGoapEnemy in a TMP_Text.
/// Attach this to a GameObject that has a TextMeshProUGUI or TextMeshPro component.
/// </summary>
public class EnemyDebugText : MonoBehaviour
{
    #region Variables
    [Header("References")]
    [SerializeField] private GenericGoapEnemy genericEnemy;
    [SerializeField] private TMP_Text debugText;

    [Header("World Space Follow")]
    [Tooltip("If true and if the UI text is on a World Space canvas, it will follow the genericEnemy.")]
    [SerializeField] private bool followEnemy = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    #endregion

    private void Reset()
    {
        debugText = GetComponent<TMP_Text>();
        if (genericEnemy == null)
        {
            genericEnemy = FindObjectOfType<GenericGoapEnemy>();
        }
    }

    private void LateUpdate()
    {
        if (genericEnemy == null || debugText == null)
        {
            return;
        }

        if (followEnemy)
        {
            transform.position = genericEnemy.transform.position + worldOffset;
        }

        StringBuilder stringBuilder = new StringBuilder();

        #region Append string lines.
        stringBuilder.AppendLine($"Goal: {genericEnemy.CurrentGoal}");
        stringBuilder.AppendLine($"State: {genericEnemy.CurrentState}");
        stringBuilder.AppendLine($"Stamina: {genericEnemy.CurrentStamina:F1}");
        stringBuilder.AppendLine($"HasStamina: {genericEnemy.DebugHasStaminaFlag}"); 
        stringBuilder.AppendLine($"Noise: {genericEnemy.LastNoiseLevel}");
        stringBuilder.AppendLine($"Dist: {genericEnemy.DebugDistanceToPlayer:F1}");
        stringBuilder.AppendLine($"HasPath: {genericEnemy.DebugHasValidPath}");
        stringBuilder.AppendLine($"LastFailToPlayer: {genericEnemy.DebugLastPathToPlayerFailed}");
        #endregion

        debugText.text = stringBuilder.ToString();
    }
}