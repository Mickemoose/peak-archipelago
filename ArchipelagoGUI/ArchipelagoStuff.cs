using System;
using BepInEx.Logging;
using UnityEngine;
using Archipelago.MultiClient.Net.Enums;
using Photon.Pun;

namespace Peak.AP
{
    public class ArchipelagoNotificationManager
    {
        private readonly ManualLogSource _log;
        private readonly string _localSlotName;
        private PlayerConnectionLog _connectionLog;
        private PhotonView _photonView;

        // Color for the local player's slot name
        private readonly Color _mySlotColor = new Color(0.7f, 0.4f, 1f);

        public ArchipelagoNotificationManager(ManualLogSource log, string localSlotName)
        {
            _log = log;
            _localSlotName = localSlotName;
        }

        /// <summary>
        /// Set the PhotonView for broadcasting notifications
        /// </summary>
        public void SetPhotonView(PhotonView photonView)
        {
            _photonView = photonView;
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
        /// Display a notification when an item is sent (LOCAL ONLY - use ShowItemNotificationBroadcast for multiplayer)
        /// Format: "fromName sent itemName to toName"
        /// </summary>
        private void ShowItemNotificationLocal(string fromName, string toName, string itemName, ItemFlags classification)
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
        /// Broadcast item notification to all clients
        /// </summary>
        public void ShowItemNotification(string fromName, string toName, string itemName, ItemFlags classification)
        {
            try
            {
                if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("ShowItemNotificationRPC", RpcTarget.All, fromName, toName, itemName, (int)classification);
                }
                else
                {
                    ShowItemNotificationLocal(fromName, toName, itemName, classification);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error broadcasting item notification: " + ex.Message);
            }
        }

        /// <summary>
        /// RPC receiver for item notifications (called by PeakArchipelagoPlugin)
        /// </summary>
        public void ReceiveItemNotificationRPC(string fromName, string toName, string itemName, int classificationInt)
        {
            ItemFlags classification = (ItemFlags)classificationInt;
            ShowItemNotificationLocal(fromName, toName, itemName, classification);
        }

        /// <summary>
        /// Display a message without player names (LOCAL ONLY)
        /// </summary>
        private void ShowSimpleMessageLocal(string message)
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

        /// <summary>
        /// Broadcast simple message to all clients
        /// </summary>
        public void ShowSimpleMessage(string message)
        {
            try
            {
                if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("ShowSimpleMessageRPC", RpcTarget.All, message);
                }
                else
                {
                    ShowSimpleMessageLocal(message);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error broadcasting simple message: " + ex.Message);
            }
        }

        /// <summary>
        /// RPC receiver for simple messages
        /// </summary>
        public void ReceiveSimpleMessageRPC(string message)
        {
            ShowSimpleMessageLocal(message);
        }

        private void ShowWarningMessageLocal(string message)
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

        public void ShowWarningMessage(string message)
        {
            try
            {
                if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("ShowWarningMessageRPC", RpcTarget.All, message);
                }
                else
                {
                    ShowWarningMessageLocal(message);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error broadcasting warning message: " + ex.Message);
            }
        }

        public void ReceiveWarningMessageRPC(string message)
        {
            ShowWarningMessageLocal(message);
        }

        private void ShowColoredMessageLocal(string message, Color color)
        {
            try
            {
                FindConnectionLog();

                if (_connectionLog == null) return;

                var addMessageMethod = _connectionLog.GetType().GetMethod("AddMessage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (addMessageMethod != null)
                {
                    string colorTag = GetColorTag(color);
                    string formattedMessage = colorTag + message + "</color>";

                    addMessageMethod.Invoke(_connectionLog, new object[] { formattedMessage });
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error displaying colored message: " + ex.Message);
            }
        }

        public void ShowColoredMessage(string message, Color color)
        {
            try
            {
                if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("ShowColoredMessageRPC", RpcTarget.All, message, color.r, color.g, color.b);
                }
                else
                {
                    ShowColoredMessageLocal(message, color);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error broadcasting colored message: " + ex.Message);
            }
        }

        public void ReceiveColoredMessageRPC(string message, float r, float g, float b)
        {
            ShowColoredMessageLocal(message, new Color(r, g, b));
        }

        private void ShowHeroTitleLocal(string message)
        {
            try
            {
                if (GUIManager.instance != null)
                {
                    GUIManager.instance.SetHeroTitle(message, null);
                }
                else
                {
                    _log.LogWarning("[PeakPelago] Cannot show hero title - GUIManager not found");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error showing hero title: {ex.Message}");
            }
        }

        public void ShowHeroTitle(string message)
        {
            try
            {
                if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("ShowHeroTitleRPC", RpcTarget.All, message);
                }
                else
                {
                    ShowHeroTitleLocal(message);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error broadcasting hero title: {ex.Message}");
            }
        }

        public void ReceiveHeroTitleRPC(string message)
        {
            ShowHeroTitleLocal(message);
        }

        private void ShowDeathLinkLocal(string cause, string source)
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
                    
                    string message = $"{deathTag}Death Link:</color> {source} {sourceTag}{cause}</color>";
                    
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

        public void ShowDeathLink(string cause, string source)
        {
            try
            {
                if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("ShowDeathLinkRPC", RpcTarget.All, cause, source);
                }
                else
                {
                    ShowDeathLinkLocal(cause, source);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error broadcasting death link: " + ex.Message);
            }
        }

        public void ReceiveDeathLinkRPC(string cause, string source)
        {
            ShowDeathLinkLocal(cause, source);
        }

        // Helper methods remain the same
        public void ShowGoalComplete()
        {
            ShowSimpleMessage("GOAL COMPLETE!");
        }

        public void ShowTrapLinkNotification(string message)
        {
            ShowColoredMessage(message, new Color(1f, 0.5f, 0f));
        }

        public void ShowDeathLinkSent(string message)
        {
            ShowColoredMessage(message, new Color(1f, 0.2f, 0.2f));
        }

        public void ShowRingLinkNotification(string message)
        {
            ShowColoredMessage(message, new Color(1f, 0.84f, 0f));
        }

        public void ShowEnergyLinkNotification(string message)
        {
            ShowColoredMessage(message, new Color(0f, 1f, 0.25f));
        }

        public void ShowConnected()
        {
            ShowSimpleMessage($"Connected to Archipelago as {_localSlotName}");
        }

        public void ShowDisconnected()
        {
            ShowWarningMessage("Disconnected from Archipelago");
        }
    }
}