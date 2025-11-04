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
        private ArchipelagoNotificationManager _notifications;

        public RingLinkService(ManualLogSource log, ArchipelagoNotificationManager notifications)
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
                _harmony = new Harmony("com.mickemoose.peak.ap.ringlink");
                _harmony.PatchAll(typeof(RingLinkPatches));
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
        /// Send a Ring Link packet when rings change (supports negative amounts)
        /// </summary>
        public void SendRingLink(int amount)
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
                    Tags = new List<string> { "RingLink" },
                    Data = ringLinkData
                };

                _session.Socket.SendPacket(bouncePacket);
                
                string ringType = amount > 0 ? "positive" : "negative";
                _log.LogInfo($"[PeakPelago] Sent Ring Link: {amount} rings ({ringType})");
                if (ringType == "positive")
                {
                    _notifications.ShowRingLinkNotification($"RingLink: Sent +{amount} ring(s)");
                }
                else
                {
                    _notifications.ShowRingLinkNotification($"RingLink: Sent -{amount} ring(s)");
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
                    string ringType = amount > 0 ? "positive" : "negative";
                    _log.LogInfo($"[PeakPelago] Ring Link received: {amount} rings ({ringType})");
                    if (ringType == "positive")
                    {
                        _notifications.ShowRingLinkNotification($"RingLink: +{amount} ring(s)!");
                    }
                    else
                    {
                        _notifications.ShowRingLinkNotification($"RingLink: -{amount} ring(s)!");
                    }
                    ApplyRingLinkEffect(amount);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to handle Ring Link: {ex.Message}");
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
                    _log.LogWarning("[PeakPelago] Cannot apply Ring Link - no characters found");
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
                    _log.LogInfo($"[PeakPelago] Ring Link {action}: {Mathf.Abs(staminaValue)} stamina (from {amount} rings)");
                }

                _log.LogInfo($"[PeakPelago] Ring Link applied to {validCharacters.Count} character(s)");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to apply Ring Link: {ex.Message}");
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

            [HarmonyPatch(typeof(Item), "Awake")]
            public static class ItemAwakePatch
            {
                static void Postfix(Item __instance)
                {
                    try
                    {
                        if (_instance == null || !_instance._isEnabled) return;

                        // Add a handler to OnConsumed event
                        __instance.OnConsumed = (System.Action)System.Delegate.Combine(
                            __instance.OnConsumed,
                            (System.Action)delegate
                            {
                                if (_instance == null || !_instance._isEnabled) return;

                                if (__instance.holderCharacter == null) return;

                                // Calculate ring value based on item (or poison)
                                int ringValue = CalculateRingValue(__instance);
                                
                                if (ringValue != 0)
                                {
                                    string ringType = ringValue > 0 ? "positive" : "negative";
                                    _instance._log.LogInfo($"[PeakPelago] Item consumed: {__instance.name}, sending {ringValue} rings ({ringType})");
                                    _instance.SendRingLink(ringValue);
                                }
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError("[PeakPelago] ItemAwake patch error: " + ex.Message);
                        }
                    }
                }

                private static int CalculateRingValue(Item item)
                {
                    string name = item.name;

                    // Check if item has poison effects - if so, return negative rings
                    bool isPoisonous = HasPoisonEffects(item);
                    
                    if (isPoisonous)
                    {
                        // Calculate poison penalty
                        float poisonPenalty = CalculatePoisonPenalty(item);
                        
                        if (poisonPenalty > 0)
                        {
                            _instance._log.LogInfo($"[PeakPelago] Item {name} is poisonous, penalty: {poisonPenalty}");
                            return -Mathf.RoundToInt(poisonPenalty * 100f);
                        }
                    }

                    float totalRings = 0f;


                    if (name.Contains("Apple Berry"))
                    {
                        totalRings += 0.1f;
                    }
                    if (name.Contains("Berrynana"))
                    {
                        totalRings += 0.2f;
                    }
                    if (name.Contains("Clusterberry"))
                    {
                        totalRings += 0.35f;
                    }
                    if (name.Contains("Kingberry"))
                    {
                        totalRings += 0.15f;
                    }
                    if (name.Contains("Marshmallow"))
                    {
                        totalRings += 0.50f;
                    }
                    if (name.Contains("Mushroom"))
                    {
                        totalRings += 0.07f;
                    }
                    if (name.Contains("Sports Drink"))
                    {
                        totalRings += 0.15f;
                    }
                    if (name.Contains("Energy Drink"))
                    {
                        totalRings += 0.25f;
                    }
                    if (name.Contains("Winterberry"))
                    {
                        totalRings += 0.35f;
                    }
                    if (name.Contains("Honeycomb"))
                    {
                        totalRings += 0.15f;
                    }
                    if (name.Contains("Coconut_half"))
                    {
                        totalRings += 0.35f;
                    }
                    if (name.Contains("AloeVera"))
                    {
                        totalRings += 0.35f;
                    }
                    if (name.Contains("Turkey"))
                    {
                        totalRings += 0.75f;
                    }
                    if (name.Contains("Napberry"))
                    {
                        totalRings += 1f;
                    }
                    if (name.Contains("Prickleberry"))
                    {
                        totalRings += 0.15f;
                    }
                    if (name.Contains("Cure-All"))
                    {
                        totalRings += 0.30f;
                    }
                    if (name.Contains("MedicinalRoot"))
                    {
                        totalRings += 0.25f;
                    }
                    if (name.Contains("Granola Bar"))
                    {
                        totalRings += 0.15f;
                    }
                    if (name.Contains("Scout Cookies") || name.Contains("ScoutCookies"))
                    {
                        totalRings += 0.3f;
                    }
                    if (name.Contains("Trail Mix") || name.Contains("TrailMix"))
                    {
                        totalRings += 0.25f;
                    }
                    if (name.Contains("Airline Food"))
                    {
                        totalRings += 0.35f;
                    }
                    if (name.Contains("Lollipop"))
                    {
                        totalRings += 0.2f;
                    }
                    if (name.Contains("Egg") && !name.Contains("Turkey"))
                    {
                        totalRings += 0.15f;
                    }

                    // Convert to rings (1.0f = 100 rings)
                    return Mathf.RoundToInt(totalRings * 100f);
                }

                private static bool HasPoisonEffects(Item item)
                {
                    try
                    {
                        // Check for Action_InflictPoison component
                        var inflictPoison = item.GetComponent<Action_InflictPoison>();
                        if (inflictPoison != null)
                        {
                            return true;
                        }

                        // Check for Action_ModifyStatus with poison
                        var modifyStatusActions = item.GetComponents<Action_ModifyStatus>();
                        if (modifyStatusActions != null)
                        {
                            foreach (var action in modifyStatusActions)
                            {
                                // STATUSTYPE.Poison = 3
                                if ((int)action.statusType == 3 && action.changeAmount > 0)
                                {
                                    return true;
                                }
                            }
                        }

                        // Check for thorns
                        var thornsAction = item.GetComponent<Action_AddOrRemoveThorns>();
                        if (thornsAction != null)
                        {
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError($"[PeakPelago] Error checking poison effects: {ex.Message}");
                        }
                    }

                    return false;
                }

                private static float CalculatePoisonPenalty(Item item)
                {
                    float penalty = 0f;

                    try
                    {
                        // Check Action_InflictPoison
                        var inflictPoison = item.GetComponent<Action_InflictPoison>();
                        if (inflictPoison != null)
                        {
                            var poisonAmountField = inflictPoison.GetType().GetField("poisonAmount",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                            if (poisonAmountField != null)
                            {
                                float poisonAmount = (float)poisonAmountField.GetValue(inflictPoison);
                                penalty += poisonAmount * 0.05f;
                            }
                            else
                            {
                                penalty += 0.05f;
                            }

                            if (penalty < 0.01f)
                            {
                                penalty = 0.05f;
                            }
                        }

                        // Check Action_ModifyStatus with poison
                        var modifyStatusActions = item.GetComponents<Action_ModifyStatus>();
                        if (modifyStatusActions != null)
                        {
                            foreach (var action in modifyStatusActions)
                            {
                                if ((int)action.statusType == 3 && action.changeAmount > 0)
                                {
                                    penalty += action.changeAmount * 0.05f;
                                }
                            }
                        }

                        var thornsAction = item.GetComponent<Action_AddOrRemoveThorns>();
                        if (thornsAction != null)
                        {
                            penalty += 0.05f;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError($"[PeakPelago] Error calculating poison penalty: {ex.Message}");
                        }
                        penalty = 0.05f;
                    }
                    
                    if (penalty < 0.01f)
                    {
                        penalty = 0.05f;
                    }

                    return penalty;
                }
            }
        }
    }
}