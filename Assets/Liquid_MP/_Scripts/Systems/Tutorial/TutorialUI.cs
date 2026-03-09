using TMPro;
using UnityEngine;

namespace _Scripts.Tutorial
{
    /// <summary>
    /// Drives all tutorial UI in response to TutorialPresenter events.
    /// Contains zero game logic — it only shows and hides things.
    /// </summary>
    public sealed class TutorialUI : MonoBehaviour
    {
        #region Variables

        [Header("Wiring")]
        [SerializeField] private TutorialPresenter presenter;

        [Header("Speech Panel")]
        [SerializeField] private GameObject speechPanel;
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueText;

        [Header("Lieutenant Hologram")]
        [Tooltip("Root GameObject of the Lieutenant hologram, positioned left of screen.")]
        [SerializeField] private GameObject lieutenantRoot;

        [Header("Advance Prompt")]
        [Tooltip("'Press Space to continue' hint.")]
        [SerializeField] private GameObject advancePrompt;

        private bool _hologramPinnedActive;
        #endregion

        private void Awake()
        {
            if (presenter == null)
                presenter = GetComponent<TutorialPresenter>();

            presenter.OnBeatPresented += HandleBeatPresented;
            presenter.OnSequenceEnded += HandleSequenceEnded;

            SetSpeechVisible(false);
            SetLieutenantVisible(false);
        }

        private void Update()
        {
            if (presenter == null || !presenter.IsRunning) return;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                presenter.Advance();
        }

        private void OnDestroy()
        {
            if (presenter != null)
            {
                presenter.OnBeatPresented -= HandleBeatPresented;
                presenter.OnSequenceEnded -= HandleSequenceEnded;
            }
        }


        #region Handlers.
        private void HandleBeatPresented(string speakerName, NarrativeBeat beat)
        {
            SetSpeechVisible(true);

            bool showName = beat.speaker != TutorialSpeakerKind.Narrator;
            if (speakerNameText != null)
            {
                speakerNameText.gameObject.SetActive(showName);
                speakerNameText.text = showName ? speakerName : string.Empty;
            }

            if (dialogueText != null)
                dialogueText.text = beat.text;

            bool showHologram = beat.speaker == TutorialSpeakerKind.Lieutenant || beat.keepHologramActive;
            _hologramPinnedActive = beat.keepHologramActive;
            SetLieutenantVisible(showHologram);

            if (advancePrompt != null)
                advancePrompt.SetActive(beat.autoAdvanceDelay <= 0f);
        }

        private void HandleSequenceEnded()
        {
            SetSpeechVisible(false);

            if (!_hologramPinnedActive)
                SetLieutenantVisible(false);

            _hologramPinnedActive = false;
        }

        #endregion

        #region Helpers

        private void SetSpeechVisible(bool show)
        {
            if (speechPanel != null) speechPanel.SetActive(show);
        }

        private void SetLieutenantVisible(bool show)
        {
            if (lieutenantRoot != null) lieutenantRoot.SetActive(show);
        }

        public void SetLieutenantVisiblePublic(bool show)
        {
            _hologramPinnedActive = show;
            SetLieutenantVisible(show);
        }

        public void SetAdvancePromptVisible(bool show)
        {
            if (advancePrompt != null) advancePrompt.SetActive(show);
        }

        #endregion
    }
}