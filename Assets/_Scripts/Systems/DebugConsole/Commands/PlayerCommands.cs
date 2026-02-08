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

        #endregion
    }
}
