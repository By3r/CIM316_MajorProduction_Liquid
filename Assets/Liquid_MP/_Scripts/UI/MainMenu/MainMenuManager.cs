using _Scripts.Core.Persistence;
using _Scripts.Core.SceneFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Scripts.UI.MainMenu
{
    /// <summary>
    /// Manages the main menu layout, button visibility, separator visibility, and panel switching.
    /// Supports both keyboard/controller selection and mouse hover selection.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        #region Variables
        [Header("Buttons")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        [Header("Separators")]
        [SerializeField] private GameObject continueSeparator;
        [SerializeField] private GameObject loadSeparator;
        [SerializeField] private GameObject newGameSeparator;
        [SerializeField] private GameObject settingsSeparator;

        [Header("Panels")]
        [SerializeField] private GameObject rootMenuPanel;
        [SerializeField] private GameObject loadGamePanel;
        [SerializeField] private GameObject newGamePanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Optional Labels")]
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField] private TMP_Text loadLabel;
        [SerializeField] private TMP_Text newGameLabel;
        [SerializeField] private TMP_Text settingsLabel;
        [SerializeField] private TMP_Text exitLabel;

        [Header("Colors")]
        [SerializeField] private Color normalTextColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        [SerializeField] private Color selectedTextColor = Color.white;

        [Header("Selection")]
        [SerializeField] private Button defaultSelectedButtonWhenSaveExists;
        [SerializeField] private Button defaultSelectedButtonWhenNoSave;

        private bool hasSave;
        private Button currentSelectedButton;
        #endregion

        private void Awake()
        {
            BindButtons();
            CloseAllSubPanels();
            RefreshMenu();
        }

        private void OnEnable()
        {
            RefreshMenu();
        }

        private void Update()
        {
            UpdateSelectedVisualsFromEventSystem();
        }

        private void BindButtons()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinuePressed);
                continueButton.onClick.AddListener(OnContinuePressed);
                AddPointerSelectHelper(continueButton);
            }

            if (loadButton != null)
            {
                loadButton.onClick.RemoveListener(OnLoadPressed);
                loadButton.onClick.AddListener(OnLoadPressed);
                AddPointerSelectHelper(loadButton);
            }

            if (newGameButton != null)
            {
                newGameButton.onClick.RemoveListener(OnNewGamePressed);
                newGameButton.onClick.AddListener(OnNewGamePressed);
                AddPointerSelectHelper(newGameButton);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OnSettingsPressed);
                settingsButton.onClick.AddListener(OnSettingsPressed);
                AddPointerSelectHelper(settingsButton);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitPressed);
                exitButton.onClick.AddListener(OnExitPressed);
                AddPointerSelectHelper(exitButton);
            }
        }

        private void AddPointerSelectHelper(Button button)
        {
            MenuButtonPointerHelper helper = button.GetComponent<MenuButtonPointerHelper>();

            if (helper == null)
            {
                helper = button.gameObject.AddComponent<MenuButtonPointerHelper>();
            }

            helper.Initialize(this, button);
        }

        /// <summary>
        /// Called by pointer helper so hovered buttons also become selected for keyboard/controller continuity.
        /// </summary>
        public void NotifyPointerEntered(Button button)
        {
            if (button == null || !button.gameObject.activeInHierarchy)
            {
                return;
            }

            EventSystem.current?.SetSelectedGameObject(button.gameObject);
            currentSelectedButton = button;
            RefreshButtonColors();
        }

        public void RefreshMenu()
        {
            hasSave = SaveSystem.AnySaveExists();

            SetButtonAndSeparatorVisibility();
            CloseAllSubPanels();
            SelectDefaultButton();
            RefreshButtonColors();
        }

        private void SetButtonAndSeparatorVisibility()
        {
            SetMenuElementVisible(continueButton, hasSave);
            SetMenuElementVisible(loadButton, hasSave);

            SetGameObjectVisible(continueSeparator, hasSave);
            SetGameObjectVisible(loadSeparator, hasSave);

            SetMenuElementVisible(newGameButton, true);
            SetMenuElementVisible(settingsButton, true);
            SetMenuElementVisible(exitButton, true);

            SetGameObjectVisible(newGameSeparator, true);
            SetGameObjectVisible(settingsSeparator, true);
        }

        private void SetMenuElementVisible(Button button, bool visible)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(visible);
        }

        private void SetGameObjectVisible(GameObject target, bool visible)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(visible);
        }

        private void CloseAllSubPanels()
        {
            if (rootMenuPanel != null)
            {
                rootMenuPanel.SetActive(true);
            }

            if (loadGamePanel != null)
            {
                loadGamePanel.SetActive(false);
            }

            if (newGamePanel != null)
            {
                newGamePanel.SetActive(false);
            }

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        private void SelectDefaultButton()
        {
            Button targetButton = hasSave ? defaultSelectedButtonWhenSaveExists : defaultSelectedButtonWhenNoSave;

            if (targetButton == null)
            {
                targetButton = hasSave ? continueButton : newGameButton;
            }

            if (targetButton != null && targetButton.gameObject.activeInHierarchy)
            {
                EventSystem.current?.SetSelectedGameObject(targetButton.gameObject);
                currentSelectedButton = targetButton;
            }
        }

        private void UpdateSelectedVisualsFromEventSystem()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

            if (selectedObject == null)
            {
                return;
            }

            Button selectedButton = selectedObject.GetComponent<Button>();

            if (selectedButton == null)
            {
                return;
            }

            if (currentSelectedButton == selectedButton)
            {
                return;
            }

            currentSelectedButton = selectedButton;
            RefreshButtonColors();
        }

        private void RefreshButtonColors()
        {
            ApplyLabelColor(continueButton, continueLabel);
            ApplyLabelColor(loadButton, loadLabel);
            ApplyLabelColor(newGameButton, newGameLabel);
            ApplyLabelColor(settingsButton, settingsLabel);
            ApplyLabelColor(exitButton, exitLabel);
        }

        private void ApplyLabelColor(Button button, TMP_Text label)
        {
            if (button == null || label == null || !button.gameObject.activeInHierarchy)
            {
                return;
            }

            label.color = currentSelectedButton == button ? selectedTextColor : normalTextColor;
        }

        #region Button Actions
        private void OnContinuePressed()
        {
            if (!SaveSystem.AnySaveExists())
            {
                return;
            }

            LoadGameManager loadGameManager = loadGamePanel != null ? loadGamePanel.GetComponent<LoadGameManager>() : null;

            if (loadGameManager != null)
            {
                loadGameManager.ContinueMostRecentSave();
                return;
            }

            if (SceneTransitionManager.Instance == null)
            {
                Debug.LogError("SceneTransitionManager instance is missing.");
                return;
            }

            SceneTransitionManager.Instance.ContinueFromSave();
        }

        private void OnLoadPressed()
        {
            if (!SaveSystem.AnySaveExists())
            {
                return;
            }

            if (rootMenuPanel != null)
            {
                rootMenuPanel.SetActive(false);
            }

            if (loadGamePanel != null)
            {
                loadGamePanel.SetActive(true);

                LoadGameManager loadGameManager = loadGamePanel.GetComponent<LoadGameManager>();
                if (loadGameManager != null)
                {
                    loadGameManager.RefreshPanel();
                }
            }
        }

        private void OnNewGamePressed()
        {
            if (rootMenuPanel != null)
            {
                rootMenuPanel.SetActive(false);
            }

            if (newGamePanel != null)
            {
                newGamePanel.SetActive(true);

                NewGameManager newGameManager = newGamePanel.GetComponent<NewGameManager>();
                if (newGameManager != null)
                {
                    newGameManager.PreparePanel();
                }
            }
        }

        private void OnSettingsPressed()
        {
            if (rootMenuPanel != null)
            {
                rootMenuPanel.SetActive(false);
            }

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);

                SettingsManager settingsManager = settingsPanel.GetComponent<SettingsManager>();
                if (settingsManager != null)
                {
                    settingsManager.OpenSettings();
                }
            }
        }

        private void OnExitPressed()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        #endregion

        #region Public UI Hooks
        public void UI_BackFromLoadPanel()
        {
            RefreshMenu();
        }

        public void UI_BackFromNewGamePanel()
        {
            RefreshMenu();
        }

        public void UI_BackFromSettingsPanel()
        {
            RefreshMenu();
        }

        public void UI_RefreshMenuState()
        {
            RefreshMenu();
        }
        #endregion
    }
}