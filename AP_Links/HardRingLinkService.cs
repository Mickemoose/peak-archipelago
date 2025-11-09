using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Newtonsoft.Json.Linq;
using static MountainProgressHandler;

namespace Peak.AP
{
    public class HardRingLinkService
    {
        private readonly ManualLogSource _log;
        private ArchipelagoSession _session;
        private bool _isEnabled;
        private int _connectionId;
        private Harmony _harmony;
        private ArchipelagoNotificationManager _notifications;

        public HardRingLinkService(ManualLogSource log, ArchipelagoNotificationManager notifications)
        {
            _log = log;
            _connectionId = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            _notifications = notifications;
        }

        /// <summary>
        /// Initialize the Ring Link service with an Archipelago session
        /// </summary>
        public void Initialize(ArchipelagoSession session, bool enabled)
        {
            _session = session;
            _isEnabled = enabled;

            if (_session != null && _isEnabled)
            {
                _session.Socket.PacketReceived += OnPacketReceived;
                _harmony = new Harmony("com.mickemoose.peak.ap.hardringlink");
                _harmony.PatchAll(typeof(HardRingLinkPatches));
                HardRingLinkPatches.SetInstance(this);

                _log.LogInfo($"[PeakPelago] Hard Ring Link service initialized (Connection ID: {_connectionId})");
            }
        }

        /// <summary>
        /// Enable or disable Ring Link
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            _log.LogInfo($"[PeakPelago] Hard Ring Link {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Send a Ring Link packet when rings change (supports negative amounts)
        /// </summary>
        public void SendHardRingLink(int amount)
        {
            if (_session == null || !_isEnabled)
            {
                return;
            }

            try
            {
                var ringLinkData = new Dictionary<string, JToken>
                {
                    { "time", JToken.FromObject(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) },
                    { "source", JToken.FromObject(_connectionId) },
                    { "amount", JToken.FromObject(amount) }
                };

                var bouncePacket = new BouncePacket
                {
                    Tags = new List<string> { "HardRingLink" },
                    Data = ringLinkData
                };

                _session.Socket.SendPacket(bouncePacket);
                
                string ringType = amount > 0 ? "positive" : "negative";
                _log.LogInfo($"[PeakPelago] Sent Hard Ring Link: {amount} rings ({ringType})");
                if (ringType == "positive")
                {
                    _notifications.ShowRingLinkNotification($"HardRingLink: Sent +{amount} ring(s)");
                }
                else
                {
                    _notifications.ShowRingLinkNotification($"HardRingLink: Sent -{amount} ring(s)");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to send Ring Link: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle incoming Archipelago packets
        /// </summary>
        private void OnPacketReceived(ArchipelagoPacketBase packet)
        {
            try
            {
                if (packet is BouncePacket bounce)
                {
                    if (bounce.Tags != null && bounce.Tags.Contains("HardRingLink") && _isEnabled)
                    {
                        HandleHardRingLinkReceived(bounce.Data);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error handling Ring Link packet: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle incoming Ring Link packets
        /// </summary>
        private void HandleHardRingLinkReceived(Dictionary<string, JToken> data)
        {
            try
            {
                string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (!currentScene.StartsWith("Level_"))
                {
                    return;
                }
                // Don't process our own Ring Links
                if (data.ContainsKey("source"))
                {
                    int source = data["source"].ToObject<int>();
                    if (source == _connectionId)
                    {
                        _log.LogDebug("[PeakPelago] Ignoring own Hard Ring Link");
                        return;
                    }
                }

                if (data.ContainsKey("amount"))
                {
                    int amount = data["amount"].ToObject<int>();
                    string ringType = amount > 0 ? "positive" : "negative";
                    _log.LogInfo($"[PeakPelago] Hard Ring Link received: {amount} rings ({ringType})");
                    if (ringType == "positive")
                    {
                        _notifications.ShowRingLinkNotification($"HardRingLink: +{amount} ring(s)!");
                    }
                    else
                    {
                        _notifications.ShowRingLinkNotification($"HardRingLink: -{amount} ring(s)!");
                    }
                    ApplyRingLinkEffect(amount);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to handle Hard Ring Link: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply Ring Link effect to all characters in the lobby
        /// </summary>
        private void ApplyRingLinkEffect(int amount)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    _log.LogWarning("[PeakPelago] Cannot apply Hard Ring Link - no characters found");
                    return;
                }

                // Convert rings to stamina value (100 rings = 1.0f stamina)
                float staminaValue = amount / 100f;

                // Apply to all valid characters
                var validCharacters = Character.AllCharacters.Where(c => 
                    c != null && 
                    c.gameObject.activeInHierarchy && 
                    !c.data.dead
                ).ToList();

                foreach (var character in validCharacters)
                {
                    if (staminaValue > 0)
                    {
                        // Positive: Add to extra stamina
                        character.data.extraStamina += staminaValue;
                    }
                    else if (staminaValue < 0)
                    {
                        float remainingPenalty = Mathf.Abs(staminaValue);
                        
                        // Negative: Deduct from extra stamina first
                        if (character.data.extraStamina > 0)
                        {
                            float deduction = Mathf.Min(character.data.extraStamina, remainingPenalty);
                            character.data.extraStamina -= deduction;
                            remainingPenalty -= deduction;
                        }
                        
                        // If there's still penalty left, deduct from regular stamina
                        if (remainingPenalty > 0)
                        {
                            character.data.staminaDelta = Mathf.Max(0f, character.data.staminaDelta - remainingPenalty);
                        }
                    }
                    
                    string action = amount > 0 ? "added" : "deducted";
                    _log.LogInfo($"[PeakPelago] Hard Ring Link {action}: {Mathf.Abs(staminaValue)} stamina (from {amount} rings)");
                }

                _log.LogInfo($"[PeakPelago] Hard Ring Link applied to {validCharacters.Count} character(s)");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to apply Hard Ring Link: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean up when disconnecting
        /// </summary>
        public void Cleanup()
        {
            if (_session != null)
            {
                _session.Socket.PacketReceived -= OnPacketReceived;
            }
            
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            HardRingLinkPatches.SetInstance(null);
            
            _session = null;
            _isEnabled = false;
        }

        /// <summary>
        /// Harmony patches for things that trigger HARD RING LINK events
        /// </summary>
        private static class HardRingLinkPatches
        {
            private static HardRingLinkService _instance;

            public static void SetInstance(HardRingLinkService instance)
            {
                _instance = instance;
            }

            [HarmonyPatch(typeof(MountainProgressHandler), "CheckAreaAchievement")]
            public static class MountainProgressPeakReachedPatch
            {
                static void Postfix(ProgressPoint pointReached)
                {
                    try
                    {
                        if (_instance == null || !_instance._isEnabled) return;
                        string peakName = "Unknown";
                        if (pointReached != null)
                        {
                            var titleField = pointReached.GetType().GetField("title");
                            if (titleField != null)
                            {
                                peakName = (string)titleField.GetValue(pointReached) ?? "Unknown";
                            }
                        }

                        _instance._log.LogInfo($"[PeakPelago] Player reached peak: {peakName}");
                        int ringAmount = 0;
                        if (peakName.ToUpper() == "PEAK")
                        {
                            ringAmount = 200;
                            _instance._log.LogInfo($"[PeakPelago] Final PEAK reached, sending +{ringAmount} rings via Hard Ring Link");
                        }
                        else if (peakName.ToUpper() == "SHORE")
                        {
                            ringAmount = 25;
                            _instance._log.LogInfo($"[PeakPelago] SHORE reached, sending +{ringAmount} rings via Hard Ring Link");
                        }
                        else if (!string.IsNullOrEmpty(peakName) && peakName.ToUpper() != "UNKNOWN")
                        {
                            ringAmount = 100;
                            _instance._log.LogInfo($"[PeakPelago] Peak '{peakName}' reached, sending +{ringAmount} rings via Hard Ring Link");
                        }

                        if (ringAmount > 0)
                        {
                            _instance.SendHardRingLink(ringAmount);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError($"[PeakPelago] MountainProgressPeakReachedPatch error: {ex.Message}");
                        }
                    }
                }
            }

            [HarmonyPatch(typeof(Character), "RPCA_Die")]
            public static class CharacterDeathPatch
            {
                static void Postfix(Character __instance)
                {
                    try
                    {
                        if (_instance == null || !_instance._isEnabled) return;

                        // Don't send rings if dying from DeathLink (to avoid loops)
                        if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance._isDyingFromDeathLink)
                        {
                            _instance._log.LogDebug("[PeakPelago] Death was from DeathLink, not sending Hard Ring Link");
                            return;
                        }

                        string characterName = __instance.characterName ?? "Unknown";
                        _instance._log.LogInfo($"[PeakPelago] Character died: {characterName}, sending -75 rings via Hard Ring Link");
                        _instance.SendHardRingLink(-75);
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError($"[PeakPelago] CharacterDeathPatch error: {ex.Message}");
                        }
                    }
                }
            }

            [HarmonyPatch(typeof(Character), "FinishZombifying")]
            public static class CharacterZombifyPatch
            {
                static void Postfix(Character __instance)
                {
                    try
                    {
                        if (_instance == null || !_instance._isEnabled) return;

                        // Don't send rings if zombifying from DeathLink (to avoid loops)
                        if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance._isDyingFromDeathLink)
                        {
                            _instance._log.LogDebug("[PeakPelago] Zombification was from DeathLink, not sending Hard Ring Link");
                            return;
                        }

                        string characterName = __instance.characterName ?? "Unknown";
                        _instance._log.LogInfo($"[PeakPelago] Character zombified: {characterName}, sending -75 rings via Hard Ring Link");
                        _instance.SendHardRingLink(-75);
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError($"[PeakPelago] CharacterZombifyPatch error: {ex.Message}");
                        }
                    }
                }
            }

            [HarmonyPatch(typeof(Scoutmaster), "SetCurrentTarget")]
            public static class ScoutmasterSetTargetPatch
            {
                private static bool _hasSpawnedThisSession = false;
                
                static void Postfix(Scoutmaster __instance, Character setCurrentTarget)
                {
                    try
                    {
                        if (_instance == null || !_instance._isEnabled) return;

                        // Only trigger on the first time Scoutmaster gets a target (spawns/activates) otherwise it would murder hard ring linkers lmao
                        if (setCurrentTarget != null && !_hasSpawnedThisSession)
                        {
                            _hasSpawnedThisSession = true;
                            _instance._log.LogInfo($"[PeakPelago] Scoutmaster spawned/activated (targeting {setCurrentTarget.characterName}), sending -45 rings via Hard Ring Link");
                            _instance.SendHardRingLink(-45);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError($"[PeakPelago] ScoutmasterSetTargetPatch error: {ex.Message}");
                        }
                    }
                }
                
                [HarmonyPatch(typeof(Scoutmaster), "OnDisable")]
                public static class ScoutmasterOnDisablePatch
                {
                    static void Postfix()
                    {
                        _hasSpawnedThisSession = false;
                    }
                }
            }

            [HarmonyPatch(typeof(ItemCooking), "Wreck")]
            public static class ItemCookingWreckPatch
            {
                static void Postfix(ItemCooking __instance)
                {
                    try
                    {
                        if (_instance == null || !_instance._isEnabled) return;

                        if (__instance.item != null && __instance.item.holderCharacter != null)
                        {
                            string itemName = __instance.item.name ?? "Unknown";
                            string characterName = __instance.item.holderCharacter.characterName ?? "Unknown";
                            
                            _instance._log.LogInfo($"[PeakPelago] Item wrecked from cooking: {itemName} (held by {characterName}), sending -15 rings via Hard Ring Link");
                            _instance.SendHardRingLink(-15);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError($"[PeakPelago] ItemCookingWreckPatch error: {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}