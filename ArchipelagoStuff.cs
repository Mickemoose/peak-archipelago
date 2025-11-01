using System;
using BepInEx.Logging;
using UnityEngine;
using Archipelago.MultiClient.Net.Enums;

namespace Peak.AP
{

    public class ArchipelagoNotificationManager
    {
        private readonly ManualLogSource _log;
        private readonly string _localSlotName;
        private PlayerConnectionLog _connectionLog;

        // Color for the local player's slot name
        private readonly Color _mySlotColor = new Color(0.7f, 0.4f, 1f);

        public ArchipelagoNotificationManager(ManualLogSource log, string localSlotName)
        {
            _log = log;
            _localSlotName = localSlotName;
        }

        /// <summary>
        /// Find and cache the PlayerConnectionLog component
        /// </summary>
        private void FindConnectionLog()
        {
            if (_connectionLog == null)
            {
                _connectionLog = UnityEngine.Object.FindFirstObjectByType<PlayerConnectionLog>();
                if (_connectionLog != null)
                {
                    _log.LogInfo("[PeakPelago] Found PlayerConnectionLog for notifications");
                }
            }
        }

        /// <summary>
        /// Get the color for an item based on its classification
        /// </summary>
        private Color GetColorForClassification(ItemFlags classification)
        {
            switch (classification)
            {
                case ItemFlags.Advancement:
                    return new Color(0.69f, 0.01f, 0.76f);

                case ItemFlags.NeverExclude:
                    return new Color(0f, 0.32f, 1f);

                case ItemFlags.Trap:
                    return new Color(0.92f, 0.47f, 0f);

                case ItemFlags.None:
                default:
                    return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        /// <summary>
        /// Create a color tag for TextMeshPro
        /// </summary>
        private string GetColorTag(Color c)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">";
        }

        /// <summary>
        /// Display a notification when an item is sent
        /// Format: "fromName sent itemName to toName"
        /// </summary>
        public void ShowItemNotification(string fromName, string toName, string itemName, ItemFlags classification)
        {
            try
            {
                FindConnectionLog();

                if (_connectionLog == null)
                {
                    _log.LogDebug("[PeakPelago] PlayerConnectionLog not found, cannot display notification");
                    return;
                }

                // Determine colors for player names
                bool fromIsMe = fromName.Equals(_localSlotName, StringComparison.OrdinalIgnoreCase);
                bool toIsMe = toName.Equals(_localSlotName, StringComparison.OrdinalIgnoreCase);

                Color fromColor = fromIsMe ? _mySlotColor : _connectionLog.userColor;
                Color toColor = toIsMe ? _mySlotColor : _connectionLog.userColor;
                Color itemColor = GetColorForClassification(classification);

                // Build the message: "fromName sent itemName to toName"
                string fromTag = GetColorTag(fromColor);
                string toTag = GetColorTag(toColor);
                string itemTag = GetColorTag(itemColor);

                string message = $"{fromTag}{fromName}</color> sent {itemTag}{itemName}</color> to {toTag}{toName}</color>";

                // Use reflection to call AddMessage (it's private)
                var addMessageMethod = _connectionLog.GetType().GetMethod("AddMessage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (addMessageMethod != null)
                {
                    addMessageMethod.Invoke(_connectionLog, new object[] { message });

                    // Play sound only if receiving an item (not sending)
                    if (toIsMe && _connectionLog.sfxJoin != null)
                    {
                        _connectionLog.sfxJoin.Play();
                    }

                    _log.LogDebug($"[PeakPelago] Displayed notification: {fromName} -> {itemName} -> {toName}");
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error displaying item notification: " + ex.Message);
            }
        }

        /// <summary>
        /// Display a message without player names
        /// </summary>
        public void ShowSimpleMessage(string message, bool playSound = true)
        {
            try
            {
                FindConnectionLog();

                if (_connectionLog == null)
                {
                    _log.LogDebug("[PeakPelago] PlayerConnectionLog not found, cannot display message");
                    return;
                }

                var addMessageMethod = _connectionLog.GetType().GetMethod("AddMessage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (addMessageMethod != null)
                {
                    // Use joined color (green) for positive messages
                    string colorTag = GetColorTag(_connectionLog.joinedColor);
                    string formattedMessage = colorTag + message + "</color>";

                    addMessageMethod.Invoke(_connectionLog, new object[] { formattedMessage });

                    _log.LogDebug($"[PeakPelago] Displayed message: {message}");
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error displaying simple message: " + ex.Message);
            }
        }

        public void ShowWarningMessage(string message, bool playSound = false)
        {
            try
            {
                FindConnectionLog();

                if (_connectionLog == null) return;

                var addMessageMethod = _connectionLog.GetType().GetMethod("AddMessage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (addMessageMethod != null)
                {
                    string colorTag = GetColorTag(_connectionLog.leftColor);
                    string formattedMessage = colorTag + message + "</color>";

                    addMessageMethod.Invoke(_connectionLog, new object[] { formattedMessage });

                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error displaying warning message: " + ex.Message);
            }
        }

        // some quick helper functions
        public void ShowGoalComplete()
        {
            ShowSimpleMessage("GOAL COMPLETE!", true);
        }

        public void ShowConnected()
        {
            ShowSimpleMessage($"Connected to Archipelago as {_localSlotName}", true);
        }

        public void ShowDisconnected()
        {
            ShowWarningMessage("Disconnected from Archipelago", false);
        }

        public void ShowDeathLink(string cause, string source)
        {
            try
            {
                FindConnectionLog();
                if (_connectionLog == null) return;

                var addMessageMethod = _connectionLog.GetType().GetMethod("AddMessage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (addMessageMethod != null)
                {
                    string deathTag = GetColorTag(new Color(1f, 0.2f, 0.2f));
                    string sourceTag = GetColorTag(_connectionLog.userColor);
                    
                    string message = $"{deathTag}☠ Death Link:</color> {cause} {sourceTag}({source})</color>";
                    
                    addMessageMethod.Invoke(_connectionLog, new object[] { message });

                    if (_connectionLog.sfxLeave != null)
                    {
                        _connectionLog.sfxLeave.Play();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error displaying death link: " + ex.Message);
            }
        }
    }
}