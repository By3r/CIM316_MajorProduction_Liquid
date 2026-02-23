using _Scripts.Core.Managers;
using _Scripts.Systems.Player;
using KINEMATION.KShooterCore.Runtime.Camera;
using KINEMATION.TacticalShooterPack.Scripts.Animation;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using Liquid.Audio;
using UnityEngine;

namespace _Scripts.Systems.DebugConsole.Commands
{
    /// <summary>
    /// Diagnostic command that validates the entire player stack at runtime.
    /// Checks every component, singleton, and connection that must be working.
    /// </summary>
    public static class PlayerDiagCommand
    {
        [DebugCommand("player diag", "Runs a full diagnostic on the player stack (components, singletons, connections).", "player diag")]
        public static string Diagnose(string[] args)
        {
            var sb = new System.Text.StringBuilder();
            int pass = 0;
            int fail = 0;
            int warn = 0;

            sb.AppendLine("<b>=== PLAYER STACK DIAGNOSTIC ===</b>");
            sb.AppendLine();

            // ─────────────────────────────────────
            // 1. SINGLETONS
            // ─────────────────────────────────────
            sb.AppendLine("<b>[ Singletons ]</b>");

            CheckDetail(sb, "PlayerManager.Instance", PlayerManager.Instance != null, ref pass, ref fail,
                failHint: "No PlayerManager in scene. Add a GameObject with PlayerManager component.");
            CheckDetail(sb, "InputManager.Instance", InputManager.Instance != null, ref pass, ref fail,
                failHint: "No InputManager in scene. Add a GameObject with InputManager component.");
            CheckDetail(sb, "NoiseManager.Instance", NoiseManager.Instance != null, ref pass, ref fail,
                failHint: "No NoiseManager in scene. Enemy hearing won't work.");
            CheckDetail(sb, "GameManager.Instance", GameManager.Instance != null, ref pass, ref fail,
                failHint: "No GameManager in scene. Events, floor transitions won't fire.");

            sb.AppendLine();

            // ─────────────────────────────────────
            // 2. PLAYER GAMEOBJECT — detailed search if missing
            // ─────────────────────────────────────
            sb.AppendLine("<b>[ Player GameObject ]</b>");

            GameObject player = PlayerManager.Instance != null ? PlayerManager.Instance.CurrentPlayer : null;

            if (player == null)
            {
                fail++;
                sb.AppendLine("  <color=red>\u2717 CurrentPlayer is NULL</color>");
                sb.AppendLine();

                // --- Why? Let's investigate ---
                sb.AppendLine("  <b>Investigating why...</b>");

                // Check if PlayerManager has a prefab assigned
                if (PlayerManager.Instance != null)
                {
                    // Use reflection-free approach: try to spawn and see what happens
                    sb.AppendLine($"  PlayerManager exists on: \"{PlayerManager.Instance.gameObject.name}\"");
                    sb.AppendLine($"  PlayerManager.IsPlayerFrozen: {PlayerManager.Instance.IsPlayerFrozen}");
                }
                else
                {
                    sb.AppendLine("  PlayerManager.Instance is null — cannot check prefab.");
                }

                // Search scene for any TacticalShooterPlayer that didn't register
                var allTSP = Object.FindObjectsOfType<TacticalShooterPlayer>(true);
                if (allTSP.Length > 0)
                {
                    sb.AppendLine($"  <color=yellow>Found {allTSP.Length} TacticalShooterPlayer(s) in scene that didn't register:</color>");
                    foreach (var t in allTSP)
                    {
                        sb.AppendLine($"    - \"{t.gameObject.name}\" active={t.gameObject.activeInHierarchy} enabled={t.enabled}");
                        if (!t.gameObject.activeInHierarchy)
                            sb.AppendLine("      <color=yellow>\u2192 GameObject is inactive — Start() never ran, so RegisterPlayer() never called.</color>");
                        else if (!t.enabled)
                            sb.AppendLine("      <color=yellow>\u2192 Component is disabled — Start() may not have run.</color>");
                        else
                            sb.AppendLine("      <color=yellow>\u2192 Active & enabled but not registered. Was PlayerManager.Instance null during Start()?</color>");
                    }
                }
                else
                {
                    sb.AppendLine("  <color=red>No TacticalShooterPlayer found anywhere in the scene (including inactive).</color>");
                }

                // Search for old-style player objects by common names
                string[] playerNames = { "Player", "Player(Clone)", "LiquidPlayer", "LiquidPlayer(Clone)", "TacticalShooterCharacter", "TacticalShooterCharacter(Clone)" };
                bool foundAny = false;
                foreach (var name in playerNames)
                {
                    var obj = GameObject.Find(name);
                    if (obj != null)
                    {
                        if (!foundAny) { sb.AppendLine("  <b>Found GameObjects by name:</b>"); foundAny = true; }
                        var hasTSP = obj.GetComponent<TacticalShooterPlayer>() != null;
                        var hasMC = obj.GetComponent<MovementController>() != null;
                        var hasCC = obj.GetComponent<CharacterController>() != null;
                        sb.AppendLine($"    - \"{name}\"  TSP={hasTSP}  MC={hasMC}  CC={hasCC}");
                    }
                }
                if (!foundAny)
                {
                    sb.AppendLine("  No common player-named GameObjects found in scene.");
                }

                // Check if CharacterController exists without TSP (wrong prefab?)
                var allCC = Object.FindObjectsOfType<CharacterController>(true);
                bool foundPlayerLike = false;
                foreach (var c in allCC)
                {
                    if (c.GetComponent<TacticalShooterPlayer>() == null && c.GetComponent<MovementController>() != null)
                    {
                        if (!foundPlayerLike) { sb.AppendLine("  <b>Possible misconfigured players (has CC+MC but no TSP):</b>"); foundPlayerLike = true; }
                        sb.AppendLine($"    - \"{c.gameObject.name}\"");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("  <b>Likely causes:</b>");
                sb.AppendLine("  1. Player prefab not assigned on PlayerManager (check Inspector).");
                sb.AppendLine("  2. Player prefab missing TacticalShooterPlayer component.");
                sb.AppendLine("  3. Player exists but is inactive (so Start() never ran).");
                sb.AppendLine("  4. PlayerManager.Instance was null when TacticalShooterPlayer.Start() tried to register.");
                sb.AppendLine("     (PlayerManager has [DefaultExecutionOrder(-100)] — did its Awake run first?).");
                sb.AppendLine("  5. Player was never spawned — call PlayerManager.GetOrSpawnPlayer() from your floor generator or scene bootstrap.");

                sb.AppendLine();
                AppendSummary(sb, pass, fail, warn);
                return sb.ToString();
            }

            // Player found — show info
            pass++;
            sb.AppendLine($"  <color=green>\u2713</color> CurrentPlayer exists");
            sb.AppendLine($"  Name: \"{player.name}\"");
            sb.AppendLine($"  Position: ({player.transform.position.x:F2}, {player.transform.position.y:F2}, {player.transform.position.z:F2})");
            sb.AppendLine($"  Rotation: ({player.transform.eulerAngles.x:F1}, {player.transform.eulerAngles.y:F1}, {player.transform.eulerAngles.z:F1})");
            sb.AppendLine($"  Active: {player.activeInHierarchy}  Layer: {LayerMask.LayerToName(player.layer)} ({player.layer})");

            sb.AppendLine();

            // ─────────────────────────────────────
            // 3. CORE COMPONENTS
            // ─────────────────────────────────────
            sb.AppendLine("<b>[ Core Components ]</b>");

            var tsp = player.GetComponent<TacticalShooterPlayer>();
            CheckDetail(sb, "TacticalShooterPlayer", tsp != null, ref pass, ref fail,
                failHint: "Add TacticalShooterPlayer to the player prefab root.");
            if (tsp != null)
            {
                CheckDetail(sb, "  .enabled", tsp.enabled, ref pass, ref fail,
                    failHint: "Component is disabled — no input or animation processing.");
                sb.AppendLine($"  LookSensitivity: {tsp.LookSensitivity:F2}");
            }

            var mc = player.GetComponent<MovementController>();
            CheckDetail(sb, "MovementController", mc != null, ref pass, ref fail,
                failHint: "Add MovementController to the player prefab root. Without it, player can't move.");
            if (mc != null)
            {
                CheckDetail(sb, "  .enabled", mc.enabled, ref pass, ref fail,
                    failHint: "Component is disabled — movement frozen.");
            }

            var cc = player.GetComponent<CharacterController>();
            CheckDetail(sb, "CharacterController", cc != null, ref pass, ref fail,
                failHint: "Add CharacterController to the player prefab root. Without it, no physics collision/gravity.");
            if (cc != null)
            {
                CheckDetail(sb, "  .enabled", cc.enabled, ref pass, ref fail,
                    failHint: "Disabled — no collision. Noclip on? Or player frozen?");
                sb.AppendLine($"  Height: {cc.height:F2}  Radius: {cc.radius:F2}  Center: ({cc.center.x:F2}, {cc.center.y:F2}, {cc.center.z:F2})");
                if (cc.height < 0.5f || cc.radius < 0.1f)
                {
                    sb.AppendLine("  <color=yellow>\u26A0 CharacterController dimensions look too small. Recommended: height=2, radius=0.5.</color>");
                    warn++;
                }
            }

            var ic = player.GetComponent<InteractionController>();
            CheckDetail(sb, "InteractionController", ic != null, ref pass, ref fail,
                failHint: "Add InteractionController to the player prefab. Without it, can't interact with doors/items.");
            if (ic != null)
            {
                CheckDetail(sb, "  .enabled", ic.enabled, ref pass, ref fail,
                    failHint: "Component is disabled — interactions won't work.");
            }

            var animator = player.GetComponentInChildren<Animator>();
            CheckDetail(sb, "Animator (in children)", animator != null, ref pass, ref fail,
                failHint: "No Animator found in player hierarchy. Kinemation animations won't play.");
            if (animator != null)
            {
                CheckDetail(sb, "  .runtimeAnimatorController", animator.runtimeAnimatorController != null, ref pass, ref fail,
                    failHint: "Animator has no controller assigned. Animations won't play.");
                sb.AppendLine($"  Animator on: \"{animator.gameObject.name}\"  UpdateMode: {animator.updateMode}");
            }

            var audioSrc = player.GetComponent<AudioSource>();
            if (audioSrc != null)
            {
                sb.AppendLine($"  <color=green>\u2713</color> AudioSource");
                pass++;
            }
            else
            {
                sb.AppendLine("  <color=yellow>\u26A0</color> AudioSource — not present. Weapon/movement sounds from TacticalShooterPlayer won't play.");
                warn++;
            }

            sb.AppendLine();

            // ─────────────────────────────────────
            // 4. KINEMATION ANIMATION STACK
            // ─────────────────────────────────────
            sb.AppendLine("<b>[ Kinemation Animation ]</b>");

            var tacAnim = player.GetComponent<TacticalProceduralAnimation>();
            CheckDetail(sb, "TacticalProceduralAnimation", tacAnim != null, ref pass, ref fail,
                failHint: "Missing TacticalProceduralAnimation. Weapon sway, recoil, ADS won't work.");
            if (tacAnim != null)
            {
                bool bonesOk = tacAnim.bones.ikHandGun != null;
                CheckDetail(sb, "  bones.ikHandGun", bonesOk, ref pass, ref fail,
                    failHint: "IK hand gun bone not assigned. Weapons can't attach. Check TacticalProceduralAnimation inspector.");
                if (tacAnim.bones.rightHand != null)
                    sb.AppendLine($"  bones.rightHand: \"{tacAnim.bones.rightHand.name}\"");
                else
                {
                    sb.AppendLine("  <color=red>  bones.rightHand: NULL — weapon init will fail.</color>");
                    fail++;
                }
                sb.AppendLine($"  pitchInput: {tacAnim.pitchInput:F1}  yawInput: {tacAnim.yawInput:F1}  aimingWeight: {tacAnim.aimingWeight:F2}");
                sb.AppendLine($"  moveInput: ({tacAnim.moveInput.x:F2}, {tacAnim.moveInput.y:F2})");
            }

            sb.AppendLine();

            // ─────────────────────────────────────
            // 5. CAMERA
            // ─────────────────────────────────────
            sb.AppendLine("<b>[ Camera ]</b>");

            FPSCameraAnimator fpsCam = tsp != null ? tsp.FpsCamera : null;
            CheckDetail(sb, "FPSCameraAnimator (via TSP.FpsCamera)", fpsCam != null, ref pass, ref fail,
                failHint: "fpsCamera field not assigned on TacticalShooterPlayer. Check Inspector. Camera shake/ADS FOV won't work.");
            if (fpsCam != null)
            {
                sb.AppendLine($"  BaseFOV: {fpsCam.BaseFOV:F1}  FreeLook: {fpsCam.UseFreeLook}");
                sb.AppendLine($"  Attached to: \"{fpsCam.gameObject.name}\"");
            }

            Camera cam = player.GetComponentInChildren<Camera>();
            CheckDetail(sb, "Camera component (in children)", cam != null, ref pass, ref fail,
                failHint: "No Camera found in player children. Nothing will render from the player's viewpoint.");
            if (cam != null)
            {
                sb.AppendLine($"  FOV: {cam.fieldOfView:F1}  Near: {cam.nearClipPlane:F3}  Far: {cam.farClipPlane:F0}");
                sb.AppendLine($"  Tag: {cam.tag}  Depth: {cam.depth:F0}  Culling: {cam.cullingMask}");
                sb.AppendLine($"  Camera on: \"{cam.gameObject.name}\"");
                if (cam.tag != "MainCamera")
                {
                    sb.AppendLine("  <color=yellow>\u26A0 Camera tag is not 'MainCamera'. Some systems may not find it via Camera.main.</color>");
                    warn++;
                }
            }

            sb.AppendLine();

            // ─────────────────────────────────────
            // 6. WEAPONS
            // ─────────────────────────────────────
            sb.AppendLine("<b>[ Weapons ]</b>");

            if (tsp != null)
            {
                TacticalShooterWeapon activeWeapon = null;
                string weaponError = null;
                try { activeWeapon = tsp.GetActiveWeapon(); }
                catch (System.Exception e) { weaponError = e.Message; }

                if (activeWeapon != null)
                {
                    pass++;
                    sb.AppendLine($"  <color=green>\u2713</color> Active weapon: \"{activeWeapon.name}\"");
                    sb.AppendLine($"  IsFiring: {activeWeapon.IsFiring}  FireMode: {activeWeapon.FireMode}  IsOneHanded: {activeWeapon.IsOneHanded}");
                    sb.AppendLine($"  Weapon root: \"{activeWeapon.GetWeaponRoot()?.name ?? "NULL"}\"");

                    var settings = activeWeapon.tacWeaponSettings;
                    CheckDetail(sb, "  TacticalWeaponSettings", settings != null, ref pass, ref fail,
                        failHint: "Weapon has no TacticalWeaponSettings assigned. Recoil/ADS/animations won't work.");
                    if (settings != null)
                    {
                        sb.AppendLine($"  AimFOV: {settings.aimFov:F1}  AimSpeed: {settings.aimingSpeed:F1}  OneHanded: {settings.isOneHanded}");
                    }
                }
                else
                {
                    fail++;
                    sb.AppendLine($"  <color=red>\u2717 No active weapon.</color>");
                    if (weaponError != null)
                        sb.AppendLine($"  Exception: {weaponError}");
                    sb.AppendLine("  Check that weaponPrefabs array is assigned on TacticalShooterPlayer.");
                    sb.AppendLine("  Weapons are instantiated during Start() — if it threw, no weapons exist.");
                }

                // Count all weapons in hierarchy
                var allWeapons = player.GetComponentsInChildren<TacticalShooterWeapon>(true);
                sb.AppendLine($"  Total weapons in hierarchy: {allWeapons.Length}");
                for (int i = 0; i < allWeapons.Length; i++)
                {
                    var w = allWeapons[i];
                    string activeMarker = w == activeWeapon ? " <color=green>[ACTIVE]</color>" : "";
                    sb.AppendLine($"    [{i}] \"{w.name}\" visible={w.gameObject.activeInHierarchy}{activeMarker}");
                }
            }
            else
            {
                Fail(sb, "Cannot check weapons — TacticalShooterPlayer missing.", ref fail);
            }

            sb.AppendLine();

            // ─────────────────────────────────────
            // 7. MOVEMENT STATE
            // ─────────────────────────────────────
            sb.AppendLine("<b>[ Movement State ]</b>");

            if (mc != null)
            {
                sb.AppendLine($"  Grounded: {mc.IsGrounded}  Sprinting: {mc.IsSprinting}  Crouching: {mc.IsCrouching}  Jumping: {mc.IsJumping}");
                sb.AppendLine($"  Speed: {mc.CurrentSpeed:F2} / {mc.MaxSpeed:F2}  WalkToggle: {mc.IsWalkingToggled}");
                sb.AppendLine($"  Velocity: ({mc.Velocity.x:F2}, {mc.Velocity.y:F2}, {mc.Velocity.z:F2})");

                if (!mc.IsGrounded && mc.Velocity.y < -15f)
                {
                    sb.AppendLine("  <color=yellow>\u26A0 Falling fast (velocity.y < -15). Player may be outside the level.</color>");
                    warn++;
                }
            }
            else
            {
                sb.AppendLine("  <color=red>MovementController missing — cannot read movement state.</color>");
            }

            sb.AppendLine();

            // ─────────────────────────────────────
            // 8. GROUND CHECK
            // ─────────────────────────────────────
            sb.AppendLine("<b>[ Ground Check ]</b>");

            Transform groundCheck = player.transform.Find("GroundCheck");
            CheckDetail(sb, "GroundCheck child transform", groundCheck != null, ref pass, ref fail,
                failHint: "Create an empty child named 'GroundCheck' at the player's feet (localPos ~(0, 0, 0)). MovementController uses it for ground detection.");
            if (groundCheck != null)
            {
                sb.AppendLine($"  Local pos: ({groundCheck.localPosition.x:F2}, {groundCheck.localPosition.y:F2}, {groundCheck.localPosition.z:F2})");
                if (groundCheck.localPosition.y > 0.5f)
                {
                    sb.AppendLine("  <color=yellow>\u26A0 GroundCheck Y > 0.5. Should be near 0 (feet level) for accurate grounding.</color>");
                    warn++;
                }
            }

            sb.AppendLine();

            // ─────────────────────────────────────
            // 9. INPUT STATE (live snapshot)
            // ─────────────────────────────────────
            sb.AppendLine("<b>[ Input (live snapshot) ]</b>");

            if (InputManager.Instance != null)
            {
                var move = InputManager.Instance.MoveInput;
                var look = InputManager.Instance.LookInput;
                sb.AppendLine($"  Move: ({move.x:F2}, {move.y:F2})  Look: ({look.x:F2}, {look.y:F2})");
                sb.AppendLine($"  Sprint: {InputManager.Instance.IsSprinting}  Fire: {InputManager.Instance.FirePressed}  Aim: {InputManager.Instance.AimJustPressed}  Reload: {InputManager.Instance.ReloadPressed}");
                sb.AppendLine($"  Jump: {InputManager.Instance.JumpPressed}  Crouch: {InputManager.Instance.CrouchPressed}  Interact: {InputManager.Instance.InteractPressed}");
                sb.AppendLine($"  WeaponScroll: {InputManager.Instance.SwitchWeaponInput:F2}  WalkToggle: {InputManager.Instance.WalkTogglePressed}");
            }
            else
            {
                sb.AppendLine("  <color=red>InputManager missing — all input dead.</color>");
            }

            sb.AppendLine();

            // ─────────────────────────────────────
            // 10. PLAYER MANAGER STATE
            // ─────────────────────────────────────
            sb.AppendLine("<b>[ PlayerManager State ]</b>");

            if (PlayerManager.Instance != null)
            {
                sb.AppendLine($"  Frozen: {PlayerManager.Instance.IsPlayerFrozen}");
                if (PlayerManager.Instance.IsPlayerFrozen)
                {
                    sb.AppendLine("  <color=yellow>\u26A0 Player is frozen. Movement/input disabled. Waiting for floor generation to complete?</color>");
                    warn++;
                }
                bool playerMatch = PlayerManager.Instance.CurrentPlayer == player;
                CheckDetail(sb, "  Registered player matches", playerMatch, ref pass, ref fail,
                    failHint: "PlayerManager.CurrentPlayer doesn't match the player we found. Registration issue.");
            }

            sb.AppendLine();

            // ─────────────────────────────────────
            // 11. OPTIONAL COMPONENTS
            // ─────────────────────────────────────
            sb.AppendLine("<b>[ Optional ]</b>");

            var boots = player.GetComponent<Liquid.Player.Equipment.NeutronicBoots>();
            if (boots != null)
            {
                sb.AppendLine($"  NeutronicBoots: present  enabled={boots.enabled}");
                sb.AppendLine($"    OverrideMovement: {boots.ShouldOverrideMovement}  PreventJump: {boots.ShouldPreventJump}");
            }
            else
            {
                sb.AppendLine("  NeutronicBoots: not present (OK — only needed if boots are equipped).");
                warn++;
            }

            // Check for leftover components from old system
            sb.AppendLine();
            sb.AppendLine("<b>[ Cleanup Check ]</b>");
            bool cleanupOk = true;

            // Check for Unity PlayerInput component (by type name to avoid hard dependency)
            foreach (var mono in player.GetComponents<MonoBehaviour>())
            {
                if (mono != null && mono.GetType().Name == "PlayerInput")
                {
                    sb.AppendLine("  <color=yellow>\u26A0 PlayerInput component found — should be REMOVED. Liquid uses InputManager, not PlayerInput+SendMessages.</color>");
                    warn++;
                    cleanupOk = false;
                    break;
                }
            }

            // Check for any MonoBehaviours with "PlayerController" in the type name (old script lingering on prefab)
            foreach (var mono in player.GetComponents<MonoBehaviour>())
            {
                if (mono == null) continue;
                string typeName = mono.GetType().Name;
                if (typeName == "PlayerController" || typeName == "CameraController" || typeName == "CameraEffectsController" || typeName == "PlayerMoveWASD")
                {
                    sb.AppendLine($"  <color=red>\u2717 Old deleted script '{typeName}' still on player! Remove it from the prefab.</color>");
                    fail++;
                    cleanupOk = false;
                }
            }

            // Check for missing script references (shows as null MonoBehaviour)
            foreach (var mono in player.GetComponents<MonoBehaviour>())
            {
                if (mono == null)
                {
                    sb.AppendLine("  <color=yellow>\u26A0 Missing script reference detected (null MonoBehaviour). Remove it from Inspector.</color>");
                    warn++;
                    cleanupOk = false;
                    break; // Only report once
                }
            }

            if (cleanupOk)
            {
                sb.AppendLine("  <color=green>\u2713</color> No leftover old scripts or missing references.");
                pass++;
            }

            sb.AppendLine();

            // ─────────────────────────────────────
            // SUMMARY
            // ─────────────────────────────────────
            AppendSummary(sb, pass, fail, warn);

            return sb.ToString();
        }

        #region Helpers

        private static void CheckDetail(System.Text.StringBuilder sb, string label, bool condition,
            ref int pass, ref int fail, string failHint = null, bool warnOnFail = false, string warnMsg = null)
        {
            if (condition)
            {
                sb.AppendLine($"  <color=green>\u2713</color> {label}");
                pass++;
            }
            else if (warnOnFail)
            {
                sb.AppendLine($"  <color=yellow>\u26A0</color> {label}");
                if (!string.IsNullOrEmpty(warnMsg))
                    sb.AppendLine($"    {warnMsg}");
                // Warnings don't increment pass or fail — tracked via inline warn++ at call sites.
            }
            else
            {
                sb.AppendLine($"  <color=red>\u2717</color> {label}");
                if (!string.IsNullOrEmpty(failHint))
                    sb.AppendLine($"    <color=red>\u2192 {failHint}</color>");
                fail++;
            }
        }

        private static void Fail(System.Text.StringBuilder sb, string label, ref int fail)
        {
            sb.AppendLine($"  <color=red>\u2717</color> {label}");
            fail++;
        }

        private static void AppendSummary(System.Text.StringBuilder sb, int pass, int fail, int warn)
        {
            sb.AppendLine("<b>=== SUMMARY ===</b>");
            string color = fail > 0 ? "red" : "green";
            sb.AppendLine($"<color={color}>{pass} passed, {fail} failed, {warn} warnings</color>");

            if (fail == 0 && warn == 0)
            {
                sb.AppendLine("<color=green>Player stack is fully healthy!</color>");
            }
            else if (fail == 0)
            {
                sb.AppendLine("<color=green>Player stack is functional.</color> Warnings above are non-critical.");
            }
            else
            {
                sb.AppendLine("<color=red>Fix the failures above before testing gameplay.</color>");
            }
        }

        #endregion
    }
}
