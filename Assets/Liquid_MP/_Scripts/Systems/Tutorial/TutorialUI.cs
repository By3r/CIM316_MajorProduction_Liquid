using TMPro;
using UnityEngine;

namespace _Scripts.Tutorial
{
    /// <summary>
    /// Drives all tutorial UI in response to TutorialPresenter events.
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
        [Tooltip("Root GameObject of the Lieutenant hologram. Enabled only during Lieutenant beats.")]
        [SerializeField] private GameObject lieutenantRoot;

        [Header("Advance Prompt")]
        [Tooltip("'Press Space to continue' hint. Hidden when a beat auto-advances.")]
        [SerializeField] private GameObject advancePrompt;

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

        #region Handlers

        private void HandleBeatPresented(string speakerName, string text, TutorialSpeakerKind kind)
        {
            SetSpeechVisible(true);

            bool showName = kind != TutorialSpeakerKind.Narrator;
            if (speakerNameText != null)
            {
                speakerNameText.gameObject.SetActive(showName);
                speakerNameText.text = showName ? speakerName : string.Empty;
            }

            if (dialogueText != null)
                dialogueText.text = text;

            SetLieutenantVisible(kind == TutorialSpeakerKind.Lieutenant);

            bool isAutoAdvance = false;
            if (presenter != null)
            {
                int idx = presenter.CurrentBeatIndex;
                isAutoAdvance = false; 
            }

            if (advancePrompt != null)
                advancePrompt.SetActive(showName || kind == TutorialSpeakerKind.Narrator);
        }

        private void HandleSequenceEnded()
        {
            SetSpeechVisible(false);
            SetLieutenantVisible(false);
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

        public void SetAdvancePromptVisible(bool show)
        {
            if (advancePrompt != null) advancePrompt.SetActive(show);
        }

        #endregion
    }
}