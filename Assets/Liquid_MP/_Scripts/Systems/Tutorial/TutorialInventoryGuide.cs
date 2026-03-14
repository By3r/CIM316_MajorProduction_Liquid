using System.Collections;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Inventory.UI;
using UnityEngine;

namespace _Scripts.Tutorial
{
    /// <summary>
    /// Orchestrates the inventory tutorial notification flow:
    ///   1. When triggered via Begin(), waits for the inventory to open, then shows a task
    ///      notification telling the player to equip the Communication Device.
    ///   2. When the player equips any item, the task completes (strikethrough + shake).
    ///   3. A second task notification appears: "Close the inventory with TAB".
    ///   4. When the player closes the inventory, that task completes and the
    ///      tutorial step advances via TutorialManager.CompleteCurrentStep().
    ///
    /// Wire this up via UnityEvents on a TutorialStep's onStepCompleted:
    ///   → call TutorialInventoryGuide.Begin()
    ///
    /// Requires an InventoryNotification component somewhere in the scene.
    /// </summary>
    public class TutorialInventoryGuide : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField] private InventoryNotification _notification;
        [SerializeField] private TutorialManager _tutorialManager;

        [Header("Messages")]
        [SerializeField, TextArea(2, 4)]
        private string _equipMessage = "Equip the Communication Device by right clicking on its icon and pressing \"EQUIP\"";

        [SerializeField, TextArea(2, 4)]
        private string _closeMessage = "Close the inventory with TAB";

        #endregion

        #region Private Fields

        private bool _isActive;
        private bool _waitingForEquip;
        private bool _waitingForClose;

        #endregion

        #region Public API

        /// <summary>
        /// Starts the inventory guide sequence.
        /// Call this from a TutorialStep's onStepCompleted UnityEvent.
        /// </summary>
        public void Begin()
        {
            Debug.Log("[TutorialInventoryGuide] Begin() called.");

            if (_isActive)
            {
                Debug.LogWarning("[TutorialInventoryGuide] Begin() skipped — already active.");
                return;
            }

            if (_notification == null || _tutorialManager == null)
            {
                Debug.LogError($"[TutorialInventoryGuide] Missing references. _notification={_notification}, _tutorialManager={_tutorialManager}");
                return;
            }

            _isActive = true;

            bool inventoryExists = InventoryUI.Instance != null;
            bool inventoryOpen = inventoryExists && InventoryUI.Instance.IsOpen;
            Debug.Log($"[TutorialInventoryGuide] InventoryUI.Instance exists={inventoryExists}, IsOpen={inventoryOpen}");

            // If the inventory is already open (e.g. HandleFirstPickup opened it),
            // show the notification immediately. Otherwise, wait for it to open.
            if (inventoryExists && inventoryOpen)
            {
                Debug.Log("[TutorialInventoryGuide] Inventory already open → StartEquipPhase()");
                StartEquipPhase();
            }
            else
            {
                // Subscribe and wait
                if (inventoryExists)
                {
                    Debug.Log("[TutorialInventoryGuide] Subscribing to OnInventoryOpened and waiting...");
                    InventoryUI.Instance.OnInventoryOpened += HandleInventoryOpened;
                }
                else
                {
                    Debug.LogError("[TutorialInventoryGuide] InventoryUI.Instance is NULL — cannot subscribe!");
                }
            }
        }

        #endregion

        #region Private Methods

        private void HandleInventoryOpened()
        {
            Debug.Log("[TutorialInventoryGuide] HandleInventoryOpened fired!");
            if (InventoryUI.Instance != null)
                InventoryUI.Instance.OnInventoryOpened -= HandleInventoryOpened;

            StartEquipPhase();
        }

        private void StartEquipPhase()
        {
            Debug.Log("[TutorialInventoryGuide] StartEquipPhase()");
            _waitingForEquip = true;

            // Subscribe to equip event
            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance.OnItemEquipped += HandleItemEquipped;
                Debug.Log("[TutorialInventoryGuide] Subscribed to OnItemEquipped.");
            }
            else
            {
                Debug.LogError("[TutorialInventoryGuide] InventoryUI.Instance is NULL in StartEquipPhase!");
            }

            // Show the equip task notification (with a tiny delay so the inventory
            // UI has time to fully render before the notification expands)
            Debug.Log("[TutorialInventoryGuide] Starting ShowEquipTaskDelayed coroutine...");
            StartCoroutine(ShowEquipTaskDelayed());
        }

        private IEnumerator ShowEquipTaskDelayed()
        {
            // Wait one frame for the inventory UI to fully lay out
            yield return null;
            Debug.Log($"[TutorialInventoryGuide] Calling _notification.ShowTask('{_equipMessage}')");
            _notification.ShowTask(_equipMessage);
        }

        private void HandleItemEquipped(InventoryItemData itemData)
        {
            if (!_waitingForEquip) return;
            _waitingForEquip = false;

            Debug.Log($"[TutorialInventoryGuide] HandleItemEquipped — '{itemData?.displayName}'");

            // Unsubscribe
            if (InventoryUI.Instance != null)
                InventoryUI.Instance.OnItemEquipped -= HandleItemEquipped;

            // Complete the equip task (strikethrough + shake)
            _notification.CompleteTask();

            // When the equip task animation finishes, show the close task
            _notification.OnTaskCompleted += HandleEquipTaskCompleted;
        }

        private void HandleEquipTaskCompleted()
        {
            _notification.OnTaskCompleted -= HandleEquipTaskCompleted;
            Debug.Log("[TutorialInventoryGuide] Equip task completed. Showing close task.");

            // Start the close-inventory phase
            _waitingForClose = true;
            _notification.ShowTask(_closeMessage);

            // Listen for inventory close
            if (InventoryUI.Instance != null)
                InventoryUI.Instance.OnInventoryClosed += HandleInventoryClosed;
        }

        private void HandleInventoryClosed()
        {
            if (!_waitingForClose) return;
            _waitingForClose = false;

            Debug.Log("[TutorialInventoryGuide] HandleInventoryClosed fired.");

            // Unsubscribe
            if (InventoryUI.Instance != null)
                InventoryUI.Instance.OnInventoryClosed -= HandleInventoryClosed;

            // Complete the close task
            _notification.CompleteTask();

            // When the close task animation finishes, advance the tutorial
            _notification.OnTaskCompleted += HandleCloseTaskCompleted;
        }

        private void HandleCloseTaskCompleted()
        {
            _notification.OnTaskCompleted -= HandleCloseTaskCompleted;
            Debug.Log("[TutorialInventoryGuide] Close task completed. Advancing tutorial.");

            _isActive = false;
        }

        private void OnDestroy()
        {
            // Cleanup subscriptions
            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance.OnInventoryOpened -= HandleInventoryOpened;
                InventoryUI.Instance.OnItemEquipped -= HandleItemEquipped;
                InventoryUI.Instance.OnInventoryClosed -= HandleInventoryClosed;
            }

            if (_notification != null)
            {
                _notification.OnTaskCompleted -= HandleEquipTaskCompleted;
                _notification.OnTaskCompleted -= HandleCloseTaskCompleted;
            }
        }

        #endregion
    }
}
