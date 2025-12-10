using System.Collections.Generic;
using _Scripts.Core.Managers;
using UnityEngine;

namespace MainMenu.UI
{
    public class DiegeticMainMenuController : MonoBehaviour
    {
        #region Variables
        [Header("Transforms")]
        [Tooltip("Pivot transform of the flashlight that should rotate toward menu buttons.")]
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

        private int _currentIndex;
        private float _lastNavigateTime;
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

            SetSelectedIndex(0, force: true);
        }

        private void Update()
        {
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

            if (InputManager.Instance.SubmitPressed)
            {
                DiegeticMenuButton currentButton = menuButtons[_currentIndex];
                currentButton.Activate();
            }
        }

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