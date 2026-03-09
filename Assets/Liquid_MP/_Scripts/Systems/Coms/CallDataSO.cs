using UnityEngine;

namespace _Scripts.Systems.Coms
{
    /// <summary>
    /// A single line of dialogue during a COMS call.
    /// Voice clip drives the timing — if no clip is provided, fallbackDuration is used.
    /// </summary>
    [System.Serializable]
    public struct DialogueLine
    {
        [Tooltip("Who is speaking this line (e.g., 'The Lieutenant').")]
        public string speakerName;

        [TextArea(1, 4)]
        [Tooltip("Subtitle text displayed on the COMS device screen.")]
        public string text;

        [Tooltip("Voice audio for this line. Duration determines how long the line stays on screen.")]
        public AudioClip voiceClip;

        [Tooltip("How long to show this line if no voice clip is assigned (seconds).")]
        public float fallbackDuration;

        /// <summary>
        /// Effective duration: voice clip length if present, otherwise fallbackDuration.
        /// </summary>
        public float Duration => voiceClip != null ? voiceClip.length : fallbackDuration;
    }

    /// <summary>
    /// ScriptableObject defining a single incoming COMS call.
    /// Contains caller identity, ring settings, and dialogue lines.
    /// Any system can trigger a call via ComsCallManager.Instance.TriggerCall(callData).
    /// </summary>
    [CreateAssetMenu(menuName = "Liquid/COMS/Call Data", fileName = "NewCallData")]
    public class CallDataSO : ScriptableObject
    {
        [Header("Caller")]
        [Tooltip("Display name of the caller (e.g., 'The Lieutenant').")]
        public string callerName;

        [Tooltip("3D hologram prefab (upper body bust with hologram shader). " +
                 "Spawned above the device when the call is answered.")]
        public GameObject hologramPrefab;

        [Header("Ring")]
        [Tooltip("Ringtone clip. If null, ComsDeviceController's default ringtone is used.")]
        public AudioClip ringtone;

        [Tooltip("Seconds of ringing before the device starts emitting noise that attracts enemies.")]
        public float gracePeriod = 5f;

        [Header("Dialogue")]
        [Tooltip("Dialogue lines played in order after the call is answered. " +
                 "Each line auto-advances by its voice clip duration.")]
        public DialogueLine[] lines;
    }
}
