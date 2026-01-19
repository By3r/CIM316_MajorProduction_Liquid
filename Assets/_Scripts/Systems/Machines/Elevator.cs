/*
 * NOT YET INTEGRATED - Commented out for later integration
 * Remove #if false and #endif when ready to integrate Elevator system
 */
#if false

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using _Scripts.Core.Managers;
using _Scripts.Systems.Inventory;

namespace _Scripts.Systems.Machines
{
    /// <summary>
    /// Elevator controller for floor transitions.
    /// Requires PowerCell to operate. Goes up or down based on type.
    /// </summary>
    public class Elevator : PoweredMachine
    {
        #region Events

        public event Action OnElevatorActivated;
        public event Action OnTransitionStarted;
        public event Action OnTransitionComplete;

        #endregion

        #region Serialized Fields

        [Header("Elevator Configuration")]
        [SerializeField] private ElevatorType _elevatorType = ElevatorType.Exit;
        [SerializeField] private string _gameSceneName = "Game";

        [Header("Transition Settings")]
        [SerializeField] private float _transitionDelay = 2f;
        [SerializeField] private bool _requirePowerCell = true;

        [Header("Interaction")]
        [SerializeField] private float _interactionRange = 3f;
        [SerializeField] private Transform _playerStandPoint;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject _doorsClosed;
        [SerializeField] private GameObject _doorsOpen;
        [SerializeField] private Animator _doorAnimator;
        [SerializeField] private string _openDoorsAnimTrigger = "Open";
        [SerializeField] private string _closeDoorsAnimTrigger = "Close";

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _doorOpenSound;
        [SerializeField] private AudioClip _doorCloseSound;
        [SerializeField] private AudioClip _elevatorMoveSound;

        [Header("Events")]
        [SerializeField] private UnityEvent _onElevatorActivated;
        [SerializeField] private UnityEvent _onDoorsOpened;
        [SerializeField] private UnityEvent _onDoorsClosed;

        #endregion

        #region Private Fields

        private bool _isTransitioning = false;
        private bool _doorsAreOpen = false;

        #endregion

        #region Properties

        public ElevatorType ElevatorType => _elevatorType;
        public bool IsTransitioning => _isTransitioning;
        public bool DoorsAreOpen => _doorsAreOpen;
        public float InteractionRange => _interactionRange;

        public string InteractionPrompt
        {
            get
            {
                if (_requirePowerCell && !IsPowered)
                    return "Requires PowerCell";

                if (_isTransitioning)
                    return "Transitioning...";

                return _elevatorType == ElevatorType.Exit
                    ? "Go Down (Next Floor)"
                    : "Go Up (Previous Floor)";
            }
        }

        public bool CanOperate
        {
            get
            {
                if (_isTransitioning) return false;
                if (_requirePowerCell && !IsPowered) return false;

                var floorManager = FloorStateManager.Instance;
                if (floorManager == null) return false;

                // Entry elevator (go up) can't go above floor 1
                if (_elevatorType == ElevatorType.Entry && floorManager.CurrentFloorNumber <= 1)
                    return false;

                return true;
            }
        }

        #endregion

        #region Unity Lifecycle

        protected override void OnPoweredOn()
        {
            base.OnPoweredOn();
            OpenDoors();
        }

        protected override void OnPoweredOff()
        {
            base.OnPoweredOff();
            CloseDoors();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to use the elevator to transition floors.
        /// </summary>
        public bool TryUseElevator()
        {
            if (!CanOperate)
            {
                Debug.Log($"[Elevator] Cannot operate. Powered: {IsPowered}, Transitioning: {_isTransitioning}");
                return false;
            }

            StartCoroutine(TransitionCoroutine());
            return true;
        }

        /// <summary>
        /// Opens the elevator doors.
        /// </summary>
        public void OpenDoors()
        {
            if (_doorsAreOpen) return;

            _doorsAreOpen = true;

            if (_doorAnimator != null)
            {
                _doorAnimator.SetTrigger(_openDoorsAnimTrigger);
            }
            else
            {
                if (_doorsClosed != null) _doorsClosed.SetActive(false);
                if (_doorsOpen != null) _doorsOpen.SetActive(true);
            }

            PlaySound(_doorOpenSound);
            _onDoorsOpened?.Invoke();
        }

        /// <summary>
        /// Closes the elevator doors.
        /// </summary>
        public void CloseDoors()
        {
            if (!_doorsAreOpen) return;

            _doorsAreOpen = false;

            if (_doorAnimator != null)
            {
                _doorAnimator.SetTrigger(_closeDoorsAnimTrigger);
            }
            else
            {
                if (_doorsClosed != null) _doorsClosed.SetActive(true);
                if (_doorsOpen != null) _doorsOpen.SetActive(false);
            }

            PlaySound(_doorCloseSound);
            _onDoorsClosed?.Invoke();
        }

        #endregion

        #region Private Methods

        private IEnumerator TransitionCoroutine()
        {
            _isTransitioning = true;
            OnTransitionStarted?.Invoke();

            // Close doors
            CloseDoors();

            // Play elevator movement sound
            PlaySound(_elevatorMoveSound);

            // Wait for transition
            yield return new WaitForSeconds(_transitionDelay);

            // Update floor number
            var floorManager = FloorStateManager.Instance;
            if (floorManager != null)
            {
                if (_elevatorType == ElevatorType.Exit)
                {
                    // Going down - increase floor number
                    floorManager.CurrentFloorNumber++;
                    Debug.Log($"[Elevator] Going DOWN to floor {floorManager.CurrentFloorNumber}");
                }
                else
                {
                    // Going up - decrease floor number
                    floorManager.CurrentFloorNumber--;
                    Debug.Log($"[Elevator] Going UP to floor {floorManager.CurrentFloorNumber}");
                }

                // Mark current floor as visited
                floorManager.MarkCurrentFloorAsVisited();
            }

            OnElevatorActivated?.Invoke();
            _onElevatorActivated?.Invoke();

            // Reload the scene
            SceneManager.LoadScene(_gameSceneName);

            OnTransitionComplete?.Invoke();
            _isTransitioning = false;
        }

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _elevatorType == ElevatorType.Exit ? Color.red : Color.blue;
            Gizmos.DrawWireSphere(transform.position, _interactionRange);

            if (_playerStandPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(_playerStandPoint.position, Vector3.one * 0.5f);
            }
        }

        #endregion
    }

    public enum ElevatorType
    {
        Entry,  // Player enters floor here (go UP to previous floor)
        Exit    // Player exits floor here (go DOWN to next floor)
    }
}

#endif
