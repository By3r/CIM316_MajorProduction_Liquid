using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Tracks which crafting schematics the player has uploaded to their suit.
    /// Uploaded schematics are permanent — they persist across floor transitions
    /// and appear in the terminal's fabrication panel.
    /// </summary>
    public class SchematicRegistry : MonoBehaviour
    {
        #region Singleton

        private static SchematicRegistry _instance;
        public static SchematicRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<SchematicRegistry>();
                }
                return _instance;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Fired whenever a schematic is unlocked (or the list is restored).
        /// Subscribers (e.g. SafeRoomTerminalUI) should refresh their fabrication panel.
        /// </summary>
        public event Action OnSchematicsChanged;

        #endregion

        #region Private Fields

        private readonly List<SchematicSO> _unlockedSchematics = new List<SchematicSO>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Unlocks a schematic so it appears in the fabrication panel.
        /// No-ops if already unlocked.
        /// </summary>
        public void UnlockSchematic(SchematicSO schematic)
        {
            if (schematic == null) return;
            if (IsUnlocked(schematic)) return;

            _unlockedSchematics.Add(schematic);
            Debug.Log($"[SchematicRegistry] Unlocked schematic '{schematic.schematicId}'. Total: {_unlockedSchematics.Count}");
            OnSchematicsChanged?.Invoke();
        }

        /// <summary>
        /// Returns true if the given schematic has been unlocked.
        /// </summary>
        public bool IsUnlocked(SchematicSO schematic)
        {
            if (schematic == null) return false;
            return _unlockedSchematics.Contains(schematic);
        }

        /// <summary>
        /// Returns all currently unlocked schematics (read-only).
        /// </summary>
        public IReadOnlyList<SchematicSO> GetUnlockedSchematics()
        {
            return _unlockedSchematics;
        }

        /// <summary>
        /// Returns the number of unlocked schematics.
        /// </summary>
        public int Count => _unlockedSchematics.Count;

        #endregion

        #region Save / Restore

        /// <summary>
        /// Creates a serializable list of schematic IDs for persistence.
        /// </summary>
        public List<string> ToSaveData()
        {
            var ids = new List<string>(_unlockedSchematics.Count);
            foreach (var schematic in _unlockedSchematics)
            {
                if (schematic != null)
                    ids.Add(schematic.schematicId);
            }
            return ids;
        }

        /// <summary>
        /// Restores unlocked schematics from a list of schematic IDs.
        /// Resolves IDs to SchematicSO assets via Resources lookup.
        /// </summary>
        public void RestoreFromSaveData(List<string> schematicIds)
        {
            _unlockedSchematics.Clear();

            if (schematicIds == null || schematicIds.Count == 0)
            {
                OnSchematicsChanged?.Invoke();
                return;
            }

            // Load all SchematicSO assets and build a lookup
            var allSchematics = Resources.FindObjectsOfTypeAll<SchematicSO>();
            var lookup = new Dictionary<string, SchematicSO>(allSchematics.Length);
            foreach (var s in allSchematics)
            {
                if (s != null && !string.IsNullOrEmpty(s.schematicId))
                    lookup[s.schematicId] = s;
            }

            foreach (var id in schematicIds)
            {
                if (lookup.TryGetValue(id, out SchematicSO schematic))
                {
                    _unlockedSchematics.Add(schematic);
                }
                else
                {
                    Debug.LogWarning($"[SchematicRegistry] Could not find schematic '{id}' during restore.");
                }
            }

            Debug.Log($"[SchematicRegistry] Restored {_unlockedSchematics.Count} schematics.");
            OnSchematicsChanged?.Invoke();
        }

        #endregion
    }
}
