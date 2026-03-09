using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Tutorial
{
    /// <summary>
    /// A single line of tutorial narration.
    /// </summary>
    [Serializable]
    public sealed class NarrativeBeat
    {
        [Tooltip("Who is speaking. Narrator = no name shown. Player = loaded save name. Lieutenant = hologram.")]
        public TutorialSpeakerKind speaker;

        [TextArea(2, 6)]
        [Tooltip("The text to display.")]
        public string text;

        [Tooltip("0 = wait for player input (Space / Enter). " +
                 ">0 = auto-advance after this many seconds.")]
        [Min(0f)]
        public float autoAdvanceDelay = 0f;

        [Tooltip("Play this audio clip when this beat is shown. Leave null to skip.")]
        public AudioClip voiceClip;
    }

    [CreateAssetMenu(menuName = "Liquid/Tutorial/Narrative Sequence", fileName = "Narrative_")]
    public sealed class TutorialNarrativeAsset : ScriptableObject
    {
        [SerializeField] private List<NarrativeBeat> beats = new();

        public IReadOnlyList<NarrativeBeat> Beats => beats;
        public int Count => beats.Count;

        private void OnValidate()
        {
            beats ??= new List<NarrativeBeat>();
        }
    }
}