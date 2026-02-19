using System.Text;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Inventory.Pickups;
using _Scripts.Systems.Player;
using UnityEngine;

namespace _Scripts.Systems.DebugConsole.Commands
{
    /// <summary>
    /// Debug commands for diagnosing the InteractionController and pickup system.
    /// </summary>
    public static class InteractionCommands
    {
        [DebugCommand("interact diag", "Diagnoses InteractionController, pickup detection, and inventory state.", "interact diag")]
        public static string InteractDiag(string[] args)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<color=yellow>=== Interaction / Pickup Diagnostics ===</color>");

            // --- Find InteractionController ---
            var ic = Object.FindFirstObjectByType<InteractionController>();
            if (ic == null)
            {
                sb.AppendLine("<color=red>InteractionController NOT FOUND in scene!</color>");
                sb.AppendLine("  It should be a component on the player GameObject.");

                var pm = PlayerManager.Instance;
                if (pm != null && pm.CurrentPlayer != null)
                {
                    sb.AppendLine($"  Player '{pm.CurrentPlayer.name}' components:");
                    foreach (var comp in pm.CurrentPlayer.GetComponents<Component>())
                    {
                        if (comp != null) sb.AppendLine($"    - {comp.GetType().Name}");
                    }
                }
                return sb.ToString();
            }

            sb.AppendLine($"  Found on: '{ic.gameObject.name}'");
            sb.AppendLine($"  Enabled: {(ic.enabled ? "<color=green>YES</color>" : "<color=red>NO</color>")}");
            sb.AppendLine($"  GameObject active: {(ic.gameObject.activeInHierarchy ? "<color=green>YES</color>" : "<color=red>NO</color>")}");

            // --- Camera ---
            var cam = ic.DiagCamera;
            if (cam != null)
            {
                sb.AppendLine($"  Camera: <color=green>{cam.name}</color> (enabled={cam.enabled})");
            }
            else
            {
                sb.AppendLine("  Camera: <color=red>NULL — raycasts will fail!</color>");
            }

            // --- Interaction settings ---
            float dist = ic.DiagInteractionDistance;
            int mask = ic.DiagLayerMask;
            sb.AppendLine($"  Interaction distance: {dist:F1}m");
            sb.AppendLine($"  Layer mask: {mask} (0x{mask:X8})");

            if (mask == 0)
            {
                sb.AppendLine("    <color=red>WARNING: Mask is 0 — nothing will be detected!</color>");
            }
            else if (mask == -1 || mask == ~0)
            {
                sb.AppendLine("    (All layers — no filtering)");
            }
            else
            {
                sb.Append("    Included layers: ");
                bool first = true;
                for (int i = 0; i < 32; i++)
                {
                    if ((mask & (1 << i)) != 0)
                    {
                        string ln = LayerMask.LayerToName(i);
                        if (string.IsNullOrEmpty(ln)) ln = $"Layer{i}";
                        if (!first) sb.Append(", ");
                        sb.Append($"{ln}({i})");
                        first = false;
                    }
                }
                sb.AppendLine();
            }

            // --- Current targets ---
            sb.AppendLine();
            sb.AppendLine("<color=yellow>--- Current Targets ---</color>");
            sb.AppendLine($"  Looking at interactable: {ic.IsLookingAtInteractable}");
            sb.AppendLine($"  Door: {(ic.IsLookingAtDoor ? $"<color=green>{ic.CurrentDoor?.name}</color>" : "no")}");
            sb.AppendLine($"  Pickup: {(ic.IsLookingAtPickup ? $"<color=green>{ic.CurrentPickup?.name}</color>" : "no")}");
            sb.AppendLine($"  PowerCellSlot: {(ic.IsLookingAtPowerCellSlot ? $"<color=green>{ic.CurrentPowerCellSlot?.name}</color>" : "no")}");
            sb.AppendLine($"  Elevator: {(ic.IsLookingAtElevatorPanel ? $"<color=green>{ic.CurrentElevator?.name}</color>" : "no")}");
            sb.AppendLine($"  NPC Dialogue: {(ic.IsLookingAtNpcDialogue ? "<color=green>yes</color>" : "no")}");

            // --- Raycast test ---
            sb.AppendLine();
            sb.AppendLine("<color=yellow>--- Raycast Test ---</color>");
            if (cam != null)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);

                // First: test with the interaction mask
                if (Physics.Raycast(ray, out RaycastHit hit, dist, mask))
                {
                    var go = hit.collider.gameObject;
                    string ln = LayerMask.LayerToName(go.layer);
                    sb.AppendLine($"  Hit (with mask): '{go.name}' at {hit.distance:F2}m, layer={ln}({go.layer})");

                    // Check what components exist
                    var pickup = go.GetComponent<Pickup>() ?? go.GetComponentInParent<Pickup>();
                    if (pickup != null)
                    {
                        sb.AppendLine($"    Pickup: <color=green>{pickup.GetType().Name}</color>, collected={pickup.IsCollected}");
                    }
                    else
                    {
                        sb.AppendLine("    Pickup component: <color=red>NOT FOUND</color> (not on hit object or parents)");
                    }

                    var door = go.GetComponent<_Scripts.Systems.ProceduralGeneration.Doors.Door>()
                            ?? go.GetComponentInParent<_Scripts.Systems.ProceduralGeneration.Doors.Door>();
                    if (door != null) sb.AppendLine($"    Door: <color=green>found</color>");
                }
                else
                {
                    sb.AppendLine($"  Hit (with mask): <color=red>NOTHING within {dist:F1}m</color>");
                }

                // Second: test WITHOUT mask to see if something is there but being filtered
                if (Physics.Raycast(ray, out RaycastHit hitAll, dist))
                {
                    var go = hitAll.collider.gameObject;
                    string ln = LayerMask.LayerToName(go.layer);
                    bool inMask = (mask & (1 << go.layer)) != 0;
                    sb.AppendLine($"  Hit (no mask): '{go.name}' at {hitAll.distance:F2}m, layer={ln}({go.layer}), in interaction mask={inMask}");
                    if (!inMask)
                    {
                        sb.AppendLine($"    <color=red>BLOCKED: Object is on layer {ln}({go.layer}) which is NOT in the interaction mask!</color>");
                    }
                }
                else
                {
                    sb.AppendLine("  Hit (no mask): nothing within range");
                }
            }
            else
            {
                sb.AppendLine("  <color=red>Cannot raycast — no camera!</color>");
            }

            // --- Inventory ---
            sb.AppendLine();
            sb.AppendLine("<color=yellow>--- Inventory ---</color>");
            var inv = PlayerInventory.Instance;
            if (inv != null)
            {
                sb.AppendLine($"  PlayerInventory: <color=green>found</color>");
            }
            else
            {
                sb.AppendLine("  PlayerInventory: <color=red>NULL — pickups will fail even if detected!</color>");
            }

            // --- Input ---
            sb.AppendLine();
            sb.AppendLine("<color=yellow>--- Input ---</color>");
            if (_Scripts.Core.Managers.InputManager.Instance != null)
            {
                sb.AppendLine($"  InputManager: <color=green>found</color>");
                sb.AppendLine($"  Interact action bound to: F key");
            }
            else
            {
                sb.AppendLine("  InputManager: <color=red>NULL</color>");
            }

            // --- Scene pickups ---
            sb.AppendLine();
            sb.AppendLine("<color=yellow>--- Scene Pickups ---</color>");
            var allPickups = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None);
            int total = allPickups.Length;
            int collected = 0;
            int inactive = 0;
            foreach (var p in allPickups)
            {
                if (p.IsCollected) collected++;
                if (!p.gameObject.activeInHierarchy) inactive++;
            }
            sb.AppendLine($"  Total: {total}, collected: {collected}, inactive: {inactive}, available: {total - collected - inactive}");

            if (total > 0 && total <= 20)
            {
                foreach (var p in allPickups)
                {
                    string ln = LayerMask.LayerToName(p.gameObject.layer);
                    bool inMask = (mask & (1 << p.gameObject.layer)) != 0;
                    string status = p.IsCollected ? "<color=grey>collected</color>"
                                  : !p.gameObject.activeInHierarchy ? "<color=grey>inactive</color>"
                                  : "<color=green>available</color>";
                    string maskStr = inMask ? "" : " <color=red>[NOT IN MASK]</color>";
                    bool hasCollider = p.GetComponent<Collider>() != null || p.GetComponentInChildren<Collider>() != null;
                    string collStr = hasCollider ? "" : " <color=red>[NO COLLIDER]</color>";
                    sb.AppendLine($"    {p.name}: {status}, layer={ln}({p.gameObject.layer}){maskStr}{collStr}");
                }
            }
            else if (total > 20)
            {
                sb.AppendLine($"  (Too many to list individually)");
            }

            return sb.ToString();
        }
    }
}
