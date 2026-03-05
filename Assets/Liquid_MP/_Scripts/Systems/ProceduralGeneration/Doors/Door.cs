using System.Collections;
using _Scripts.Core.Managers;
using UnityEngine;

namespace _Scripts.Systems.ProceduralGeneration.Doors
{
    [RequireComponent(typeof(AudioSource))]
    public class Door : MonoBehaviour
    {
        #region Enums
        
        public enum DoorAnimationType
        {
            Slide,
            Rotation,
            SmartRotation
        }

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

        [SerializeField] private DoorType _doorType = DoorType.Standard;
        [SerializeField] private DoorAnimationType _animationType = DoorAnimationType.Slide;
        [SerializeField] private Vector3 _slideDirection = Vector3.up;
        [SerializeField] private float _slideDistance = 3f;
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;
        [SerializeField] private float _rotationAngle = 90f;
        [SerializeField] private float _frontRotationAngle = 90f;
        [SerializeField] private float _backRotationAngle = -90f;
        [SerializeField] private float _openingDuration = 1f;
        [SerializeField] private float _closingDuration = 1f;
        [SerializeField] private bool _autoClose;
        [SerializeField] private float _autoCloseDelay = 3f;
        [SerializeField] private bool _allowManualClose = true;
        [SerializeField] private AudioClip _openSound;
        [SerializeField] private AudioClip _closeSound;
        [SerializeField] private float _noiseGenerated = 5f;
        [SerializeField] private bool _showGizmos = false;

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

        public bool IsOpen => _isOpen;

        public bool IsAnimating => _isAnimating;

        public bool AllowManualClose => _allowManualClose;

        public DoorType Type => _doorType;

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
            if (!_showGizmos) return;

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

    public class NoiseEventData
    {
        public Vector3 Position;
        public float NoiseMagnitude;
        public GameObject Source;
    }

    #endregion
}