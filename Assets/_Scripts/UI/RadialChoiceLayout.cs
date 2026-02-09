using System.Collections.Generic;
using UnityEngine;

namespace Liquid.Dialogue.UI
{
    public sealed class RadialChoiceLayout : MonoBehaviour
    {
        #region Variables
        [Header("Layout")]
        [SerializeField] private float radius = 220f;
        [SerializeField] private bool useFixedCrossPositions = true;
        [SerializeField] private float rotationDegrees = 0f;

        private static readonly Vector2 Up = new Vector2(0f, 1f);
        private static readonly Vector2 Right = new Vector2(1f, 0f);
        private static readonly Vector2 Down = new Vector2(0f, -1f);
        private static readonly Vector2 Left = new Vector2(-1f, 0f);
        #endregion

        public void ApplyLayout(IReadOnlyList<RectTransform> buttons)
        {
            if (buttons == null || buttons.Count == 0)
                return;

            int count = Mathf.Min(buttons.Count, 4);
            var dirs = GetDirections(count);

            Quaternion rot = Quaternion.Euler(0f, 0f, rotationDegrees);

            for (int i = 0; i < count; i++)
            {
                RectTransform rt = buttons[i];
                if (rt == null) continue;

                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                Vector2 dir = dirs[i];
                dir = rot * dir;

                rt.anchoredPosition = dir * radius;
            }
        }

        private Vector2[] GetDirections(int count)
        {
            if (!useFixedCrossPositions)
                return GetEvenlySpacedDirections(count);

            return count switch
            {
                2 => new[] { Left, Right },
                3 => new[] { Left, Right, Down },
                _ => new[] { Left, Right, Up, Down }
            };
        }

        private Vector2[] GetEvenlySpacedDirections(int count)
        {
            var dirs = new Vector2[count];
            float startAngle = 90f;
            float step = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angle = (startAngle - step * i) * Mathf.Deg2Rad;
                dirs[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
            }

            return dirs;
        }

        private void OnValidate()
        {
            radius = Mathf.Max(10f, radius);
        }
    }
}
