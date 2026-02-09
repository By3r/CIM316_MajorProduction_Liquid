using Liquid.Dialogue.UI;
using Liquid.NPC;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Liquid.Dialogue
{
    public sealed class DialogueTestUI : MonoBehaviour
    {
        #region Variables
        [Header("Wiring")]
        [SerializeField] private DialogueRunner runner;
        [SerializeField] private DialogueContextBehaviour context;

        [Header("Speech UI")]
        [SerializeField] private GameObject speechPanel;
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private TextMeshProUGUI lineText;

        [Header("Choices UI")]
        [SerializeField] private GameObject choicesPanel;
        [SerializeField] private Transform choicesContainer;
        [SerializeField] private Button choiceButtonPrefab;

        [Header("Radial Layout")]
        [SerializeField] private RadialChoiceLayout radialLayout;

        private readonly List<Button> _spawnedButtons = new();
        private readonly List<RectTransform> _spawnedButtonRects = new();
        #endregion

        public bool IsDialogueActive => runner != null && runner.State != DialogueRunner.DialogueState.Idle;

        private void Awake()
        {
            if (runner == null) runner = FindFirstObjectByType<DialogueRunner>();
            if (context == null) context = FindFirstObjectByType<DialogueContextBehaviour>();

            if (radialLayout == null && choicesContainer != null)
                radialLayout = choicesContainer.GetComponent<RadialChoiceLayout>();

            runner.OnLinePresented += HandleLinePresented;
            runner.OnChoicesPresented += HandleChoicesPresented;
            runner.OnDialogueEnded += HandleDialogueEnded;

            ShowSpeech(false);
            ShowChoices(false);
        }

        private void Update()
        {
            if (runner == null) return;

            if (runner.State == DialogueRunner.DialogueState.PresentingLine)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    runner.Advance();
            }
        }

        public void StartDialogue(NpcDefinition npc, DialogueAsset dialogue)
        {
            if (runner == null || context == null) return;
            if (npc == null || dialogue == null) return;

            // Avoid restarting while already in dialogue
            if (IsDialogueActive) return;

            context.SetNpc(npc);
            runner.Begin(dialogue, context);
        }

        private void HandleLinePresented(string speakerName, string text, DialogueSpeakerKind kind)
        {
            ShowChoices(false);
            ShowSpeech(true);

            speakerText.text = speakerName;
            lineText.text = text;
        }

        private void HandleChoicesPresented(IReadOnlyList<DialogueRunner.ChoiceViewModel> choices)
        {
            ShowSpeech(false);
            ShowChoices(true);

            RebuildChoices(choices);
        }

        private void HandleDialogueEnded()
        {
            ShowSpeech(false);
            ShowChoices(false);
            ClearChoices();
        }

        private void RebuildChoices(IReadOnlyList<DialogueRunner.ChoiceViewModel> choices)
        {
            ClearChoices();
            _spawnedButtonRects.Clear();

            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                var vm = choices[i];

                var btn = Instantiate(choiceButtonPrefab, choicesContainer);
                _spawnedButtons.Add(btn);

                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = vm.Text;

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => runner.Choose(index));

                var rect = btn.GetComponent<RectTransform>();
                if (rect != null)
                    _spawnedButtonRects.Add(rect);
            }

            if (radialLayout != null)
            {
                radialLayout.ApplyLayout(_spawnedButtonRects);
            }
            else
            {
                Debug.LogWarning("[DialogueTestUI] No RadialChoiceLayout assigned/found. Buttons will overlap at (0,0).");
            }

            if (EventSystem.current != null && _spawnedButtons.Count > 0)
                EventSystem.current.SetSelectedGameObject(_spawnedButtons[0].gameObject);
        }

        private void ClearChoices()
        {
            for (int i = 0; i < _spawnedButtons.Count; i++)
            {
                if (_spawnedButtons[i] != null)
                    Destroy(_spawnedButtons[i].gameObject);
            }
            _spawnedButtons.Clear();
            _spawnedButtonRects.Clear();
        }

        private void ShowSpeech(bool show)
        {
            if (speechPanel != null) speechPanel.SetActive(show);
        }

        private void ShowChoices(bool show)
        {
            if (choicesPanel != null) choicesPanel.SetActive(show);
        }

        private void OnDestroy()
        {
            if (runner != null)
            {
                runner.OnLinePresented -= HandleLinePresented;
                runner.OnChoicesPresented -= HandleChoicesPresented;
                runner.OnDialogueEnded -= HandleDialogueEnded;
            }
        }
    }
}