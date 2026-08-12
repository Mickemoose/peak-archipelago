using System;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Peak.AP
{
    public class RingLinkService : RingLinkServiceBase
    {
        public RingLinkService(ManualLogSource log, ArchipelagoNotificationManager notifications)
            : base(log, notifications)
        {
        }

        protected override string Tag => "RingLink";
        protected override string DisplayName => "Ring Link";
        protected override string HarmonyId => "com.mickemoose.peak.ap.ringlink";
        protected override string SendRpcName => "RPC_SendRingLink";

        protected override void ApplyHarmonyPatches() => _harmony.PatchAll(typeof(RingLinkPatches));
        protected override void SetPatchInstance() => RingLinkPatches.SetInstance(this);
        protected override void ClearPatchInstance() => RingLinkPatches.SetInstance(null);

        /// <summary>
        /// Send a Ring Link packet when rings change (supports negative amounts)
        /// </summary>
        public void SendRingLink(int amount) => SendRingLinkInternal(amount);

        /// <summary>
        /// Apply Ring Link effect to all characters in the lobby
        /// </summary>
        protected override void ApplyRingLinkEffect(int amount)
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
                        // Positive: Add to extra stamina, but cap at max (1.0)
                        character.data.extraStamina = Mathf.Min(character.data.extraStamina + staminaValue, 1f);

                        _log.LogInfo($"[PeakPelago] Ring Link added: {staminaValue} stamina (from {amount} rings), capped at 1.0");
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
                            character.data.currentStamina = Mathf.Max(0f, character.data.currentStamina - remainingPenalty);
                        }

                        _log.LogInfo($"[PeakPelago] Ring Link deducted: {Mathf.Abs(staminaValue)} stamina (from {amount} rings)");
                    }

                    // Ensure extra stamina stays within bounds
                    character.data.extraStamina = Mathf.Clamp(character.data.extraStamina, 0f, 1f);
                }

                _log.LogInfo($"[PeakPelago] Ring Link applied to {validCharacters.Count} character(s)");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to apply Ring Link: {ex.Message}");
            }
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
                        __instance.OnConsumed = (Action)Delegate.Combine(
                            __instance.OnConsumed,
                            (Action)delegate
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
                    if (name.Contains("Winterberry") || name.Contains("Shroomberry"))
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
                    if (name.Contains("Glizzy"))
                    {
                        totalRings += 0.69f;
                    }
                    if (name.Contains("Milk"))
                    {
                        totalRings += 0.40f;
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
