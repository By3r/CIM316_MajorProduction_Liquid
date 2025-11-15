using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Core
{
    /// <summary>
    /// Manages a global event system for inter-system communication using the observer pattern.
    /// Supports both parameterless and parameterized event subscriptions.
    /// </summary>
    public class EventManager : MonoBehaviour
    {
        private Dictionary<string, Delegate> _eventDictionary = new();

        /// <summary>
        /// Subscribes a callback to an event. The callback will be invoked whenever the event is published.
        /// Multiple callbacks can subscribe to the same event.
        /// </summary>
        /// <param name="eventName">The name of the event to subscribe to.</param>
        /// <param name="callback">The parameterless action to execute when the event is published.</param>
        public void Subscribe(string eventName, Action callback)
        {
            if (_eventDictionary.TryGetValue(eventName, out Delegate existingDelegate))
            {
                _eventDictionary[eventName] = Delegate.Combine(existingDelegate, callback);
            }
            else
            {
                _eventDictionary[eventName] = callback;
            }
        }

        /// <summary>
        /// Subscribes a callback to an event with typed data.
        /// The callback will receive the published data as a parameter.
        /// </summary>
        /// <typeparam name="T">The type of data passed to subscribers.</typeparam>
        /// <param name="eventName">The name of the event to subscribe to.</param>
        /// <param name="callback">The action that accepts typed data when the event is published.</param>
        public void Subscribe<T>(string eventName, Action<T> callback)
        {
            if (_eventDictionary.TryGetValue(eventName, out Delegate existingDelegate))
            {
                _eventDictionary[eventName] = Delegate.Combine(existingDelegate, callback);
            }
            else
            {
                _eventDictionary[eventName] = callback;
            }
        }

        /// <summary>
        /// Unsubscribes a callback from a parameterless event.
        /// </summary>
        /// <param name="eventName">The name of the event to unsubscribe from.</param>
        /// <param name="callback">The exact action reference that was previously subscribed.</param>
        public void Unsubscribe(string eventName, Action callback)
        {
            if (_eventDictionary.TryGetValue(eventName, out Delegate existingDelegate))
            {
                _eventDictionary[eventName] = Delegate.Remove(existingDelegate, callback);
            }
        }

        /// <summary>
        /// Unsubscribes a callback from a typed event.
        /// </summary>
        /// <typeparam name="T">The type of data the event passes.</typeparam>
        /// <param name="eventName">The name of the event to unsubscribe from.</param>
        /// <param name="callback">The exact action reference that was previously subscribed.</param>
        public void Unsubscribe<T>(string eventName, Action<T> callback)
        {
            if (_eventDictionary.TryGetValue(eventName, out Delegate existingDelegate))
            {
                _eventDictionary[eventName] = Delegate.Remove(existingDelegate, callback);
            }
        }

        /// <summary>
        /// Publishes a parameterless event, invoking all subscribed callbacks.
        /// </summary>
        /// <param name="eventName">The name of the event to publish.</param>
        public void Publish(string eventName)
        {
            if (_eventDictionary.TryGetValue(eventName, out Delegate callback))
            {
                (callback as Action)?.Invoke();
            }
        }

        /// <summary>
        /// Publishes an event with typed data, invoking all subscribed callbacks with that data.
        /// </summary>
        /// <typeparam name="T">The type of data to pass to subscribers.</typeparam>
        /// <param name="eventName">The name of the event to publish.</param>
        /// <param name="data">The data to pass to all subscribed callbacks.</param>
        public void Publish<T>(string eventName, T data)
        {
            if (_eventDictionary.TryGetValue(eventName, out Delegate callback))
            {
                (callback as Action<T>)?.Invoke(data);
            }
        }

        private void OnDestroy()
        {
            _eventDictionary.Clear();
        }
    }

    /// <summary>
    /// Centralized collection of all game event names used throughout the application.
    /// Use these constants when subscribing to or publishing events through the EventManager.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Fired when the player dies. Triggers respawn logic.</summary>
        public const string OnPlayerDeath = "OnPlayerDeath";
        
        /// <summary>Fired when the player respawns after death.</summary>
        public const string OnPlayerRespawn = "OnPlayerRespawn";
        
        /// <summary>Fired when the player enters a safe room.</summary>
        public const string OnPlayerEnteredSafeRoom = "OnPlayerEnteredSafeRoom";
        
        /// <summary>Fired when the player exits a safe room.</summary>
        public const string OnPlayerExitedSafeRoom = "OnPlayerExitedSafeRoom";
        
        /// <summary>Fired when augmented resources are mined/collected.</summary>
        public const string OnARMined = "OnARMined";
        
        /// <summary>Fired when augmented resources are deposited.</summary>
        public const string OnARDeposited = "OnARDeposited";
        
        /// <summary>Fired when augmented resources are lost.</summary>
        public const string OnARLost = "OnARLost";
        
        /// <summary>Fired when a new floor is procedurally generated.</summary>
        public const string OnFloorGenerated = "OnFloorGenerated";
        
        /// <summary>Fired when all enemies on a floor are defeated.</summary>
        public const string OnFloorCleared = "OnFloorCleared";
        
        /// <summary>Fired when the player uses an elevator.</summary>
        public const string OnElevatorUsed = "OnElevatorUsed";
        
        /// <summary>Fired when an enemy is spawned.</summary>
        public const string OnEnemySpawned = "OnEnemySpawned";
        
        /// <summary>Fired when an enemy is killed.</summary>
        public const string OnEnemyKilled = "OnEnemyKilled";
        
        /// <summary>Fired when the game's threat level changes.</summary>
        public const string OnThreatLevelChanged = "OnThreatLevelChanged";
        
        /// <summary>Fired when the player fires a weapon.</summary>
        public const string OnWeaponFired = "OnWeaponFired";
        
        /// <summary>Fired when background music transitions between tracks.</summary>
        public const string OnMusicTransition = "OnMusicTransition";
        
        /// <summary>Fired when ambient sounds change.</summary>
        public const string OnAmbienceChange = "OnAmbienceChange";
        
        /// <summary>Fired when the game state changes (Gameplay, Paused, MainMenu, etc.).</summary>
        public const string OnGameStateChanged = "OnGameStateChanged";
    }
}