using System.Collections;
using System.Collections.Generic;
using _Scripts.Core.Managers;
using _Scripts.Core.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace MainMenu.UI
{
    public class DiegeticMainMenuController : MonoBehaviour
    {
        #region Variables
        [Header("Transforms")]
        [Tooltip("Pivot transform of the flashlight or the bone that should rotate toward menu buttons.")]
        [SerializeField] private Transform flashlightPivot;

        [SerializeField] private Transform playerRoot;

        [Tooltip("List of menu buttons in navigation order (top to bottom).")]
        [SerializeField] private List<DiegeticMenuButton> menuButtons = new List<DiegeticMenuButton>();

        [Header("Flashlight Movement")]
        [SerializeField] private float flashlightRotateSpeed = 6f;
        [SerializeField] private float playerTurnLerpSpeed = 4f;

        [Header("Input Settings")]
        [Tooltip("Minimum time between navigation steps to avoid overshooting with a stick, keyboard, or mouse.")]
        [SerializeField] private float navigationCooldown = 0.25f;

        [Tooltip("How strong the vertical input must be before it counts as a step.")]
        [SerializeField] private float navigationThreshold = 0.5f;

        [Header("Scenes")]
        [Tooltip("Name of the safe room scene to load for New Game / Load Game.")]
        [SerializeField] private string safeRoomSceneName = "Game";

        [Header("Panels")]
        [Tooltip("Panel shown when Load Game is pressed but no save file exists.")]
        [SerializeField] private GameObject noSavePanel;
        [SerializeField] private TMP_Text noSaveText;
        [SerializeField] private float noSaveMessageDuration = 5f;

        [Tooltip("Panel shown when New Game is pressed but a save file already exists.")]
        [SerializeField] private GameObject overwriteConfirmPanel;
        [SerializeField] private TMP_Text overwriteConfirmText;

        private int _currentIndex;
        private float _lastNavigateTime;
        private bool _isModalOpen;
        private Coroutine _noSaveCoroutine;
        #endregion

        private void Awake()
        {
            if (flashlightPivot == null)
            {
                Debug.LogWarning("Flashlight pivot is not assigned.", this);
            }

            if (menuButtons.Count == 0)
            {
                Debug.LogWarning("No menu buttons assigned.", this);
            }

            if (noSavePanel != null)
            {
                noSavePanel.SetActive(false);
            }

            if (overwriteConfirmPanel != null)
            {
                overwriteConfirmPanel.SetActive(false);
            }

            SetSelectedIndex(0, force: true);
        }

        private void Update()
        {
            if (_isModalOpen)
            {
                AimFlashlightAtCurrentButton();
                return;
            }

            HandleNavigationInput();
            HandleSubmitInput();
            AimFlashlightAtCurrentButton();
        }

        private void HandleNavigationInput()
        {
            if (InputManager.Instance == null || menuButtons.Count == 0)
            {
                return;
            }

            Vector2 navigate = InputManager.Instance.NavigateInput;
            float navY = navigate.y;

            bool usingMouseForThisStep = false;

            Vector2 look = InputManager.Instance.LookInput;

            if (Mathf.Abs(look.y) > Mathf.Abs(navY))
            {
                navY = look.y;
                usingMouseForThisStep = true;
            }

            if (Time.unscaledTime < _lastNavigateTime + navigationCooldown)
            {
                return;
            }

            int direction = 0;

            if (navY > navigationThreshold)
            {
                direction = -1;
            }
            else if (navY < -navigationThreshold)
            {
                direction = 1;
            }

            if (direction == 0)
            {
                return;
            }

            if (usingMouseForThisStep)
            {
                bool atTopAndGoingUp = direction < 0 && _currentIndex == 0;
                bool atBottomAndGoingDown = direction > 0 && _currentIndex == menuButtons.Count - 1;

                if (atTopAndGoingUp || atBottomAndGoingDown)
                {
                    return;
                }
            }

            _lastNavigateTime = Time.unscaledTime;

            int newIndex = _currentIndex + direction;

            if (usingMouseForThisStep)
            {
                newIndex = Mathf.Clamp(newIndex, 0, menuButtons.Count - 1);
            }
            else
            {
                if (newIndex < 0)
                {
                    newIndex = menuButtons.Count - 1;
                }
                else if (newIndex >= menuButtons.Count)
                {
                    newIndex = 0;
                }
            }

            SetSelectedIndex(newIndex);
        }

        private void HandleSubmitInput()
        {
            if (InputManager.Instance == null || menuButtons.Count == 0)
            {
                return;
            }

            bool submit = InputManager.Instance.SubmitPressed;
            bool interact = InputManager.Instance.InteractPressed;

            if (!submit && !interact)
            {
                return;
            }

            DiegeticMenuButton currentButton = menuButtons[_currentIndex];

            switch (currentButton.Type)
            {
                case DiegeticMenuButton.ButtonType.NewGame:
                    OnNewGamePressed();
                    break;

                case DiegeticMenuButton.ButtonType.LoadGame:
                    OnLoadGamePressed();
                    break;

                case DiegeticMenuButton.ButtonType.Settings:
                    OnSettingsPressed();
                    break;

                case DiegeticMenuButton.ButtonType.Exit:
                    OnExitPressed();
                    break;
            }

            currentButton.Activate();
        }

        #region Button behaviours

        private void OnNewGamePressed()
        {
            if (SaveSystem.SaveExists())
            {
                if (overwriteConfirmPanel != null)
                {
                    overwriteConfirmPanel.SetActive(true);
                    _isModalOpen = true;

                    if (overwriteConfirmText != null)
                    {
                        overwriteConfirmText.text ="Starting a New Game will overwrite your previous save.\nContinue?";
                            }
                }
                else
                {
                    StartNewGameAndCreateSave();
                }
            }
            else
            {
                StartNewGameAndCreateSave();
            }
        }

        private void OnLoadGamePressed()
        {
            if (!SaveSystem.SaveExists())
            {
                ShowNoSaveMessage("No saved game found.\nStart a New Game first.");
                return;
            }

            LoadGameScene();
        }

        private void OnSettingsPressed()
        {
            Debug.Log("Settings pressed. (TODO: rotate camera up and open settings canvas.)");
        }

        private void OnExitPressed()
        {
            Debug.Log("Exit pressed.");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region Panels
        public void UI_ConfirmOverwriteNewGame()
        {
            if (overwriteConfirmPanel != null)
            {
                overwriteConfirmPanel.SetActive(false);
            }

            _isModalOpen = false;
            StartNewGameAndCreateSave();
        }

        public void UI_CancelOverwriteNewGame()
        {
            if (overwriteConfirmPanel != null)
            {
                overwriteConfirmPanel.SetActive(false);
            }

            _isModalOpen = false;
        }

        private void ShowNoSaveMessage(string message)
        {
            if (noSavePanel == null)
            {
                Debug.LogWarning("NoSavePanel is not assigned.");
                return;
            }

            if (_noSaveCoroutine != null)
            {
                StopCoroutine(_noSaveCoroutine);
            }

            if (noSaveText != null)
            {
                noSaveText.text = message;
            }

            noSavePanel.SetActive(true);
            _noSaveCoroutine = StartCoroutine(NoSaveMessageRoutine());
        }

        private IEnumerator NoSaveMessageRoutine()
        {
            _isModalOpen = true;
            yield return new WaitForSecondsRealtime(noSaveMessageDuration);
            if (noSavePanel != null)
            {
                noSavePanel.SetActive(false);
            }
            _isModalOpen = false;
            _noSaveCoroutine = null;
        }

        #endregion

        #region Scene and Save helper functions.
        private void StartNewGameAndCreateSave()
        {
            string debugName = $"DebugPlayer_{Random.Range(1000, 9999)}";
            GameSaveData data = new GameSaveData(debugName);
            SaveSystem.SaveGame(data);

            LoadGameScene();
        }

        private void LoadGameScene()
        {
            if (string.IsNullOrEmpty(safeRoomSceneName))
            {
                Debug.LogError("Game scene name is not set.");
                return;
            }

            SceneManager.LoadScene(safeRoomSceneName);
        }
        #endregion

        private void AimFlashlightAtCurrentButton()
        {
            if (flashlightPivot == null || menuButtons.Count == 0)
            {
                return;
            }

            DiegeticMenuButton currentButton = menuButtons[_currentIndex];
            Transform target = currentButton.AimTarget;

            if (target == null)
            {
                return;
            }

            Vector3 direction = target.position - flashlightPivot.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            direction.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);

            flashlightPivot.rotation = Quaternion.Slerp(flashlightPivot.rotation, targetRotation, flashlightRotateSpeed * Time.unscaledDeltaTime);

            if (playerRoot != null)
            {
                Vector3 flatDir = direction;
                flatDir.y = 0f;

                if (flatDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion bodyRotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
                    playerRoot.rotation = Quaternion.Slerp(playerRoot.rotation, bodyRotation, playerTurnLerpSpeed * Time.unscaledDeltaTime);
                }
            }
        }

        private void SetSelectedIndex(int newIndex, bool force = false)
        {
            if (!force && newIndex == _currentIndex)
            {
                return;
            }

            if (menuButtons.Count == 0)
            {
                _currentIndex = 0;
                return;
            }

            newIndex = Mathf.Clamp(newIndex, 0, menuButtons.Count - 1);

            if (!force && _currentIndex >= 0 && _currentIndex < menuButtons.Count)
            {
                menuButtons[_currentIndex].SetSelected(false);
            }

            _currentIndex = newIndex;

            if (_currentIndex >= 0 && _currentIndex < menuButtons.Count)
            {
                menuButtons[_currentIndex].SetSelected(true);
            }
        }
    }
}