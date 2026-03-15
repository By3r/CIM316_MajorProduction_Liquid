using System.Collections;
using _Scripts.Systems.Inventory;
using _Scripts.Systems.Inventory.UI;
using _Scripts.Systems.HUD;
using _Scripts.Systems.Player;
using UnityEngine;

namespace _Scripts.Tutorial
{
    /// <summary>
    /// Orchestrates the inventory tutorial notification flow:
    ///   1. When triggered via Begin(), waits for the inventory to open, then shows a task
    ///      notification telling the player to equip the Communication Device.
    ///   2. When the player equips any item, the task completes (strikethrough + shake).
    ///   3. A second task notification appears: "Close the inventory with TAB".
    ///      If the player already closed it, the message still appears and auto completes.
    ///   4. A third task notification appears: "Press 3 to pull out COMS Tool".
    ///   5. When the player activates the COMS device, that task completes and the
    ///      tutorial step advances via TutorialManager.CompleteCurrentStep().
    ///
    /// Wire this up via UnityEvents on a TutorialStep's onStepCompleted:
    ///   → call TutorialInventoryGuide.Begin()
    ///
    /// Requires an VisorNotification component somewhere in the scene.
    /// </summary>
    public class TutorialInventoryGuide : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField] private VisorNotification _notification;
        [SerializeField] private TutorialManager _tutorialManager;
        [SerializeField] private ComsDeviceController _comsDeviceController;

        [Header("Messages")]
        [SerializeField, TextArea(2, 4)]
        private string _equipMessage = "Equip the Communication Device by right clicking on its icon and pressing \"EQUIP\"";

        [SerializeField, TextArea(2, 4)]
        private string _closeMessage = "Close the inventory with TAB";

        [SerializeField, TextArea(2, 4)]
        private string _pullOutComsMessage = "Press 3 to pull out COMS Tool";

        #endregion

        #region Private Fields

        private bool _isActive;
        private bool _waitingForEquip;
        private bool _waitingForClose;
        private bool _waitingForComsPullOut;
        private bool _inventoryClosedEarly;

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

            // Subscribe to inventory close NOW so we catch it even during the animation
            _inventoryClosedEarly = false;
            if (InventoryUI.Instance != null)
                InventoryUI.Instance.OnInventoryClosed += HandleEarlyInventoryClosed;

            // Complete the equip task (strikethrough + shake)
            _notification.CompleteTask();

            // When the equip task animation finishes, show the close task
            _notification.OnTaskCompleted += HandleEquipTaskCompleted;
        }

        private void HandleEarlyInventoryClosed()
        {
            Debug.Log("[TutorialInventoryGuide] Inventory closed early (during equip animation).");
            _inventoryClosedEarly = true;
            if (InventoryUI.Instance != null)
                InventoryUI.Instance.OnInventoryClosed -= HandleEarlyInventoryClosed;
        }

        private void HandleEquipTaskCompleted()
        {
            _notification.OnTaskCompleted -= HandleEquipTaskCompleted;
            Debug.Log("[TutorialInventoryGuide] Equip task completed. Showing close task.");

            // Unsubscribe early close listener if still active
            if (InventoryUI.Instance != null)
                InventoryUI.Instance.OnInventoryClosed -= HandleEarlyInventoryClosed;

            if (_inventoryClosedEarly)
            {
                // Player already closed inventory, but still show the message
                // so they see it completed, then auto complete it
                Debug.Log("[TutorialInventoryGuide] Inventory was already closed. Showing close task and auto completing.");
                _notification.ShowTask(_closeMessage);
                _notification.CompleteTask();
                _notification.OnTaskCompleted += HandleCloseTaskCompleted;
                return;
            }

            // Start the close inventory phase
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

            // When the close task animation finishes, show the COMS pull out task
            _notification.OnTaskCompleted += HandleCloseTaskCompleted;
        }

        private void HandleCloseTaskCompleted()
        {
            _notification.OnTaskCompleted -= HandleCloseTaskCompleted;
            Debug.Log("[TutorialInventoryGuide] Close task completed. Showing COMS pull out task.");

            // Start the COMS pull out phase
            _waitingForComsPullOut = true;
            _notification.ShowTask(_pullOutComsMessage);

            if (_comsDeviceController != null)
            {
                _comsDeviceController.OnComsActivated += HandleComsActivated;
            }
            else
            {
                Debug.LogWarning("[TutorialInventoryGuide] _comsDeviceController is NULL. Cannot listen for COMS activation.");
            }
        }

        private void HandleComsActivated()
        {
            if (!_waitingForComsPullOut) return;
            _waitingForComsPullOut = false;

            Debug.Log("[TutorialInventoryGuide] COMS device activated. Completing pull out task.");

            if (_comsDeviceController != null)
                _comsDeviceController.OnComsActivated -= HandleComsActivated;

            _notification.CompleteTask();

            _notification.OnTaskCompleted += HandleComsPullOutTaskCompleted;
        }

        private void HandleComsPullOutTaskCompleted()
        {
            _notification.OnTaskCompleted -= HandleComsPullOutTaskCompleted;
            Debug.Log("[TutorialInventoryGuide] COMS pull out task completed. Advancing tutorial.");

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
                InventoryUI.Instance.OnInventoryClosed -= HandleEarlyInventoryClosed;
            }

            if (_comsDeviceController != null)
                _comsDeviceController.OnComsActivated -= HandleComsActivated;

            if (_notification != null)
            {
                _notification.OnTaskCompleted -= HandleEquipTaskCompleted;
                _notification.OnTaskCompleted -= HandleCloseTaskCompleted;
                _notification.OnTaskCompleted -= HandleComsPullOutTaskCompleted;
            }
        }

        #endregion
    }
}
