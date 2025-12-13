using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Automatically turns child Images into radial wedges that form a circle.
    /// </summary>

    [ExecuteAlways]
    public class RadialSegmentArranger : MonoBehaviour
    {
        #region Variables
        [Header("Segment Images")]
        [Tooltip("One Image per slice, in circular order.")]
        [SerializeField] private List<Image> segmentImages = new List<Image>();

        [Header("Layout")]
        [Tooltip("Extra rotation applied to the whole wheel in degrees.")]
        [SerializeField] private float globalRotationOffset = 0f;

        [Tooltip("Gap between segments in degrees.")]
        [SerializeField] private float gapAngle = 2f;

        [Tooltip("If set to true, the segments will go clockwise, otherwise counter-clockwise.")]
        [SerializeField] private bool clockwise = true;
        #endregion

        private void OnEnable()
        {
            ArrangeSegments();
        }

        private void OnValidate()
        {
            ArrangeSegments();
        }

        private void ArrangeSegments()
        {
            if (segmentImages == null || segmentImages.Count == 0)
            {
                return;
            }

            int count = segmentImages.Count;
            if (count <= 0)
            {
                return;
            }

            float sliceAngle = 360f / count;

            float visibleAngle = Mathf.Max(0f, sliceAngle - gapAngle);
            float fillAmount = visibleAngle / 360f;

            for (int i = 0; i < count; i++)
            {
                Image img = segmentImages[i];
                if (img == null)
                {
                    continue;
                }

                RectTransform rectTransform = img.rectTransform;

                #region Forces proper image setup
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Bottom;
                img.fillClockwise = clockwise;
                img.fillAmount = fillAmount;
                #endregion

                #region Centers Rect on the Parent.
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = Vector2.zero;
                #endregion

                float baseAngle = sliceAngle * i;
                float halfGap = gapAngle * 0.5f;

                float rotationAngle = baseAngle + globalRotationOffset;

                if (clockwise)
                {
                    rotationAngle = -rotationAngle;
                }

                rotationAngle += clockwise ? halfGap : -halfGap;

                rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotationAngle);
            }
        }
    }
}