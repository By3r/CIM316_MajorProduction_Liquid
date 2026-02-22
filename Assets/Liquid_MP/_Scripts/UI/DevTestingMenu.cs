using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _Scripts.UI
{
    /// <summary>
    /// Developer testing menu for quick scene navigation.
    /// Persists across scene loads. Use DevTestingMenuSetup to create the UI structure.
    /// </summary>
    public class DevTestingMenu : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Scene Configuration")]
        [SerializeField] private List<string> _sceneNames = new List<string>();

        [Header("UI References")]
        [SerializeField] private Button _toggleButton;
        [SerializeField] private TMP_Text _toggleButtonText;
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private Transform _buttonContainer;
        [SerializeField] private GameObject _buttonPrefab;

        [Header("Styling")]
        [SerializeField] private Color _closeButtonTextColor = new Color(1f, 0.5f, 0.2f, 1f);

        [Header("Event System")]
        [SerializeField] private EventSystem _eventSystem;

        #endregion

        #region Private Fields

        private static DevTestingMenu _instance;
        private bool _isMenuOpen;
        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();

        #endregion

        #region Initialization

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            SetupToggleButton();
            PopulateSceneButtons();
            CloseMenu();
            
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystemExists();
        }

        private void EnsureEventSystemExists()
        {
            var existingEventSystem = FindFirstObjectByType<EventSystem>();
            
            if (existingEventSystem == null)
            {
                if (_eventSystem != null)
                {
                    _eventSystem.gameObject.SetActive(true);
                }
            }
            else if (existingEventSystem != _eventSystem && _eventSystem != null)
            {
                _eventSystem.gameObject.SetActive(false);
            }
        }

        private void SetupToggleButton()
        {
            if (_toggleButton != null)
            {
                _toggleButton.onClick.RemoveAllListeners();
                _toggleButton.onClick.AddListener(ToggleMenu);
            }
        }

        #endregion

        #region Button Population

        /// <summary>
        /// Clears existing buttons and repopulates from scene list.
        /// </summary>
        public void RefreshButtons()
        {
            ClearSpawnedButtons();
            PopulateSceneButtons();
        }

        private void PopulateSceneButtons()
        {
            if (_buttonPrefab == null || _buttonContainer == null) return;

            foreach (var sceneName in _sceneNames)
            {
                if (string.IsNullOrEmpty(sceneName)) continue;
                CreateSceneButton(sceneName);
            }

            CreateCloseButton();
        }

        private void CreateSceneButton(string sceneName)
        {
            var buttonObj = Instantiate(_buttonPrefab, _buttonContainer);
            buttonObj.name = $"Button_{sceneName}";

            var text = buttonObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = sceneName;
            }

            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                string sceneToLoad = sceneName;
                button.onClick.AddListener(() => LoadScene(sceneToLoad));
            }

            _spawnedButtons.Add(buttonObj);
        }

        private void CreateCloseButton()
        {
            var buttonObj = Instantiate(_buttonPrefab, _buttonContainer);
            buttonObj.name = "Button_CloseMenu";

            var text = buttonObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = "Close Menu";
                text.color = _closeButtonTextColor;
            }

            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(CloseMenu);
            }

            _spawnedButtons.Add(buttonObj);
        }

        private void ClearSpawnedButtons()
        {
            foreach (var button in _spawnedButtons)
            {
                if (button != null)
                {
                    if (Application.isPlaying)
                        Destroy(button);
                    else
                        DestroyImmediate(button);
                }
            }
            _spawnedButtons.Clear();
        }

        #endregion

        #region Menu Actions

        private void ToggleMenu()
        {
            if (_isMenuOpen)
                CloseMenu();
            else
                OpenMenu();
        }

        private void OpenMenu()
        {
            _isMenuOpen = true;
            if (_menuPanel != null) _menuPanel.SetActive(true);
            UpdateToggleButtonText();
        }

        private void CloseMenu()
        {
            _isMenuOpen = false;
            if (_menuPanel != null) _menuPanel.SetActive(false);
            UpdateToggleButtonText();
        }

        private void UpdateToggleButtonText()
        {
            if (_toggleButtonText != null)
            {
                _toggleButtonText.text = _isMenuOpen 
                    ? "Close Developer Testing Menu" 
                    : "Open Developer Testing Menu";
            }
        }

        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[DevTestingMenu] Scene name is empty.");
                return;
            }

            CloseMenu();
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion
    }
}