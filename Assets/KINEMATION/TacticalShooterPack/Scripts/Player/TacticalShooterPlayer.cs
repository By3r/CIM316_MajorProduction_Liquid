// Copyright (c) 2026 KINEMATION.
// All rights reserved.
//
// Modified for Liquid project: InputManager integration, body rotation, MovementController bridge.

using System.Collections;
using System.Collections.Generic;
using KINEMATION.Shared.KAnimationCore.Runtime.Attributes;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using KINEMATION.KShooterCore.Runtime;
using KINEMATION.KShooterCore.Runtime.Camera;
using KINEMATION.KShooterCore.Runtime.Character;
using KINEMATION.KShooterCore.Runtime.Weapon;
using KINEMATION.TacticalShooterPack.Scripts.Animation;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;

using _Scripts.Core.Managers;
using _Scripts.Systems.Player;

using UnityEngine;
using Random = UnityEngine.Random;

namespace KINEMATION.TacticalShooterPack.Scripts.Player
{
    [AddComponentMenu("KINEMATION/Tactical Shooter Pack/Tactical Shooter Player")]
    public class TacticalShooterPlayer : KShooterCharacter
    {
        [Tab("General")]
        [Header("Inputs")]
        [SerializeField] protected float lookSensitivity = 1f;
        [SerializeField, Range(0f, 1f)] protected float timeScale = 1f;

        [Header("Weapons & Camera")]
        [SerializeField] protected GameObject[] weaponPrefabs;
        [SerializeField] protected FPSCameraAnimator fpsCamera;

        [Tab("Animation")]
        [SerializeField] protected IKMotion aimIkMotion;
        [SerializeField] protected IKMotion fireModeIkMotion;

        [Tab("Sounds")]

        [Header("Actions")]
        [SerializeField] protected AudioClip quickDrawSound;
        [SerializeField] protected AudioClip quickHolsterSound;
        [SerializeField] protected AudioClip jumpSound;
        [SerializeField] protected AudioClip landSound;

        [Header("Movement")]
        [SerializeField] private List<AudioClip> walkSounds;
        [SerializeField] private List<AudioClip> sprintSounds;
        [SerializeField] private float walkDelay = 0.4f;
        [SerializeField] private float sprintDelay = 0.4f;

        protected TacticalWeaponSettings _weaponSettings;

        protected List<TacticalShooterWeapon> _weapons;
        protected int _activeWeaponIndex;

        // Pistol quick draw.
        protected int _quickDrawWeaponIndex;
        protected bool _quickDrawPistol;
        protected bool _isAiming;

        protected Animator _animator;
        protected bool _wantsToSprint;

        protected AudioSource _audioSource;
        protected TacticalProceduralAnimation _tacProceduralAnimation;

        protected float _playback = 0f;
        protected bool _hasActiveAction;

        protected float _leanInput;

        // Liquid: Accumulated body yaw for world-space rotation.
        protected float _bodyYaw;

        // Liquid: Reference to MovementController for reading gait state.
        protected MovementController _movementController;

        #region Public Accessors (Liquid)

        /// <summary>
        /// Public accessor for the camera animator (used by SettingsUI, etc.)
        /// </summary>
        public FPSCameraAnimator FpsCamera => fpsCamera;

        /// <summary>
        /// Public accessor for look sensitivity (used by SettingsUI).
        /// </summary>
        public float LookSensitivity
        {
            get => lookSensitivity;
            set => lookSensitivity = value;
        }

        #endregion

        public void OnActionStarted()
        {
            _hasActiveAction = true;
            _wantsToSprint = false;
        }

        public void OnActionEnded()
        {
            _hasActiveAction = false;
        }

        private void Awake()
        {
            if (fpsCamera == null) fpsCamera = transform.root.GetComponentInChildren<FPSCameraAnimator>();

            // Liquid: Cache MovementController sibling.
            _movementController = GetComponent<MovementController>();
        }

        public override KShooterWeapon GetActiveShooterWeapon()
        {
            return GetActiveWeapon();
        }

        public TacticalShooterWeapon GetActiveWeapon()
        {
            return _weapons[_activeWeaponIndex];
        }

        public TacticalShooterWeapon GetPrimaryWeapon()
        {
            return _quickDrawPistol ? _weapons[_quickDrawWeaponIndex] : GetActiveWeapon();
        }

        private void Start()
        {
            Cursor.visible = false;

            _tacProceduralAnimation = GetComponent<TacticalProceduralAnimation>();
            _audioSource = GetComponent<AudioSource>();
            _weapons = new List<TacticalShooterWeapon>();

            var bones = _tacProceduralAnimation.bones;

            foreach (var prefab in weaponPrefabs)
            {
                var weapon = Instantiate(prefab, bones.ikHandGun).GetComponentInChildren<TacticalShooterWeapon>();
                weapon.Initialize(gameObject, bones.rightHand);
                weapon.HideWeapon();

                _weapons.Add(weapon);
            }

            _animator = GetComponentInChildren<Animator>();
            EquipWeapon();

            // Liquid: Sync initial body yaw with current rotation.
            _bodyYaw = transform.eulerAngles.y;

            // Liquid: Register with PlayerManager.
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.RegisterPlayer(gameObject);
            }
            else
            {
                Debug.LogWarning($"[TacticalShooterPlayer] PlayerManager.Instance is null during Start(). " +
                    $"Registration deferred. Ensure PlayerManager Awake runs before this Start. " +
                    $"(PlayerManager needs DefaultExecutionOrder lower than 0).");
                // Defer registration to next frame when singletons should be ready.
                StartCoroutine(DeferredRegister());
            }
        }

        private void UpdateCurveAnimIntensity(TacCurveAnimIntensity intensity, AnimatorStateName stateName)
        {
            float value = _animator.GetFloat(stateName.hash);
            float target = GetActiveWeapon().IsFiring ? intensity.firing : intensity.standing;
            target = Mathf.Lerp(target, intensity.aiming, _tacProceduralAnimation.aimingWeight);
            _animator.SetFloat(stateName.hash, KMath.FloatInterp(value, target, 8f, Time.deltaTime));
        }

        private float GetDesiredGait()
        {
            float desiredGait = _tacProceduralAnimation.moveInput.magnitude > 0f ? 1f : 0f;

            if (_wantsToSprint)
            {
                if (_tacProceduralAnimation.moveInput.y > 0f)
                {
                    desiredGait = 2f;
                }
                else
                {
                    _wantsToSprint = false;
                }
            }

            return desiredGait;
        }

        protected void PlayWalkSound()
        {
            if (_audioSource == null || walkSounds == null || walkSounds.Count == 0) return;
            _audioSource.PlayOneShot(walkSounds[Random.Range(0, walkSounds.Count - 1)]);
        }

        protected void PlaySprintSound()
        {
            if (_audioSource == null || sprintSounds == null || sprintSounds.Count == 0) return;
            _audioSource.PlayOneShot(sprintSounds[Random.Range(0, sprintSounds.Count - 1)]);
        }

        protected void PlayMovementSounds(float gait, float error = 0.4f)
        {
            if (Mathf.Approximately(gait, 0f) || _animator.GetBool(TacShooterUtility.Animator_IsInAir.hash))
            {
                _playback = 0f;
                return;
            }

            _playback += Time.deltaTime;

            if (gait >= error && gait <= 1f + error)
            {
                if (_playback >= walkDelay)
                {
                    PlayWalkSound();
                    _playback = 0f;
                }
                return;
            }

            if (gait >= 1f + error && gait <= 2f + error)
            {
                if (_playback >= sprintDelay)
                {
                    PlaySprintSound();
                    _playback = 0f;
                }
            }
        }

        private void Update()
        {
            // Liquid: Read all input from InputManager singleton.
            ReadInputFromInputManager();

            float aimingSpeed = GetPrimaryWeapon().AimingSpeed * (_isAiming ? 1f : -1f);
            _tacProceduralAnimation.aimingWeight += Time.deltaTime * aimingSpeed;
            _tacProceduralAnimation.aimingWeight = Mathf.Clamp01(_tacProceduralAnimation.aimingWeight);

            Transform aimPoint = GetPrimaryWeapon().GetAimPoint();
            KTransform aimTransform = KTransform.Identity;
            if (aimPoint != null)
            {
                aimTransform = new KTransform(GetPrimaryWeapon().transform);
                aimTransform =
                    aimTransform.GetRelativeTransform(new KTransform(GetPrimaryWeapon().GetAimPoint()), false);
                aimTransform.position *= -1f;
            }

            _tacProceduralAnimation.UpdateAimPoint(aimTransform);

            UpdateCurveAnimIntensity(_weaponSettings.idleIntensity, TacShooterUtility.Animator_IdleIntensity);
            UpdateCurveAnimIntensity(_weaponSettings.walkIntensity, TacShooterUtility.Animator_WalkIntensity);

            // Liquid: Camera gets pitch from procedural animation, yaw is always 0 (body handles yaw).
            fpsCamera.lookInput.y = _tacProceduralAnimation.pitchInput;
            fpsCamera.lookInput.x = _tacProceduralAnimation.yawInput;

            _tacProceduralAnimation.leanInput = KMath.FloatInterp(_tacProceduralAnimation.leanInput, _leanInput,
                8f, Time.deltaTime);

            float gait = _animator.GetFloat(TacShooterUtility.Animator_Gait.hash);
            gait = KMath.FloatInterp(gait, GetDesiredGait(), 6f, Time.deltaTime);
            _animator.SetFloat(TacShooterUtility.Animator_Gait.hash, gait);

            PlayMovementSounds(gait);
        }

        #region Liquid Input (reads from InputManager)

        /// <summary>
        /// Reads all player input from the Liquid InputManager singleton.
        /// Replaces Kinemation's PlayerInput + SendMessages approach.
        /// </summary>
        private void ReadInputFromInputManager()
        {
            if (InputManager.Instance == null) return;

            // --- Movement input (fed to procedural animation for gait/sway) ---
            _tacProceduralAnimation.moveInput = InputManager.Instance.MoveInput;

            // --- Look input ---
            Vector2 lookDelta = InputManager.Instance.LookInput * lookSensitivity;
            UpdateLookInput(lookDelta);

            // --- Sprint ---
            if (!_hasActiveAction)
            {
                _wantsToSprint = InputManager.Instance.IsSprinting;
            }

            // --- Fire ---
            if (_hasActiveAction)
            {
                if (GetPrimaryWeapon().IsFiring) GetPrimaryWeapon().StopFiring();
            }
            else
            {
                if (InputManager.Instance.FireJustPressed)
                {
                    GetPrimaryWeapon().StartFiring();
                }
                else if (!InputManager.Instance.FirePressed && GetPrimaryWeapon().IsFiring)
                {
                    GetPrimaryWeapon().StopFiring();
                }
            }

            // --- Aim (toggle on press) ---
            if (InputManager.Instance.AimJustPressed)
            {
                OnAim();
            }

            // --- Reload ---
            if (InputManager.Instance.ReloadPressed)
            {
                OnReload();
            }

            // --- Weapon switch (scroll wheel) ---
            float scrollInput = InputManager.Instance.SwitchWeaponInput;
            if (!_hasActiveAction && !_quickDrawPistol && !Mathf.Approximately(scrollInput, 0f))
            {
                if (scrollInput > 0f)
                {
                    EquipNextWeapon();
                }
                else
                {
                    EquipPreviousWeapon();
                }
                GetActiveWeapon().RestoreWeaponVisibility();
            }

            // --- Equip next weapon (F key) ---
            if (InputManager.Instance.EquipNextWeaponPressed)
            {
                OnChangeWeapon();
            }

            // --- Inspect (I key) ---
            if (InputManager.Instance.InspectPressed)
            {
                OnInspect();
            }

            // --- Mag check (M key) ---
            if (InputManager.Instance.MagCheckPressed)
            {
                OnMagCheck();
            }

            // --- Toggle attachment (N key) ---
            if (InputManager.Instance.ToggleAttachmentPressed)
            {
                OnToggleAttachment();
            }

            // --- Quick draw pistol (X key) ---
            if (InputManager.Instance.QuickDrawPistolPressed)
            {
                OnQuickPistolDraw();
            }

            // --- Change fire mode (B key) ---
            if (InputManager.Instance.ChangeFireModePressed)
            {
                OnChangeFireMode();
            }

            // --- Free look (Left Alt) ---
            if (InputManager.Instance.FreeLookPressed)
            {
                OnFreeLook();
            }
        }

        #endregion

        private void OnValidate()
        {
            Time.timeScale = timeScale;
        }

        protected void EquipWeapon(bool playDraw = true)
        {
            Transform weaponTransform = GetActiveWeapon().GetWeaponRoot();
            weaponTransform.parent = _tacProceduralAnimation.bones.ikHandGun;
            weaponTransform.localPosition = Vector3.zero;
            weaponTransform.localRotation = Quaternion.identity;

            _weaponSettings = GetActiveWeapon().tacWeaponSettings;
            _tacProceduralAnimation.UpdateAnimationSettings(_weaponSettings);

            GetActiveWeapon().Draw(playDraw, true, playDraw ? 0.03f : -1f);
        }

        protected void EquipNextWeapon()
        {
            GetActiveWeapon().HideWeapon();

            _activeWeaponIndex++;
            _activeWeaponIndex = _activeWeaponIndex > _weapons.Count - 1 ? 0 : _activeWeaponIndex;

            EquipWeapon(false);
        }

        protected void EquipPreviousWeapon()
        {
            GetActiveWeapon().HideWeapon();

            _activeWeaponIndex--;
            _activeWeaponIndex = _activeWeaponIndex < 0 ? _weapons.Count - 1 : _activeWeaponIndex;

            EquipWeapon(false);
        }

        protected void ChangeWeapon()
        {
            GetActiveWeapon().HideWeapon();

            _activeWeaponIndex++;
            _activeWeaponIndex = _activeWeaponIndex > _weapons.Count - 1 ? 0 : _activeWeaponIndex;

            EquipWeapon();
        }

        public void OnChangeWeapon()
        {
            if (_hasActiveAction || _quickDrawPistol) return;
            float delay = GetActiveWeapon().Holster(true);
            Invoke(nameof(ChangeWeapon), delay);
        }

        public void OnChangeFireMode()
        {
            var prevFireMode = GetActiveWeapon().FireMode;
            GetActiveWeapon().ChangeFireMode();

            if(GetActiveWeapon().FireMode != prevFireMode) _tacProceduralAnimation.PlayIkMotion(fireModeIkMotion);
        }

        public void OnReload()
        {
            if (_hasActiveAction || _quickDrawPistol) return;
            GetActiveWeapon().Reload();
        }

        public void OnMagCheck()
        {
            if (_hasActiveAction || _quickDrawPistol) return;
            GetActiveWeapon().DoMagCheck();
        }

        public void OnInspect()
        {
            if (_hasActiveAction || _quickDrawPistol) return;
            GetActiveWeapon().Inspect();
        }

        public void OnToggleAttachment()
        {
            if (_hasActiveAction || _quickDrawPistol) return;
            GetActiveWeapon().ToggleAttachment();
        }

        public void OnQuickPistolDraw()
        {
            if (_hasActiveAction && !_quickDrawPistol) return;

            if (!_quickDrawPistol)
            {
                _quickDrawWeaponIndex = -1;
                for (int i = _activeWeaponIndex + 1; i < _weapons.Count; i++)
                {
                    if (!_weapons[i].IsOneHanded) continue;
                    _quickDrawWeaponIndex = i;
                    break;
                }

                if (_quickDrawWeaponIndex < 0)
                {
                    for (int i = 0; i < _activeWeaponIndex; i++)
                    {
                        if (!_weapons[i].IsOneHanded) continue;
                        _quickDrawWeaponIndex = i;
                        break;
                    }
                }

                if (_quickDrawWeaponIndex < 0)
                {
                    // No one-handed weapon in the inventory.
                    Debug.LogWarning("Couldn't find a one-handed weapon.");
                    return;
                }

                _quickDrawPistol = true;

                // Equip the gun without playing the animation.
                GetPrimaryWeapon().Draw(false, false, 0.2f);

                // Update the right-handed pose.
                _tacProceduralAnimation.UpdateRightHandPose(GetPrimaryWeapon().gunRightHandPose);

                // Parent the weapon to the main gun bone.
                Transform weaponTransform = GetPrimaryWeapon().GetWeaponRoot();
                weaponTransform.parent = _tacProceduralAnimation.bones.ikHandGun.parent;
                weaponTransform.localPosition = Vector3.zero;
                weaponTransform.localRotation = Quaternion.identity;
            }
            else
            {
                GetPrimaryWeapon().Holster(false, 0.35f);
                GetActiveWeapon().Draw(false);
                _quickDrawPistol = false;
                _quickDrawWeaponIndex = -1;
            }

            if(_audioSource != null) _audioSource.PlayOneShot(_quickDrawPistol ? quickDrawSound : quickHolsterSound);
            _animator.SetBool(TacShooterUtility.Animator_UseQuickDraw.hash, _quickDrawPistol);
        }

        public void OnAim()
        {
            _isAiming = !_isAiming;
            GetPrimaryWeapon().OnAiming(_isAiming);
            _tacProceduralAnimation.PlayIkMotion(aimIkMotion);

            float aimFov = GetPrimaryWeapon().tacWeaponSettings.aimFov;
            fpsCamera.SetTargetFOV(_isAiming ? aimFov : fpsCamera.BaseFOV, 6f);
        }

        public void OnFreeLook()
        {
            fpsCamera.ToggleFreeLook();
        }

        /// <summary>
        /// Updates look input: body yaw rotation + pitch for procedural animation.
        /// Liquid modification: Body rotates with yaw, spine yaw stays at 0.
        /// </summary>
        private void UpdateLookInput(Vector2 delta)
        {
            if (fpsCamera.UseFreeLook)
            {
                fpsCamera.AddFreeLookInput(delta);
                return;
            }

            _tacProceduralAnimation.deltaLookInput = delta;

            // Pitch: Procedural animation distributes across spine bones.
            _tacProceduralAnimation.pitchInput -= delta.y;
            _tacProceduralAnimation.pitchInput = Mathf.Clamp(_tacProceduralAnimation.pitchInput, -90f, 90f);

            // Yaw: Body rotates in world space. Spine yaw stays at 0 (body faces where you look).
            _bodyYaw += delta.x;
            transform.rotation = Quaternion.Euler(0f, _bodyYaw, 0f);
            _tacProceduralAnimation.yawInput = 0f;
        }

        /// <summary>
        /// Fallback: waits one frame for singletons to initialize, then registers.
        /// </summary>
        private IEnumerator DeferredRegister()
        {
            yield return null;
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.RegisterPlayer(gameObject);
                Debug.Log("[TacticalShooterPlayer] Deferred registration successful.");
            }
            else
            {
                Debug.LogError("[TacticalShooterPlayer] PlayerManager.Instance is STILL null after deferral. Player will not be registered.");
            }
        }
    }
}
