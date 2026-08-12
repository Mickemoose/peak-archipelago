using System;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using static MountainProgressHandler;

namespace Peak.AP
{
    public class HardRingLinkService : RingLinkServiceBase
    {
        public HardRingLinkService(ManualLogSource log, ArchipelagoNotificationManager notifications)
            : base(log, notifications)
        {
        }

        protected override string Tag => "HardRingLink";
        protected override string DisplayName => "Hard Ring Link";
        protected override string HarmonyId => "com.mickemoose.peak.ap.hardringlink";
        protected override string SendRpcName => "RPC_SendHardRingLink";

        protected override void ApplyHarmonyPatches() => _harmony.PatchAll(typeof(HardRingLinkPatches));
        protected override void SetPatchInstance() => HardRingLinkPatches.SetInstance(this);
        protected override void ClearPatchInstance() => HardRingLinkPatches.SetInstance(null);

        /// <summary>
        /// Send a Ring Link packet when rings change (supports negative amounts)
        /// </summary>
        public void SendHardRingLink(int amount) => SendRingLinkInternal(amount);

        /// <summary>
        /// Apply Ring Link effect to all characters in the lobby
        /// </summary>
        protected override void ApplyRingLinkEffect(int amount)
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
                        // Positive: Add to extra stamina, capped at 1.0
                        character.data.extraStamina = Mathf.Min(character.data.extraStamina + staminaValue, 1f);
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
                    }

                    // Ensure extra stamina stays within bounds
                    character.data.extraStamina = Mathf.Clamp(character.data.extraStamina, 0f, 1f);

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
