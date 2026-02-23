using System;
using System.Collections.Generic;
using _Scripts.Core.Managers;
using _Scripts.Systems.Inventory.Pickups;
using _Scripts.Systems.Player;
using UnityEngine;

namespace _Scripts.Systems.Inventory
{
    /// <summary>
    /// Debug inspector for the item persistence system.
    /// Add to scene to see all tracked dropped items, safe room items, and inventory state.
    /// Also logs key persistence events to both Unity console and the debug console.
    /// </summary>
    public class ItemPersistenceTracker : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool _logToUnityConsole = true;
        [SerializeField] private bool _autoRefresh = true;
        [SerializeField] private float _refreshInterval = 1f;

        [Header("Current State")]
        [SerializeField] private int _currentFloor;
        [SerializeField] private bool _floorManagerInitialized;
        [SerializeField] private string _gameState = "Unknown";
        [SerializeField] private bool _playerInSafeRoom;

        [Header("Saved Inventory")]
        [SerializeField] private bool _hasSavedInventory;
        [SerializeField] private int _savedARGrams;
        [SerializeField] private List<string> _savedInventorySlots = new List<string>();

        [Header("Safe Room Items")]
        [SerializeField] private int _safeRoomItemCount;
        [SerializeField] private List<string> _safeRoomItemDisplay = new List<string>();

        [Header("Current Floor Dropped Items")]
        [SerializeField] private int _currentFloorDroppedCount;
        [SerializeField] private List<string> _currentFloorDroppedDisplay = new List<string>();

        [Header("All Floor Dropped Items")]
        [SerializeField] private List<string> _allFloorDroppedSummary = new List<string>();

        [Header("Scene Pickups (Live)")]
        [SerializeField] private int _scenePickupCount;
        [SerializeField] private int _sceneDroppedPickupCount;
        [SerializeField] private List<string> _scenePickupDisplay = new List<string>();

        private float _nextRefreshTime;

        private void Update()
        {
            if (!_autoRefresh) return;

            if (Time.time >= _nextRefreshTime)
            {
                _nextRefreshTime = Time.time + _refreshInterval;
                RefreshTrackerData();
            }
        }

        /// <summary>
        /// Manually refresh all tracker data. Can be called from inspector button or code.
        /// </summary>
        [ContextMenu("Refresh Tracker Data")]
        public void RefreshTrackerData()
        {
            var floorManager = FloorStateManager.Instance;

            // Current state
            _floorManagerInitialized = floorManager != null && floorManager.IsInitialized;
            _currentFloor = floorManager != null ? floorManager.CurrentFloorNumber : -1;
            _gameState = GameManager.Instance != null
                ? GameManager.Instance.CurrentState.ToString()
                : "No GameManager";

            // Check if player is in safe room
            _playerInSafeRoom = false;
            if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentPlayer != null)
            {
                _playerInSafeRoom = FloorStateManager.IsPositionInSafeRoom(
                    PlayerManager.Instance.CurrentPlayer.transform.position);
            }

            if (!_floorManagerInitialized) return;

            // Saved inventory
            var savedInv = floorManager.GetSavedInventory();
            _hasSavedInventory = savedInv != null;
            _savedInventorySlots.Clear();
            if (savedInv != null)
            {
                _savedARGrams = savedInv.arGrams;
                for (int i = 0; i < savedInv.slots.Length; i++)
                {
                    var slot = savedInv.slots[i];
                    string display = string.IsNullOrEmpty(slot.itemId)
                        ? $"  Slot {i}: (empty)"
                        : $"  Slot {i}: {slot.itemId} x{slot.quantity}";
                    _savedInventorySlots.Add(display);
                }
            }
            else
            {
                _savedARGrams = 0;
            }

            // Safe room items
            _safeRoomItemCount = floorManager.SafeRoomDroppedItems.Count;
            _safeRoomItemDisplay.Clear();
            foreach (var item in floorManager.SafeRoomDroppedItems)
            {
                _safeRoomItemDisplay.Add(FormatDroppedItem(item));
            }

            // Current floor dropped items
            var currentFloorState = floorManager.GetCurrentFloorState();
            _currentFloorDroppedCount = currentFloorState.droppedItems?.Count ?? 0;
            _currentFloorDroppedDisplay.Clear();
            if (currentFloorState.droppedItems != null)
            {
                foreach (var item in currentFloorState.droppedItems)
                {
                    _currentFloorDroppedDisplay.Add(FormatDroppedItem(item));
                }
            }

            // All floors summary
            _allFloorDroppedSummary.Clear();
            foreach (var kvp in floorManager.FloorStates)
            {
                int floorNum = kvp.Key;
                var state = kvp.Value;
                int droppedCount = state.droppedItems?.Count ?? 0;

                if (droppedCount > 0)
                {
                    _allFloorDroppedSummary.Add($"Floor {floorNum}: {droppedCount} dropped items (visited: {state.isVisited})");
                    foreach (var item in state.droppedItems)
                    {
                        _allFloorDroppedSummary.Add($"    {FormatDroppedItem(item)}");
                    }
                }
                else
                {
                    _allFloorDroppedSummary.Add($"Floor {floorNum}: no dropped items (visited: {state.isVisited})");
                }
            }

            // Scene pickups (live objects)
            RefreshScenePickups();
        }

        private void RefreshScenePickups()
        {
            _scenePickupDisplay.Clear();
            _scenePickupCount = 0;
            _sceneDroppedPickupCount = 0;

            GameObject pickupsContainer = GameObject.Find("--- PICKUPS ---");
            if (pickupsContainer == null)
            {
                _scenePickupDisplay.Add("No PICKUPS container in scene");
                return;
            }

            Pickup[] pickups = pickupsContainer.GetComponentsInChildren<Pickup>(true);
            _scenePickupCount = pickups.Length;

            foreach (Pickup pickup in pickups)
            {
                if (pickup == null) continue;

                string id = pickup.PickupId ?? "(no id)";
                bool isDropped = id.StartsWith("dropped_");
                bool isCollected = pickup.IsCollected;
                bool isActive = pickup.gameObject.activeInHierarchy;

                if (isDropped) _sceneDroppedPickupCount++;

                string status = isCollected ? "COLLECTED" : (isActive ? "ACTIVE" : "INACTIVE");
                string prefix = isDropped ? "[DROPPED] " : "";
                Vector3 pos = pickup.transform.position;

                _scenePickupDisplay.Add($"{prefix}{id} | {status} | pos: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
            }
        }

        private string FormatDroppedItem(DroppedItemData item)
        {
            if (item == null) return "(null)";
            return $"{item.itemId} (id: {item.droppedItemId}) | qty: {item.quantity} | pos: ({item.posX:F1}, {item.posY:F1}, {item.posZ:F1})";
        }

        /// <summary>
        /// Log the full persistence state to the Unity console.
        /// </summary>
        [ContextMenu("Log Full Persistence State")]
        public void LogFullState()
        {
            RefreshTrackerData();

            Debug.Log("=== ITEM PERSISTENCE STATE ===");
            Debug.Log($"Floor Manager Initialized: {_floorManagerInitialized}");
            Debug.Log($"Current Floor: {_currentFloor}");
            Debug.Log($"Game State: {_gameState}");
            Debug.Log($"Has Saved Inventory: {_hasSavedInventory} (AR: {_savedARGrams}g)");

            foreach (var s in _savedInventorySlots) Debug.Log(s);

            Debug.Log($"--- Safe Room Items ({_safeRoomItemCount}) ---");
            foreach (var s in _safeRoomItemDisplay) Debug.Log($"  {s}");

            Debug.Log($"--- Current Floor Dropped Items ({_currentFloorDroppedCount}) ---");
            foreach (var s in _currentFloorDroppedDisplay) Debug.Log($"  {s}");

            Debug.Log($"--- All Floor Summary ---");
            foreach (var s in _allFloorDroppedSummary) Debug.Log(s);

            Debug.Log($"--- Scene Pickups ({_scenePickupCount} total, {_sceneDroppedPickupCount} dropped) ---");
            foreach (var s in _scenePickupDisplay) Debug.Log($"  {s}");

            Debug.Log("=== END ITEM PERSISTENCE STATE ===");
        }
    }
}
