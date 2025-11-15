using System.Collections;
using _Scripts.Core.Managers;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration.Doors
{
    /// <summary>
    /// Door component for procedurally generated rooms in Liquid.
    /// Supports multiple animation types and door tiers for the procedural generation system.
    /// Doors are always openable - no keys or button activators required.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Door : MonoBehaviour
    {
        #region Enums
        
        /// <summary>
        /// Defines how the door should animate when opening/closing.
        /// </summary>
        public enum DoorAnimationType
        {
            Slide,
            Rotation,
            SmartRotation
        }

        /// <summary>
        /// Defines the door tier/type for procedural generation compatibility.
        /// Used by the ConnectionSocket system to ensure compatible room connections.
        /// </summary>
        public enum DoorType
        {
            Standard,
            Large,
            Airlock,
            Emergency,
            Maintenance
        }
        
        #endregion

        #region Serialized Fields

        [Header("-- Door Type Configuration --")]
        [Tooltip("The tier/type of this door. Must match the ConnectionSocket type for procedural generation.")]
        [SerializeField] private DoorType _doorType = DoorType.Standard;

        [Header("-- Door Animation Type --")]
        [Tooltip("How should this door open?")]
        [SerializeField] private DoorAnimationType _animationType = DoorAnimationType.Slide;

        [Header("-- Slide Animation Settings --")]
        [Tooltip("Which direction should the door slide when opening?")]
        [SerializeField] private Vector3 _slideDirection = Vector3.up;

        [Tooltip("How far should the door slide (in units)?")]
        [SerializeField] private float _slideDistance = 3f;

        [Header("-- Rotation Animation Settings --")]
        [Tooltip("Which axis should the door rotate around?")]
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;

        [Tooltip("How many degrees should the door rotate when opening?")]
        [SerializeField] private float _rotationAngle = 90f;

        [Header("-- Smart Rotation Settings --")]
        [Tooltip("For Smart Rotation: How much to rotate when opening to the front (positive angle).")]
        [SerializeField] private float _frontRotationAngle = 90f;

        [Tooltip("For Smart Rotation: How much to rotate when opening to the back (negative angle).")]
        [SerializeField] private float _backRotationAngle = -90f;

        [Header("-- Animation Timing --")]
        [Tooltip("How long the opening animation takes (in seconds).")]
        [SerializeField] private float _openingDuration = 1f;

        [Tooltip("How long the closing animation takes (in seconds).")]
        [SerializeField] private float _closingDuration = 1f;

        [Tooltip("Should the door automatically close after opening?")]
        [SerializeField] private bool _autoClose;

        [Tooltip("How long to wait before auto-closing (if enabled).")]
        [SerializeField] private float _autoCloseDelay = 3f;

        [Tooltip("Can the player manually close an open door?")]
        [SerializeField] private bool _allowManualClose = true;

        [Header("-- Audio (Optional) --")]
        [Tooltip("Sound when door opens.")]
        [SerializeField] private AudioClip _openSound;

        [Tooltip("Sound when door closes.")]
        [SerializeField] private AudioClip _closeSound;

        [Header("-- Threat System Integration --")]
        [Tooltip("How much noise does opening this door generate? (Added to threat level)")]
        [SerializeField] private float _noiseGenerated = 5f;

        #endregion

        #region Private Fields

        private bool _isOpen;
        private bool _isAnimating;
        private Vector3 _closedPosition;
        private Vector3 _openPosition;
        private Quaternion _closedRotation;
        private Quaternion _openRotation;
        private AudioSource _audioSource;
        private Camera _playerCamera;
        private Coroutine _animationCoroutine;
        private Coroutine _autoCloseCoroutine;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets whether the door is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Gets whether the door is currently animating.
        /// </summary>
        public bool IsAnimating => _isAnimating;

        /// <summary>
        /// Gets whether the player can manually close this door.
        /// </summary>
        public bool AllowManualClose => _allowManualClose;

        /// <summary>
        /// Gets the door type/tier for procedural generation compatibility.
        /// </summary>
        public DoorType Type => _doorType;

        /// <summary>
        /// Gets the animation type of this door.
        /// </summary>
        public DoorAnimationType AnimationType => _animationType;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            SetupAudioSource();
            FindPlayerCamera();
        }

        private void Start()
        {
            InitializeDoorStates();
            ValidateConfiguration();
        }

        private void OnDrawGizmosSelected()
        {
            if (_animationType == DoorAnimationType.Slide)
            {
                Gizmos.color = Color.green;
                var endPos = transform.position + _slideDirection.normalized * _slideDistance;
                Gizmos.DrawLine(transform.position, endPos);
                Gizmos.DrawCube(endPos, Vector3.one * 0.2f);
            }
            else if (_animationType == DoorAnimationType.Rotation)
            {
                Gizmos.color = Color.blue;
                DrawRotationPreview(_rotationAngle);
            }
            else if (_animationType == DoorAnimationType.SmartRotation)
            {
                Gizmos.color = Color.green;
                DrawRotationPreview(_frontRotationAngle);
                
                Gizmos.color = Color.red;
                DrawRotationPreview(_backRotationAngle);
            }
        }

        #endregion

        #region Initialization

        private void SetupAudioSource()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1.0f;
        }

        private void FindPlayerCamera()
        {
            _playerCamera = Camera.main;
            if (_playerCamera == null)
            {
                var playerCamObj = GameObject.FindGameObjectWithTag("MainCamera");
                if (playerCamObj != null)
                {
                    _playerCamera = playerCamObj.GetComponent<Camera>();
                }
            }
        }

        private void InitializeDoorStates()
        {
            _closedPosition = transform.position;
            _closedRotation = transform.rotation;

            if (_animationType == DoorAnimationType.Slide)
            {
                _openPosition = _closedPosition + _slideDirection.normalized * _slideDistance;
                _openRotation = _closedRotation;
            }
            else if (_animationType == DoorAnimationType.Rotation)
            {
                _openPosition = _closedPosition;
                _openRotation = _closedRotation * Quaternion.AngleAxis(_rotationAngle, _rotationAxis.normalized);
            }
            else
            {
                _openPosition = _closedPosition;
            }
        }

        private void ValidateConfiguration()
        {
            if (_animationType == DoorAnimationType.Slide && _slideDistance <= 0f)
            {
                Debug.LogWarning($"[Door] '{gameObject.name}' has slide distance of 0 or less!");
            }

            if (_animationType == DoorAnimationType.Rotation && _rotationAngle == 0f)
            {
                Debug.LogWarning($"[Door] '{gameObject.name}' has rotation angle of 0!");
            }
            
            if (_animationType == DoorAnimationType.SmartRotation)
            {
                if (_frontRotationAngle == 0f && _backRotationAngle == 0f)
                {
                    Debug.LogWarning($"[Door] '{gameObject.name}' has both front and back rotation angles of 0!");
                }
                if (Mathf.Approximately(_frontRotationAngle, _backRotationAngle))
                {
                    Debug.LogWarning($"[Door] '{gameObject.name}' has same front and back rotation angles - consider using regular Rotation instead!");
                }
            }

            if (_openingDuration <= 0f)
            {
                Debug.LogWarning($"[Door] '{gameObject.name}' has opening duration of 0 or less! Setting to 1 second.");
                _openingDuration = 1f;
            }

            if (_closingDuration <= 0f)
            {
                Debug.LogWarning($"[Door] '{gameObject.name}' has closing duration of 0 or less! Setting to 1 second.");
                _closingDuration = 1f;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to interact with the door (open if closed, close if open and allowed).
        /// Returns true if the action was successful.
        /// </summary>
        public bool Interact()
        {
            if (_isAnimating)
                return false;

            if (_isOpen)
            {
                if (_allowManualClose)
                {
                    CloseDoor();
                    return true;
                }
                return false;
            }
            else
            {
                OpenDoor();
                return true;
            }
        }

        /// <summary>
        /// Opens the door. Always succeeds unless already open or animating.
        /// </summary>
        public void OpenDoor()
        {
            if (_isOpen || _isAnimating)
                return;

            if (_autoCloseCoroutine != null)
            {
                StopCoroutine(_autoCloseCoroutine);
                _autoCloseCoroutine = null;
            }

            if (_animationType == DoorAnimationType.SmartRotation)
            {
                DetermineSmartRotationDirection();
            }

            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            
            _animationCoroutine = StartCoroutine(AnimateDoor(true));

            PlaySound(_openSound);

            NotifyThreatSystem();

            if (GameManager.Instance?.EventManager != null)
            {
                GameManager.Instance.EventManager.Publish("OnDoorOpened", this);
            }
        }

        /// <summary>
        /// Closes the door if manual closing is allowed.
        /// </summary>
        public void CloseDoor()
        {
            if (!_isOpen || _isAnimating)
                return;

            if (!_allowManualClose)
            {
                Debug.Log($"[Door] Cannot close door '{gameObject.name}' - manual closing is disabled.");
                return;
            }

            if (_autoCloseCoroutine != null)
            {
                StopCoroutine(_autoCloseCoroutine);
                _autoCloseCoroutine = null;
            }

            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            
            _animationCoroutine = StartCoroutine(AnimateDoor(false));

            PlaySound(_closeSound);

            if (GameManager.Instance?.EventManager != null)
            {
                GameManager.Instance.EventManager.Publish("OnDoorClosed", this);
            }
        }

        /// <summary>
        /// Forces the door to open immediately without animation.
        /// Useful for procedural generation or special events.
        /// </summary>
        public void ForceOpen()
        {
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            if (_autoCloseCoroutine != null)
                StopCoroutine(_autoCloseCoroutine);

            _isOpen = true;
            _isAnimating = false;
            transform.position = _openPosition;
            transform.rotation = _openRotation;
        }

        /// <summary>
        /// Forces the door to close immediately without animation.
        /// </summary>
        public void ForceClose()
        {
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            if (_autoCloseCoroutine != null)
                StopCoroutine(_autoCloseCoroutine);

            _isOpen = false;
            _isAnimating = false;
            transform.position = _closedPosition;
            transform.rotation = _closedRotation;
        }

        #endregion

        #region Animation

        private IEnumerator AnimateDoor(bool opening)
        {
            _isAnimating = true;
            
            float duration = opening ? _openingDuration : _closingDuration;
            float elapsed = 0f;

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 targetPos = opening ? _openPosition : _closedPosition;
            Quaternion targetRot = opening ? _openRotation : _closedRotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);

                yield return null;
            }

            transform.position = targetPos;
            transform.rotation = targetRot;

            _isOpen = opening;
            _isAnimating = false;
            _animationCoroutine = null;

            if (_autoClose && opening)
            {
                _autoCloseCoroutine = StartCoroutine(AutoCloseAfterDelay());
            }
        }

        private IEnumerator AutoCloseAfterDelay()
        {
            yield return new WaitForSeconds(_autoCloseDelay);
            
            if (_isOpen && !_isAnimating)
            {
                CloseDoor();
            }
            
            _autoCloseCoroutine = null;
        }

        private void DetermineSmartRotationDirection()
        {
            if (_playerCamera == null)
            {
                _openRotation = _closedRotation * Quaternion.AngleAxis(_frontRotationAngle, _rotationAxis.normalized);
                return;
            }

            Vector3 toDoor = (transform.position - _playerCamera.transform.position).normalized;
            Vector3 doorForward = transform.forward;

            float dot = Vector3.Dot(toDoor, doorForward);

            float rotationAngle = dot > 0 ? _frontRotationAngle : _backRotationAngle;
            
            _openRotation = _closedRotation * Quaternion.AngleAxis(rotationAngle, _rotationAxis.normalized);
        }

        #endregion

        #region Audio & Threat

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        private void NotifyThreatSystem()
        {
        }

        #endregion

        #region Gizmos Helpers

        private void DrawRotationPreview(float angle)
        {
            Vector3 center = transform.position;
            Vector3 axis = _rotationAxis.normalized;
            Vector3 perpendicular = Vector3.Cross(axis, Vector3.up);
            if (perpendicular.magnitude < 0.1f)
                perpendicular = Vector3.Cross(axis, Vector3.forward);
            perpendicular = perpendicular.normalized;

            int segments = 20;
            Vector3 prevPoint = center + Quaternion.AngleAxis(0, axis) * perpendicular * 1f;

            for (int i = 1; i <= segments; i++)
            {
                float currentAngle = (angle / segments) * i;
                Vector3 point = center + Quaternion.AngleAxis(currentAngle, axis) * perpendicular * 1f;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }

            Vector3 endPoint = center + Quaternion.AngleAxis(angle, axis) * perpendicular * 1f;
            Gizmos.DrawSphere(endPoint, 0.1f);
        }

        #endregion
    }

    #region Event Data Classes

    /// <summary>
    /// Data structure for noise generation events.
    /// </summary>
    public class NoiseEventData
    {
        public Vector3 Position;
        public float NoiseMagnitude;
        public GameObject Source;
    }

    #endregion
}