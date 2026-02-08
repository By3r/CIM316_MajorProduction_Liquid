using _Scripts.Systems.Machines;
using _Scripts.Systems.Player;
using UnityEngine;

namespace _Scripts.Systems.DebugConsole.Commands
{
    /// <summary>
    /// Debug commands for player state (god mode, noclip).
    /// </summary>
    public static class PlayerCommands
    {
        #region State

        private static bool _godMode;
        private static bool _noclipMode;

        #endregion

        #region Properties

        /// <summary>
        /// Whether god mode is currently active. Other systems can check this.
        /// </summary>
        public static bool IsGodMode => _godMode;

        /// <summary>
        /// Whether noclip mode is currently active. Other systems can check this.
        /// </summary>
        public static bool IsNoclipMode => _noclipMode;

        #endregion

        #region Commands

        [DebugCommand("god", "Toggles god mode (invincibility).", "god")]
        public static string God(string[] args)
        {
            _godMode = !_godMode;

            return _godMode
                ? "<color=green>God mode: ON</color>"
                : "<color=red>God mode: OFF</color>";
        }

        [DebugCommand("noclip", "Toggles noclip mode (fly through walls).", "noclip")]
        public static string Noclip(string[] args)
        {
            if (PlayerManager.Instance == null || PlayerManager.Instance.CurrentPlayer == null)
                return "<color=red>Player not found.</color>";

            var player = PlayerManager.Instance.CurrentPlayer;
            var characterController = player.GetComponent<CharacterController>();

            if (characterController == null)
                return "<color=red>No CharacterController found on player.</color>";

            _noclipMode = !_noclipMode;
            characterController.enabled = !_noclipMode;

            return _noclipMode
                ? "<color=green>Noclip: ON — CharacterController disabled</color>"
                : "<color=red>Noclip: OFF — CharacterController re-enabled</color>";
        }

        [DebugCommand("tp", "Teleports the player. Use 'tp elevator' for safe room, or 'tp x y z' for coordinates.", "tp elevator | tp <x> <y> <z>")]
        public static string Teleport(string[] args)
        {
            if (args.Length == 0)
                return "Usage: tp elevator | tp <x> <y> <z>";

            if (PlayerManager.Instance == null || PlayerManager.Instance.CurrentPlayer == null)
                return "<color=red>Player not found.</color>";

            var player = PlayerManager.Instance.CurrentPlayer;
            var characterController = player.GetComponent<CharacterController>();

            if (args[0].ToLower() == "elevator" || args[0].ToLower() == "saferoom")
            {
                // Find the elevator in the scene
                Elevator elevator = Object.FindObjectOfType<Elevator>();
                if (elevator != null)
                {
                    Vector3 target = elevator.transform.position + Vector3.up * 1f;
                    TeleportPlayer(player, characterController, target);
                    return $"<color=green>Teleported to elevator at ({target.x:F1}, {target.y:F1}, {target.z:F1})</color>";
                }

                // Fallback: find the entry room
                GameObject entryRoom = GameObject.Find("EntryRoom");
                if (entryRoom != null)
                {
                    Vector3 target = entryRoom.transform.position + Vector3.up * 1f;
                    TeleportPlayer(player, characterController, target);
                    return $"<color=green>Teleported to entry room at ({target.x:F1}, {target.y:F1}, {target.z:F1})</color>";
                }

                return "<color=red>Could not find elevator or entry room in scene.</color>";
            }

            // Coordinate-based teleport: tp x y z
            if (args.Length < 3)
                return "Usage: tp <x> <y> <z>";

            if (!float.TryParse(args[0], out float x) ||
                !float.TryParse(args[1], out float y) ||
                !float.TryParse(args[2], out float z))
            {
                return "<color=red>Invalid coordinates. Use: tp <x> <y> <z></color>";
            }

            Vector3 pos = new Vector3(x, y, z);
            TeleportPlayer(player, characterController, pos);
            return $"<color=green>Teleported to ({x:F1}, {y:F1}, {z:F1})</color>";
        }

        private static void TeleportPlayer(Component player, CharacterController cc, Vector3 position)
        {
            // Disable CharacterController to allow position change
            bool wasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            player.transform.position = position;

            if (cc != null && wasEnabled) cc.enabled = true;
        }

        #endregion
    }
}
