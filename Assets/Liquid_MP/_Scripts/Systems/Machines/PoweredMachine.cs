/*
 * NOT YET INTEGRATED - Commented out for later integration
 * Remove #if false and #endif when ready to integrate PoweredMachine system
 */
#if false

using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Systems.Machines
{
    /// <summary>
    /// Base class for machines that require power from a PowerCellSlot.
    /// </summary>
    public class PoweredMachine : MonoBehaviour
    {
        [Header("Power")]
        [SerializeField] protected PowerCellSlot _powerCellSlot;

        [Header("Events")]
        [SerializeField] protected UnityEvent _onPoweredOn;
        [SerializeField] protected UnityEvent _onPoweredOff;

        public bool IsPowered => _powerCellSlot != null && _powerCellSlot.IsPowered;

        protected virtual void OnEnable()
        {
            if (_powerCellSlot != null)
            {
                _powerCellSlot.OnPowerStateChanged += HandlePowerStateChanged;
            }
        }

        protected virtual void OnDisable()
        {
            if (_powerCellSlot != null)
            {
                _powerCellSlot.OnPowerStateChanged -= HandlePowerStateChanged;
            }
        }

        protected virtual void HandlePowerStateChanged(bool isPowered)
        {
            if (isPowered)
            {
                OnPoweredOn();
                _onPoweredOn?.Invoke();
            }
            else
            {
                OnPoweredOff();
                _onPoweredOff?.Invoke();
            }
        }

        protected virtual void OnPoweredOn()
        {
            Debug.Log($"[PoweredMachine] {gameObject.name} powered ON");
        }

        protected virtual void OnPoweredOff()
        {
            Debug.Log($"[PoweredMachine] {gameObject.name} powered OFF");
        }
    }
}

#endif
