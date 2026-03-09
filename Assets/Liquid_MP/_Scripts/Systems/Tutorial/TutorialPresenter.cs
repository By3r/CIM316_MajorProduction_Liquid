using System;
using System.Collections;
using _Scripts.Core.Persistence;
using UnityEngine;

namespace _Scripts.Tutorial
{
    /// <summary>
    /// Drives a TutorialNarrativeAsset beat by beat.
    /// </summary>
    public sealed class TutorialPresenter : MonoBehaviour
    {
        #region Events.
        public event Action<string, string, TutorialSpeakerKind> OnBeatPresented;

        /// <summary>Fired when the last beat has been advanced past.</summary>
        public event Action OnSequenceEnded;

        #endregion

        #region Variables

        private const string FallbackPlayerName = "Agent";
        private const string LieutenantName = "Lt. Northbridge";

        private TutorialNarrativeAsset _asset;
        private int _beatIndex;
        private bool _isRunning;
        private string _resolvedPlayerName = FallbackPlayerName;

        private Coroutine _autoAdvanceRoutine;

        public bool IsRunning => _isRunning;
        public int CurrentBeatIndex => _beatIndex;

        #endregion

        #region Public Functions.
        /// <summary>
        /// Starts playing the given narrative asset.
        /// Reads the player name from saveSlotIndex — pass -1 to use the fallback "Agent".
        /// </summary>
        public void Begin(TutorialNarrativeAsset asset, int saveSlotIndex = -1)
        {
            if (asset == null || asset.Count == 0)
            {
                Debug.LogWarning("[TutorialPresenter] Begin called with null or empty asset.");
                return;
            }

            StopAutoAdvance();

            _asset = asset;
            _beatIndex = 0;
            _isRunning = true;
            _resolvedPlayerName = ResolvePlayerName(saveSlotIndex);

            PresentCurrentBeat();
        }

        /// <summary>
        /// Advances to the next beat. Call this on player input (Space / Enter).
        /// Does nothing if the current beat is auto-advancing or the sequence has ended.
        /// </summary>
        public void Advance()
        {
            if (!_isRunning) return;

            // If an auto-advance is pending, cancel it and advance immediately.
            if (_autoAdvanceRoutine != null)
            {
                StopAutoAdvance();
            }

            MoveToNextBeat();
        }

        /// <summary>
        /// Skips to the end of the sequence immediately and fires OnSequenceEnded.
        /// Used by the debug L key shortcut in TutorialManager.
        /// </summary>
        public void ForceComplete()
        {
            if (!_isRunning) return;

            StopAutoAdvance();
            EndSequence();
        }

        #endregion

        #region Internals.
        private void PresentCurrentBeat()
        {
            if (_beatIndex >= _asset.Count)
            {
                EndSequence();
                return;
            }

            NarrativeBeat beat = _asset.Beats[_beatIndex];
            string speakerName = ResolveSpeakerName(beat.speaker);

            OnBeatPresented?.Invoke(speakerName, beat.text, beat.speaker);

            // Auto-advance if delay is set.
            if (beat.autoAdvanceDelay > 0f)
                _autoAdvanceRoutine = StartCoroutine(AutoAdvanceAfter(beat.autoAdvanceDelay));
        }

        private void MoveToNextBeat()
        {
            _beatIndex++;

            if (_beatIndex >= _asset.Count)
            {
                EndSequence();
                return;
            }

            PresentCurrentBeat();
        }

        private void EndSequence()
        {
            _isRunning = false;
            _asset = null;
            OnSequenceEnded?.Invoke();
        }

        private IEnumerator AutoAdvanceAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _autoAdvanceRoutine = null;
            MoveToNextBeat();
        }

        private void StopAutoAdvance()
        {
            if (_autoAdvanceRoutine != null)
            {
                StopCoroutine(_autoAdvanceRoutine);
                _autoAdvanceRoutine = null;
            }
        }

        private string ResolveSpeakerName(TutorialSpeakerKind kind)
        {
            return kind switch
            {
                TutorialSpeakerKind.Narrator => string.Empty,
                TutorialSpeakerKind.Player => _resolvedPlayerName,
                TutorialSpeakerKind.Lieutenant => LieutenantName,
                _ => string.Empty,
            };
        }

        private static string ResolvePlayerName(int slotIndex)
        {
            if (slotIndex < 0) return FallbackPlayerName;

            GameSaveData save = SaveSystem.LoadGame(slotIndex);
            if (save == null || string.IsNullOrWhiteSpace(save.PlayerName))
                return FallbackPlayerName;

            return save.PlayerName;
        }
        #endregion
    }
}