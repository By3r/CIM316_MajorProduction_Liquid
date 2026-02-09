using System.Collections;
using System.Collections.Generic;
using _Scripts.Core.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _Scripts.Systems.DebugConsole
{
    /// <summary>
    /// Developer debug console with text input and scrollable output log.
    /// Toggle with backtick (`). Assign UI references in the inspector.
    /// Persists across scene loads via DontDestroyOnLoad.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class DebugConsole : MonoBehaviour
    {
        #region Singleton

        public static DebugConsole Instance { get; private set; }

        #endregion

        #region Settings

        private const int MAX_LOG_LINES = 200;

        #endregion

        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private GameObject _consolePanel;
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private TextMeshProUGUI _outputText;
        [SerializeField] private ScrollRect _scrollRect;

        #endregion

        #region Private Fields

        private InputAction _toggleAction;
        private bool _isOpen;
        private GameState _stateBeforeOpen;
        private bool _needsScrollToBottom;
        private int _scrollWaitFrames;

        private readonly List<string> _logLines = new();
        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;

        #endregion

        #region Properties

        /// <summary>
        /// Whether the debug console is currently visible.
        /// </summary>
        public bool IsOpen => _isOpen;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            DebugCommandRegistry.Initialize();

            // Ensure rich text is enabled for color tags in command output
            if (_outputText != null)
                _outputText.richText = true;

            SetConsoleVisible(false);

            AppendOutput("=== LIQUID DEBUG CONSOLE ===");
            AppendOutput("Type 'help' for a list of commands.");
            AppendOutput("");
        }

        private void OnEnable()
        {
            _toggleAction = new InputAction("ToggleConsole", InputActionType.Button, "<Keyboard>/backquote");
            _toggleAction.performed += OnToggleConsole;
            _toggleAction.Enable();
        }

        private void OnDisable()
        {
            if (_toggleAction != null)
            {
                _toggleAction.performed -= OnToggleConsole;
                _toggleAction.Disable();
                _toggleAction.Dispose();
                _toggleAction = null;
            }
        }

        private void Update()
        {
            if (!_isOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                SubmitCommand();
            }

            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                NavigateHistory(-1);
            }

            if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                NavigateHistory(1);
            }
        }

        private void LateUpdate()
        {
            if (!_needsScrollToBottom) return;

            // Wait 2 frames for TMP text rebuild and ContentSizeFitter layout pass
            _scrollWaitFrames--;
            if (_scrollWaitFrames > 0) return;

            _needsScrollToBottom = false;

            if (_scrollRect != null && _scrollRect.content != null)
            {
                // Force TMP to regenerate mesh so preferred height is accurate
                if (_outputText != null)
                    _outputText.ForceMeshUpdate();

                // Force ContentSizeFitter and layout groups to recalculate
                LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
                Canvas.ForceUpdateCanvases();

                _scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        #endregion

        #region Toggle

        private void OnToggleConsole(InputAction.CallbackContext context)
        {
            // Block during loading
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Loading)
                return;

            if (_isOpen)
                Close();
            else
                Open();
        }

        /// <summary>
        /// Opens the debug console.
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;

            // Save current game state so we can restore properly on close
            if (GameManager.Instance != null)
                _stateBeforeOpen = GameManager.Instance.CurrentState;

            SetConsoleVisible(true);

            // Disable player input
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnablePlayerInput(false);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Focus input field and strip the backtick character
            StartCoroutine(FocusInputFieldDelayed());
        }

        /// <summary>
        /// Closes the debug console.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            SetConsoleVisible(false);

            // Only restore player input if we were in a gameplay state before opening
            if (_stateBeforeOpen == GameState.Gameplay || _stateBeforeOpen == GameState.SafeRoom)
            {
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.EnablePlayerInput(true);
                }

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private IEnumerator FocusInputFieldDelayed()
        {
            yield return null; // Wait one frame so the backtick character is processed

            if (_inputField != null)
            {
                _inputField.text = "";
                _inputField.ActivateInputField();
                _inputField.Select();
            }
        }

        private void SetConsoleVisible(bool visible)
        {
            if (_consolePanel != null)
                _consolePanel.SetActive(visible);
        }

        #endregion

        #region Command Submission

        private void SubmitCommand()
        {
            if (_inputField == null) return;

            string input = _inputField.text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                _inputField.ActivateInputField();
                return;
            }

            // Log the input
            AppendOutput($"> {input}");

            // Add to history
            _commandHistory.Add(input);
            _historyIndex = _commandHistory.Count;

            // Execute
            string result = DebugCommandRegistry.ExecuteCommand(input);
            if (!string.IsNullOrEmpty(result))
            {
                AppendOutput(result);
            }

            // Clear and refocus
            _inputField.text = "";
            _inputField.ActivateInputField();
        }

        #endregion

        #region Command History

        private void NavigateHistory(int direction)
        {
            if (_commandHistory.Count == 0) return;

            _historyIndex += direction;
            _historyIndex = Mathf.Clamp(_historyIndex, 0, _commandHistory.Count);

            if (_historyIndex < _commandHistory.Count)
            {
                _inputField.text = _commandHistory[_historyIndex];
                _inputField.caretPosition = _inputField.text.Length;
            }
            else
            {
                _inputField.text = "";
            }
        }

        #endregion

        #region Output

        /// <summary>
        /// Appends a line to the console output log.
        /// </summary>
        public void AppendOutput(string text)
        {
            _logLines.Add(text);
            while (_logLines.Count > MAX_LOG_LINES)
                _logLines.RemoveAt(0);

            if (_outputText != null)
            {
                _outputText.text = string.Join("\n", _logLines);
            }

            // Request scroll to bottom — LateUpdate handles it after layout rebuilds
            _needsScrollToBottom = true;
            _scrollWaitFrames = 2;
        }

        /// <summary>
        /// Clears all console output.
        /// </summary>
        public void ClearOutput()
        {
            _logLines.Clear();
            if (_outputText != null)
                _outputText.text = "";
        }

        /// <summary>
        /// Static shortcut for logging to the console from anywhere.
        /// </summary>
        public static void Log(string message)
        {
            Instance?.AppendOutput(message);
        }

        #endregion
    }
}
