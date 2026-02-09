using Liquid.NPC;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liquid.Dialogue
{
    public sealed class DialogueRunner : MonoBehaviour
    {
        public enum DialogueState
        {
            Idle = 0,
            PresentingLine = 1,
            PresentingChoices = 2,
            Ended = 3
        }

        public sealed class ChoiceViewModel
        {
            public readonly string Text;
            public readonly bool IsExit;
            internal readonly DialogueChoiceOption Source;

            public ChoiceViewModel(string text, bool isExit, DialogueChoiceOption source)
            {
                Text = text;
                IsExit = isExit;
                Source = source;
            }
        }

        public event Action<string, string, DialogueSpeakerKind> OnLinePresented;
        public event Action<IReadOnlyList<ChoiceViewModel>> OnChoicesPresented;
        public event Action OnDialogueEnded;

        [Header("Runtime Choice Rules")]
        [SerializeField, Min(2)] private int minChoices = 2;
        [SerializeField, Min(2)] private int maxChoices = 4;

        [Header("Exit Option Defaults")]
        [SerializeField] private string defaultExitText = "Leave";

        private IDialogueContext _context;
        private DialogueAsset _asset;
        private DialogueNode _current;
        private DialogueState _state = DialogueState.Idle;

        private readonly List<ChoiceViewModel> _currentChoices = new();

        public DialogueState State => _state;
        public IReadOnlyList<ChoiceViewModel> CurrentChoices => _currentChoices;

        public void Begin(DialogueAsset asset, IDialogueContext context)
        {
            _asset = asset;
            _context = context;

            _currentChoices.Clear();

            if (_asset == null || _asset.EntryNode == null)
            {
                EndDialogue();
                return;
            }

            _current = _asset.EntryNode;
            StepIntoNode(_current);
        }

        public void Advance()
        {
            if (_state != DialogueState.PresentingLine) return;

            if (_current is DialogueLineNode line)
            {
                _current = line.Next;
                StepIntoNode(_current);
            }
            else
            {
                StepIntoNode(_current);
            }
        }

        public void Choose(int index)
        {
            if (_state != DialogueState.PresentingChoices) return;
            if (index < 0 || index >= _currentChoices.Count) return;

            var vm = _currentChoices[index];
            var opt = vm.Source;

            if (opt?.Effects != null)
            {
                for (int i = 0; i < opt.Effects.Count; i++)
                    opt.Effects[i]?.Apply(_context);
            }

            if (vm.IsExit || opt == null || opt.Next == null)
            {
                EndDialogue();
                return;
            }

            _current = opt.Next;
            StepIntoNode(_current);
        }

        public void Cancel()
        {
            if (_state == DialogueState.PresentingChoices)
            {
                int exitIndex = -1;
                for (int i = 0; i < _currentChoices.Count; i++)
                {
                    if (_currentChoices[i].IsExit)
                    {
                        exitIndex = i;
                        break;
                    }
                }

                if (exitIndex >= 0)
                {
                    Choose(exitIndex);
                    return;
                }
            }

            EndDialogue();
        }

        private void StepIntoNode(DialogueNode node)
        {
            _currentChoices.Clear();

            if (node == null)
            {
                EndDialogue();
                return;
            }

            if (node is DialogueLineNode lineNode)
            {
                _state = DialogueState.PresentingLine;

                string speakerName = ResolveSpeakerName(_context?.CurrentNpc, lineNode.SpeakerKind);
                OnLinePresented?.Invoke(speakerName, lineNode.Text, lineNode.SpeakerKind);
                return;
            }

            if (node is DialogueChoiceNode choiceNode)
            {
                _state = DialogueState.PresentingChoices;

                BuildChoices(choiceNode);
                OnChoicesPresented?.Invoke(_currentChoices);
                return;
            }

            EndDialogue();
        }

        private void BuildChoices(DialogueChoiceNode node)
        {
            var available = new List<DialogueChoiceOption>(8);

            var opts = node.Options;
            if (opts != null)
            {
                for (int i = 0; i < opts.Count; i++)
                {
                    var opt = opts[i];
                    if (opt == null) continue;

                    if (AreConditionsMet(opt))
                        available.Add(opt);
                }
            }

            if (available.Count > maxChoices)
                available.RemoveRange(maxChoices, available.Count - maxChoices);

            _currentChoices.Clear();
            for (int i = 0; i < available.Count; i++)
            {
                var opt = available[i];
                _currentChoices.Add(new ChoiceViewModel(opt.Text, opt.IsExitOption, opt));
            }

            if (_currentChoices.Count == 0)
                EndDialogue();
        }

        private bool AreConditionsMet(DialogueChoiceOption option)
        {
            var conds = option.Conditions;
            if (conds == null || conds.Count == 0) return true;

            for (int i = 0; i < conds.Count; i++)
            {
                var c = conds[i];
                if (c == null) continue;

                if (!c.IsMet(_context))
                    return false;
            }

            return true;
        }

        private string ResolveSpeakerName(NpcDefinition npc, DialogueSpeakerKind kind)
        {
            return kind switch
            {
                DialogueSpeakerKind.Npc => npc != null ? npc.DisplayName : "NPC",
                DialogueSpeakerKind.Player => "You",
                DialogueSpeakerKind.System => string.Empty,
                _ => string.Empty
            };
        }

        private void EndDialogue()
        {
            _state = DialogueState.Ended;
            _current = null;
            _asset = null;
            _currentChoices.Clear();

            OnDialogueEnded?.Invoke();

            _state = DialogueState.Idle;
        }

        private void OnValidate()
        {
            if (minChoices < 2) minChoices = 2;
            if (maxChoices < 2) maxChoices = 2;
            if (maxChoices < minChoices) maxChoices = minChoices;

            if (string.IsNullOrWhiteSpace(defaultExitText))
                defaultExitText = "Leave";
        }
    }
}