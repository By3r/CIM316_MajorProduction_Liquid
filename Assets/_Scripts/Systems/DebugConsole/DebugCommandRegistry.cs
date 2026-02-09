using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace _Scripts.Systems.DebugConsole
{
    /// <summary>
    /// Static registry that stores, resolves, and executes debug console commands.
    /// Automatically scans for methods marked with [DebugCommand] at initialization.
    /// </summary>
    public static class DebugCommandRegistry
    {
        #region Types

        public struct CommandEntry
        {
            public string Name;
            public string Description;
            public string Usage;
            public Func<string[], string> Execute;
        }

        #endregion

        #region Fields

        private static readonly Dictionary<string, CommandEntry> _commands = new();
        private static bool _isInitialized;

        #endregion

        #region Initialization

        /// <summary>
        /// Scans all loaded assemblies for [DebugCommand] methods and registers them.
        /// Safe to call multiple times — only runs once.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            _commands.Clear();
            ScanForAttributeCommands();
            _isInitialized = true;

            Debug.Log($"[DebugCommandRegistry] Initialized with {_commands.Count} commands.");
        }

        #endregion

        #region Registration

        /// <summary>
        /// Manually registers a command. Use this for dynamic or runtime-generated commands.
        /// </summary>
        public static void Register(string name, string description, string usage, Func<string[], string> execute)
        {
            string key = name.ToLower();

            if (_commands.ContainsKey(key))
            {
                Debug.LogWarning($"[DebugCommandRegistry] Command '{key}' already registered. Overwriting.");
            }

            _commands[key] = new CommandEntry
            {
                Name = key,
                Description = description,
                Usage = usage,
                Execute = execute,
            };
        }

        /// <summary>
        /// Unregisters a command by name.
        /// </summary>
        public static void Unregister(string name)
        {
            _commands.Remove(name.ToLower());
        }

        #endregion

        #region Execution

        /// <summary>
        /// Parses raw input and executes the matching command.
        /// Supports multi-word commands via longest-match-first resolution.
        /// </summary>
        /// <param name="rawInput">The full string the user typed (e.g. "seed set 42").</param>
        /// <returns>The command's output string, or an error message.</returns>
        public static string ExecuteCommand(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
                return null;

            string input = rawInput.Trim();
            string[] tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0) return null;

            // Try longest command match first (supports "seed set", "floor goto", etc.)
            int maxWords = Math.Min(tokens.Length, 4);
            for (int wordCount = maxWords; wordCount >= 1; wordCount--)
            {
                string candidate = string.Join(" ", tokens, 0, wordCount).ToLower();

                if (_commands.TryGetValue(candidate, out var entry))
                {
                    string[] args = tokens.Skip(wordCount).ToArray();

                    try
                    {
                        return entry.Execute(args);
                    }
                    catch (Exception ex)
                    {
                        return $"ERROR executing '{candidate}': {ex.Message}";
                    }
                }
            }

            return $"Unknown command: '{tokens[0]}'. Type 'help' for available commands.";
        }

        #endregion

        #region Queries

        /// <summary>
        /// Returns all registered commands. Used by the help command.
        /// </summary>
        public static IReadOnlyDictionary<string, CommandEntry> GetAllCommands() => _commands;

        #endregion

        #region Reflection Scanning

        private static void ScanForAttributeCommands()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                // Only scan game assemblies
                string name = assembly.GetName().Name;
                if (!name.StartsWith("Assembly-CSharp") && !name.Contains("_Scripts"))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                    foreach (var method in methods)
                    {
                        var attr = method.GetCustomAttribute<DebugCommandAttribute>();
                        if (attr == null) continue;

                        // Validate signature: static string Method(string[] args)
                        var parameters = method.GetParameters();
                        if (method.ReturnType != typeof(string) ||
                            parameters.Length != 1 ||
                            parameters[0].ParameterType != typeof(string[]))
                        {
                            Debug.LogWarning(
                                $"[DebugCommandRegistry] Skipping {type.Name}.{method.Name}: " +
                                "Expected signature: static string Method(string[] args)");
                            continue;
                        }

                        var func = (Func<string[], string>)Delegate.CreateDelegate(
                            typeof(Func<string[], string>), method);

                        Register(attr.CommandName, attr.Description, attr.Usage, func);
                    }
                }
            }
        }

        #endregion
    }
}
