using System.Text;
using _Scripts.UI.Interaction;
using UnityEngine;

namespace _Scripts.Systems.DebugConsole.Commands
{
    /// <summary>
    /// Debug commands for diagnosing the ObjectHighlightingSystem (Deus Ex-style highlights).
    /// </summary>
    public static class HighlightCommands
    {
        [DebugCommand("highlight diag", "Diagnoses the Object Highlighting System state.", "highlight diag")]
        public static string HighlightDiag(string[] args)
        {
            var system = Object.FindFirstObjectByType<ObjectHighlightingSystem>();
            if (system == null)
                return "<color=red>ObjectHighlightingSystem not found in scene.</color>";

            var sb = new StringBuilder();
            sb.AppendLine("<color=yellow>=== Object Highlighting System Diagnostics ===</color>");

            // Enabled state
            sb.AppendLine($"  Enabled: {(system.enabled ? "<color=green>YES</color>" : "<color=red>NO</color>")}");
            sb.AppendLine($"  GameObject active: {(system.gameObject.activeInHierarchy ? "<color=green>YES</color>" : "<color=red>NO</color>")}");

            // Camera
            if (system.PlayerCamera != null)
            {
                sb.AppendLine($"  Camera: <color=green>{system.PlayerCamera.name}</color> (on '{system.PlayerCamera.gameObject.name}')");
                sb.AppendLine($"    Tag: {system.PlayerCamera.tag}, Enabled: {system.PlayerCamera.enabled}");
            }
            else
            {
                sb.AppendLine("  Camera: <color=red>NULL — raycasts will not work!</color>");
                sb.AppendLine("    Camera.main: " + (Camera.main != null ? Camera.main.name : "<color=red>NULL</color>"));

                var pm = _Scripts.Systems.Player.PlayerManager.Instance;
                if (pm != null && pm.CurrentPlayer != null)
                {
                    var cam = pm.CurrentPlayer.GetComponentInChildren<Camera>();
                    sb.AppendLine($"    Player child camera: {(cam != null ? cam.name : "<color=red>NOT FOUND</color>")}");
                }
                else
                {
                    sb.AppendLine($"    PlayerManager: {(pm != null ? "exists" : "<color=red>NULL</color>")}, CurrentPlayer: {(pm?.CurrentPlayer != null ? pm.CurrentPlayer.name : "<color=red>NULL</color>")}");
                }
            }

            // Layer mask
            int mask = system.CombinedLayerMask;
            sb.AppendLine($"  Combined LayerMask: {mask} (0x{mask:X8})");
            if (mask == 0)
            {
                sb.AppendLine("    <color=red>WARNING: Layer mask is 0 — no layers will be detected!</color>");
            }
            else
            {
                sb.Append("    Active layers: ");
                bool first = true;
                for (int i = 0; i < 32; i++)
                {
                    if ((mask & (1 << i)) != 0)
                    {
                        string layerName = LayerMask.LayerToName(i);
                        if (string.IsNullOrEmpty(layerName)) layerName = $"Layer{i}";
                        if (!first) sb.Append(", ");
                        sb.Append($"{layerName}({i})");
                        first = false;
                    }
                }
                sb.AppendLine();
            }

            // Layer configs
            var configs = system.LayerConfigs;
            sb.AppendLine($"  Layer configs: {configs.Count}");
            foreach (var config in configs)
            {
                string layerName = LayerMask.LayerToName(config.layer);
                if (string.IsNullOrEmpty(layerName)) layerName = $"Layer{config.layer}";
                string enabledStr = config.enabled ? "<color=green>ON</color>" : "<color=red>OFF</color>";
                sb.AppendLine($"    [{enabledStr}] '{config.configName}' → layer {layerName}({config.layer}), brackets={config.showBrackets}, text='{config.displayText}'");
            }

            // Highlight state
            sb.AppendLine($"  Highlight active: {(system.IsHighlightActive ? "<color=green>YES</color>" : "no")}");
            sb.AppendLine($"  Current target: {(system.CurrentTargetObject != null ? system.CurrentTargetObject.name : "none")}");
            sb.AppendLine($"  Alpha: current={system.CurrentAlpha:F2}, target={system.TargetAlpha:F2}");

            // Canvas group
            var cg = system.HighlightCanvasGroup;
            if (cg != null)
            {
                sb.AppendLine($"  CanvasGroup alpha: {cg.alpha:F2}");
            }
            else
            {
                sb.AppendLine("  CanvasGroup: <color=red>NULL</color>");
            }

            // Frame
            var frame = system.HighlightFrame;
            if (frame != null)
            {
                sb.AppendLine($"  Frame active: {frame.gameObject.activeSelf}, pos={frame.anchoredPosition}, size={frame.sizeDelta}");
            }

            // Brackets
            var brackets = system.CornerBrackets;
            if (brackets != null && brackets.Length == 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    string[] names = { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };
                    if (brackets[i] != null)
                    {
                        var rt = brackets[i].GetComponent<RectTransform>();
                        sb.AppendLine($"  Bracket[{names[i]}]: active={brackets[i].gameObject.activeSelf}, color={brackets[i].color}, pos={rt.anchoredPosition}");
                    }
                    else
                    {
                        sb.AppendLine($"  Bracket[{names[i]}]: <color=red>NULL</color>");
                    }
                }
            }
            else
            {
                sb.AppendLine($"  Brackets: <color=red>{(brackets == null ? "NULL array" : $"wrong length ({brackets.Length})")}</color>");
            }

            // Raycast test
            if (system.PlayerCamera != null)
            {
                Ray ray = system.PlayerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, 10f))
                {
                    string hitLayer = LayerMask.LayerToName(hit.collider.gameObject.layer);
                    bool inMask = (mask & (1 << hit.collider.gameObject.layer)) != 0;
                    sb.AppendLine($"  Raycast hit: '{hit.collider.gameObject.name}' at {hit.distance:F1}m, layer={hitLayer}({hit.collider.gameObject.layer}), in mask={inMask}");
                }
                else
                {
                    sb.AppendLine("  Raycast hit: nothing within 10m");
                }
            }

            return sb.ToString();
        }
    }
}
