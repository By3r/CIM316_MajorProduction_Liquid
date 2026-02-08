using System;

namespace _Scripts.Systems.DebugConsole
{
    /// <summary>
    /// Marks a static method as a debug console command.
    /// Method must have signature: static string MethodName(string[] args)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DebugCommandAttribute : Attribute
    {
        /// <summary>
        /// The command name typed by the user (e.g. "seed set"). Supports multi-word commands.
        /// </summary>
        public string CommandName { get; }

        /// <summary>
        /// Short description shown in the help listing.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Usage string shown when the user types "help {command}" (e.g. "seed set {number}").
        /// </summary>
        public string Usage { get; }

        public DebugCommandAttribute(string commandName, string description, string usage = null)
        {
            CommandName = commandName.ToLower();
            Description = description;
            Usage = usage ?? commandName;
        }
    }
}
