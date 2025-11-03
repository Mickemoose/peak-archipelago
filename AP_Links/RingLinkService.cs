using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Peak.AP
{
    public class RingLinkService
    {
        private readonly ManualLogSource _log;
        private ArchipelagoSession _session;
        private bool _isEnabled;
        private int _connectionId;
        private Harmony _harmony;

        public RingLinkService(ManualLogSource log)
        {
            _log = log;
            _connectionId = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
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
                
                // Apply Harmony patches for item consumption tracking
                _harmony = new Harmony("com.mickemoose.peak.ap.ringlink");
                _harmony.PatchAll(typeof(RingLinkPatches));
                
                // Set static instance for patches
                RingLinkPatches.SetInstance(this);
                
                _log.LogInfo($"[PeakPelago] Ring Link service initialized (Connection ID: {_connectionId})");
            }
        }

        /// <summary>
        /// Enable or disable Ring Link
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            _log.LogInfo($"[PeakPelago] Ring Link {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Send a Ring Link packet when rings change
        /// </summary>
        public void SendRingLink(int amount)
        {
            if (_session == null || !_isEnabled || amount == 0)
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
                    Tags = new List<string> { "RingLink" },
                    Data = ringLinkData
                };

                _session.Socket.SendPacket(bouncePacket);
                _log.LogInfo($"[PeakPelago] Sent Ring Link: {amount} rings");
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
                    if (bounce.Tags != null && bounce.Tags.Contains("RingLink") && _isEnabled)
                    {
                        HandleRingLinkReceived(bounce.Data);
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
        private void HandleRingLinkReceived(Dictionary<string, JToken> data)
        {
            try
            {
                // Don't process our own Ring Links
                if (data.ContainsKey("source"))
                {
                    int source = data["source"].ToObject<int>();
                    if (source == _connectionId)
                    {
                        _log.LogDebug("[PeakPelago] Ignoring own Ring Link");
                        return;
                    }
                }

                if (data.ContainsKey("amount"))
                {
                    int amount = data["amount"].ToObject<int>();
                    
                    _log.LogInfo($"[PeakPelago] Ring Link received: {amount} rings");
                    
                    // Apply the healing directly
                    ApplyRingLinkHealing(amount);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to handle Ring Link: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply Ring Link healing to the local character
        /// </summary>
        private void ApplyRingLinkHealing(int amount)
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Cannot apply Ring Link - no local character");
                    return;
                }

                // Convert rings back to healing value (100 rings = 1.0f healing)
                float healingValue = amount / 100f;

                if (healingValue > 0)
                {
                    var afflictions = Character.localCharacter.refs.afflictions;
                    if (afflictions != null)
                    {
                        Character.localCharacter.data.staminaDelta = Mathf.Min(
                            Character.localCharacter.data.staminaDelta + healingValue,
                            Character.localCharacter.GetMaxStamina()
                        );

                        var hungerField = afflictions.GetType().GetField("hunger", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (hungerField != null)
                        {
                            float currentHunger = (float)hungerField.GetValue(afflictions);
                            float newHunger = Mathf.Max(0f, currentHunger - healingValue);
                            hungerField.SetValue(afflictions, newHunger);
                        }

                        _log.LogInfo($"[PeakPelago] Applied Ring Link healing: {healingValue} (from {amount} rings)");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to apply Ring Link healing: {ex.Message}");
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

            RingLinkPatches.SetInstance(null);
            
            _session = null;
            _isEnabled = false;
        }

        /// <summary>
        /// Harmony patches for item consumption tracking
        /// </summary>
        private static class RingLinkPatches
        {
            private static RingLinkService _instance;

            public static void SetInstance(RingLinkService instance)
            {
                _instance = instance;
            }

            // Patch for when items are consumed
            [HarmonyPatch(typeof(Item), "OnConsume")]
            public static class ItemOnConsumePatch
            {
                static void Prefix(Item __instance)
                {
                    try
                    {
                        if (_instance == null || !_instance._isEnabled) return;

                        // Only track local character's consumption
                        if (Character.localCharacter == null) return;
                        if (__instance.holderCharacter != Character.localCharacter) return;

                        // Calculate total healing value
                        float totalHealingValue = 0f;

                        // Get stamina healing value
                        var staminaHealField = __instance.GetType().GetField("staminaHeal", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (staminaHealField != null)
                        {
                            float staminaHeal = (float)staminaHealField.GetValue(__instance);
                            totalHealingValue += staminaHeal;
                        }

                        // Get hunger healing value (if it exists)
                        var hungerHealField = __instance.GetType().GetField("hungerHeal", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (hungerHealField != null)
                        {
                            float hungerHeal = (float)hungerHealField.GetValue(__instance);
                            totalHealingValue += hungerHeal;
                        }

                        // Convert to rings (1.0f = 100 rings)
                        int ringValue = Mathf.RoundToInt(totalHealingValue * 100f);

                        if (ringValue > 0)
                        {
                            _instance._log.LogInfo($"[PeakPelago] Item consumed: {__instance.name}, healing value: {totalHealingValue}, rings: {ringValue}");
                            _instance.SendRingLink(ringValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError("[PeakPelago] ItemOnConsume patch error: " + ex.Message);
                        }
                    }
                }
            }

            [HarmonyPatch(typeof(Character), "ConsumeItem")]
            public static class CharacterConsumeItemPatch
            {
                static void Postfix(Character __instance, Item item, float staminaHealed, float hungerHealed)
                {
                    try
                    {
                        if (_instance == null || !_instance._isEnabled) return;
                        if (!__instance.IsLocal) return;

                        // Calculate total healing
                        float totalHealing = staminaHealed + hungerHealed;
                        int ringValue = Mathf.RoundToInt(totalHealing * 100f);

                        if (ringValue > 0)
                        {
                            _instance._log.LogInfo($"[PeakPelago] Consumed {item?.name ?? "item"}, healing: {totalHealing}, rings: {ringValue}");
                            _instance.SendRingLink(ringValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError("[PeakPelago] ConsumeItem patch error: " + ex.Message);
                        }
                    }
                }
            }
        }
    }
}