using UnityEngine;

namespace _Scripts.Core.Managers
{
    /// <summary>
    /// Bootstraps the debug console system.
    /// The DebugConsole component and its UI should be set up in the scene.
    /// This manager ensures it persists and can be disabled for release builds.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class DebugManager : MonoBehaviour
    {
        #region Singleton

        public static DebugManager Instance { get; private set; }

        #endregion

        #region Settings

        [Header("Console Settings")]
        [SerializeField] private bool _enableConsole = true;
        [SerializeField] private Systems.DebugConsole.DebugConsole _debugConsole;

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

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // Strip debug console from release builds
            _enableConsole = false;
#endif

            if (!_enableConsole && _debugConsole != null)
            {
                _debugConsole.gameObject.SetActive(false);
            }
        }

        #endregion
    }
}
