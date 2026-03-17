using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;
using Zorro.Core;
using Archipelago.MultiClient.Net;

namespace Peak.AP
{
    public class ProgressiveStaminaManager
    {
        private readonly ManualLogSource _log;
        private const string STAMINA_KEY = "AP_Stamina";
        private bool _progressiveStaminaEnabled = false;
        private bool _additionalBarsEnabled = false;
        
        // Store the loaded stamina value to apply later when Photon is ready
        private float? _pendingStaminaLoad = null;
        
        // NEW: Track local state to avoid Photon sync delays
        public float _localBaseMaxStamina = 0.25f;
        public int _localStaminaUpgrades = 0;

        public ProgressiveStaminaManager(ManualLogSource log)
        {
            _log = log;
        }

        public void Initialize(bool progressiveStaminaEnabled, bool additionalBarsEnabled)
        {
            _progressiveStaminaEnabled = progressiveStaminaEnabled;
            _additionalBarsEnabled = additionalBarsEnabled;

            // Check if we have a pending stamina load from the state file
            if (_pendingStaminaLoad.HasValue)
            {
                _log.LogInfo($"[PeakPelago] Applying pending stamina load: {_pendingStaminaLoad.Value:F2}");
                if (PhotonNetwork.LocalPlayer != null)
                {
                    SetPlayerStamina(PhotonNetwork.LocalPlayer, _pendingStaminaLoad.Value);
                    _pendingStaminaLoad = null; 
                    return;
                }
                else
                {
                    _log.LogWarning("[PeakPelago] Cannot apply pending load - Photon not connected yet");
                    return;
                }
            }

            // Check if player already has stamina set from a previous load
            if (PhotonNetwork.LocalPlayer != null)
            {
                if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(STAMINA_KEY, out object existingStamina))
                {
                    float existing = (float)existingStamina;
                    // Only preserve if it's a VALID stamina value (0.25 or higher)
                    // Don't preserve default/uninitialized values (0.0 or 1.0)
                    if (existing >= 0.25f && existing != 1.0f)
                    {
                        _log.LogInfo($"[PeakPelago] Player already has valid stamina: {existing:F2} - preserving it");
                        // Update local state to match
                        _localBaseMaxStamina = existing;
                        _localStaminaUpgrades = Mathf.RoundToInt((existing - 0.25f) / 0.25f);
                        return;
                    }
                }
            }

            if (_progressiveStaminaEnabled)
            {
                _log.LogInfo("[PeakPelago] Progressive Stamina ENABLED - base max stamina set to 0.25");

                // Initialize local state
                _localBaseMaxStamina = 0.25f;
                _localStaminaUpgrades = 0;

                // Set our local player's stamina property
                if (PhotonNetwork.LocalPlayer != null)
                {
                    SetPlayerStamina(PhotonNetwork.LocalPlayer, 0.25f);
                }
            }
            else
            {
                _log.LogInfo("[PeakPelago] Progressive Stamina DISABLED - using normal 1.0 max stamina");
                
                // Reset to default
                _localBaseMaxStamina = 1.0f;
                _localStaminaUpgrades = 0;
                
                if (PhotonNetwork.LocalPlayer != null)
                {
                    SetPlayerStamina(PhotonNetwork.LocalPlayer, 1.0f);
                }
            }
        }

        private void SetPlayerStamina(Photon.Realtime.Player player, float baseMax)
        {
            Hashtable props = new Hashtable();
            props[STAMINA_KEY] = baseMax;
            player.SetCustomProperties(props);
            
            // Update local state immediately
            if (player == PhotonNetwork.LocalPlayer)
            {
                _localBaseMaxStamina = baseMax;
                _localStaminaUpgrades = Mathf.RoundToInt((baseMax - 0.25f) / 0.25f);
            }
            
            _log.LogInfo($"[PeakPelago] Set stamina for player {player.ActorNumber} to {baseMax}");
        }

        public void ApplyStaminaUpgrade()
        {
            if (!_progressiveStaminaEnabled)
            {
                _log.LogWarning("[PeakPelago] Cannot apply stamina upgrade - progressive stamina is disabled");
                return;
            }

            if (PhotonNetwork.LocalPlayer == null) return;

            int maxUpgrades = _additionalBarsEnabled ? 7 : 4;
            
            // Use local state instead of reading from Photon
            if (_localStaminaUpgrades >= maxUpgrades)
            {
                _log.LogInfo($"[PeakPelago] Already at max stamina upgrades ({maxUpgrades})");
                return;
            }

            // Update local state first
            _localStaminaUpgrades++;
            _localBaseMaxStamina += 0.25f;
            
            // Then sync to Photon (this may take time)
            SetPlayerStamina(PhotonNetwork.LocalPlayer, _localBaseMaxStamina);
            
            _log.LogInfo($"[PeakPelago] Applied stamina upgrade: now at {_localStaminaUpgrades} upgrades, {_localBaseMaxStamina} base max");
        }

        public float GetPlayerStamina(Photon.Realtime.Player player)
        {
            // For local player, use local state (most up-to-date)
            if (player == PhotonNetwork.LocalPlayer && _progressiveStaminaEnabled)
            {
                return _localBaseMaxStamina;
            }
            
            // For other players, read from Photon
            if (player == null || player.CustomProperties == null)
            {
                return _progressiveStaminaEnabled ? 0.25f : 1.0f;
            }

            if (player.CustomProperties.TryGetValue(STAMINA_KEY, out object staminaObj) && staminaObj is float stamina)
            {
                return stamina;
            }

            return _progressiveStaminaEnabled ? 0.25f : 1.0f;
        }

        public float GetBaseMaxStamina(Character character)
        {
            if (!_progressiveStaminaEnabled) return 1.0f;
            
            if (character == null || character.photonView == null || character.photonView.Owner == null)
            {
                return 0.25f;
            }

            return GetPlayerStamina(character.photonView.Owner);
        }

        public float GetBaseMaxStamina(int actorNumber)
        {
            if (!_progressiveStaminaEnabled) return 1.0f;

            Photon.Realtime.Player player = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == actorNumber);
            return GetPlayerStamina(player);
        }

        public float GetBaseMaxStamina()
        {
            if (Character.observedCharacter != null)
            {
                return GetBaseMaxStamina(Character.observedCharacter);
            }
            return _progressiveStaminaEnabled ? _localBaseMaxStamina : 1.0f;
        }

        public float GetEffectiveMaxStamina()
        {
            if (!_progressiveStaminaEnabled)
            {
                if (Character.observedCharacter != null)
                {
                    return Mathf.Max(1.0f - Character.observedCharacter.refs.afflictions.statusSum, 0f);
                }
                return 1.0f;
            }
            
            if (Character.observedCharacter == null)
            {
                return 0.25f;
            }
            
            float baseMax = GetBaseMaxStamina(Character.observedCharacter);
            float statusSum = Character.observedCharacter.refs.afflictions.statusSum;
            return Mathf.Max(baseMax - statusSum, 0f);
        }

        public int GetStaminaUpgradesReceived()
        {
            if (!_progressiveStaminaEnabled) return 0;
            
            // Return local state (most up-to-date)
            return _localStaminaUpgrades;
        }

        public bool IsProgressiveStaminaEnabled()
        {
            return _progressiveStaminaEnabled;
        }

        public void UpdateCharacterStamina()
        {
            if (Character.localCharacter != null)
            {
                float baseMax = GetBaseMaxStamina(Character.localCharacter);
                float statusSum = Character.localCharacter.refs.afflictions.statusSum;
                float effectiveMax = Mathf.Max(baseMax - statusSum, 0f);

                Character.localCharacter.data.currentStamina = Mathf.Min(Character.localCharacter.data.currentStamina, effectiveMax);

                if (GUIManager.instance != null && GUIManager.instance.bar != null)
                {
                    GUIManager.instance.bar.ChangeBar();
                }
            }
        }

        public void SyncWithArchipelago(ArchipelagoSession session)
        {
            if (!_progressiveStaminaEnabled || session == null) return;
            
            int staminaItems = session.Items.AllItemsReceived.Count(item =>
            {
                string itemName = session.Items.GetItemName(item.ItemId, item.ItemGame);
                return itemName?.Equals("Progressive Stamina Bar", StringComparison.OrdinalIgnoreCase) ?? false;
            });
            
            float correctStamina = 0.25f + (staminaItems * 0.25f);
            
            _localBaseMaxStamina = correctStamina;
            _localStaminaUpgrades = staminaItems;
            
            if (PhotonNetwork.LocalPlayer != null)
            {
                Hashtable props = new Hashtable();
                props["AP_Stamina"] = correctStamina;
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                _log.LogInfo($"[PeakPelago] Synced stamina to {correctStamina} ({staminaItems} items)");
            }
        }

        public string SaveState()
        {
            // Use local state (always accurate)
            return $"{_localStaminaUpgrades},{_localBaseMaxStamina:F2}";
        }

        public void LoadState(string stateData)
        {
            if (string.IsNullOrEmpty(stateData)) return;
            if (!PhotonNetwork.IsMasterClient)
            {
                _log.LogDebug("[PeakPelago] Skipping load save - is not master client");
                return;
            }
            try
            {
                var parts = stateData.Split(',');
                if (parts.Length >= 2)
                {
                    int upgrades = int.Parse(parts[0]);
                    float stamina = float.Parse(parts[1]);
                    
                    // Update local state immediately
                    _localStaminaUpgrades = upgrades;
                    _localBaseMaxStamina = stamina;
                    
                    // Try to apply to Photon if ready, otherwise store it
                    if (PhotonNetwork.LocalPlayer != null)
                    {
                        SetPlayerStamina(PhotonNetwork.LocalPlayer, stamina);
                        _log.LogInfo($"[PeakPelago] Loaded stamina state: {upgrades} upgrades, {stamina:F2} max");
                    }
                    else
                    {
                        _pendingStaminaLoad = stamina;
                        _log.LogInfo($"[PeakPelago] Stored pending stamina load: {upgrades} upgrades, {stamina:F2} max (will apply when Photon connects)");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to load stamina state: {ex.Message}");
            }
        }
    }

    // Keep all your existing Harmony patches - they stay the same
    [HarmonyPatch(typeof(BarAffliction), "ChangeAffliction")]
    public static class BarAfflictionChangeAfflictionPatch
    {
        private static ProgressiveStaminaManager _staminaManager;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
        }

        static void Postfix(BarAffliction __instance, StaminaBar bar)
        {
            try
            {
                if (_staminaManager == null || !_staminaManager.IsProgressiveStaminaEnabled())
                {
                    return;
                }
                if (Character.observedCharacter == null)
                {
                    return;
                }
                float currentStatus = Character.observedCharacter.refs.afflictions.GetCurrentStatus(__instance.afflictionType);
                __instance.size = bar.fullBar.sizeDelta.x * currentStatus;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] BarAffliction ChangeAffliction patch error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(BarAffliction), "UpdateAffliction")]
    public static class BarAfflictionUpdateAfflictionPatch
    {
        private static ProgressiveStaminaManager _staminaManager;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
        }

        static bool Prefix(BarAffliction __instance, StaminaBar bar)
        {
            try
            {
                if (_staminaManager == null || !_staminaManager.IsProgressiveStaminaEnabled())
                {
                    return true;
                }
                if (Character.observedCharacter == null)
                {
                    return true;
                }
                float currentStatus = Character.observedCharacter.refs.afflictions.GetCurrentStatus(__instance.afflictionType);
                if (currentStatus <= 0f)
                {
                    __instance.gameObject.SetActive(false);
                    return false;
                }

                __instance.gameObject.SetActive(true);
                // Let the vanilla method handle the width lerp and let Unity's layout system handle positioning
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] BarAffliction UpdateAffliction patch error: {ex.Message}");
                return true;
            }
        }
        
    }

    

    [HarmonyPatch(typeof(Character), "GetMaxStamina")]
    public static class CharacterGetMaxStaminaPatch
    {
        private static ProgressiveStaminaManager _staminaManager;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
        }

        static bool Prefix(Character __instance, ref float __result)
        {
            try
            {
                if (_staminaManager != null && _staminaManager.IsProgressiveStaminaEnabled())
                {
                    float baseMax = _staminaManager.GetBaseMaxStamina(__instance);
                    float statusSum = __instance.refs.afflictions.statusSum;
                    __result = Mathf.Max(baseMax - statusSum, 0f);
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] GetMaxStamina patch error: {ex.Message}");
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Character), "ClampStamina")]
    public static class CharacterClampStaminaPatch
    {
        private static ProgressiveStaminaManager _staminaManager;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
        }

        static void Postfix(Character __instance)
        {
            try
            {
                if (_staminaManager != null && _staminaManager.IsProgressiveStaminaEnabled())
                {
                    float baseMax = _staminaManager.GetBaseMaxStamina(__instance);
                    float statusSum = __instance.refs.afflictions.statusSum;
                    float effectiveMax = Mathf.Max(baseMax - statusSum, 0f);
                    __instance.data.currentStamina = Mathf.Clamp(__instance.data.currentStamina, 0f, effectiveMax);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] ClampStamina patch error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Character), "HandlePassedOut")]
    public static class CharacterHandlePassedOutPatch
    {
        private static ProgressiveStaminaManager _staminaManager;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
        }

        static bool Prefix(Character __instance)
        {
            try
            {
                if (_staminaManager == null || !_staminaManager.IsProgressiveStaminaEnabled())
                    return true;

                float baseMaxStamina = _staminaManager.GetBaseMaxStamina(__instance);
                float statusSum = __instance.refs.afflictions.statusSum;
                bool shouldBePassedOut = statusSum >= baseMaxStamina;

                if (!shouldBePassedOut)
                    return true;

                if (__instance.data.deathTimer > 1f)
                {
                    __instance.refs.items.EquipSlot(Optionable<byte>.None);
                    if (!__instance.TryCheckpoint())
                    {
                        if (__instance.refs.afflictions.willZombify && !__instance.data.zombified)
                        {
                            if (!PhotonNetwork.IsMasterClient)
                                __instance.data.zombified = true;
                            __instance.photonView.RPC("RPCA_Zombify", RpcTarget.MasterClient,
                                __instance.Center + Vector3.up * 0.2f + Vector3.forward * 0.1f);
                        }
                        else
                        {
                            __instance.photonView.RPC("RPCA_Die", RpcTarget.All,
                                __instance.Center + Vector3.up * 0.2f + Vector3.forward * 0.1f);
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] HandlePassedOut patch error: {ex.Message}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(Character), "HandleLife")]
    public static class CharacterHandleLifePatch
    {
        private static ProgressiveStaminaManager _staminaManager;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
        }

        static bool Prefix(Character __instance)
        {
            try
            {
                if (_staminaManager == null || !_staminaManager.IsProgressiveStaminaEnabled())
                {
                    return true;
                }

                float baseMaxStamina = _staminaManager.GetBaseMaxStamina(__instance);
                float statusSum = __instance.refs.afflictions.statusSum;

                bool shouldPassOut = statusSum >= baseMaxStamina;

                if (__instance.data.isSkeleton)
                {
                    if (shouldPassOut)
                    {
                        if (!__instance.TryCheckpoint())
                        {
                            __instance.photonView.RPC("RPCA_Die", RpcTarget.All,
                                __instance.Center + Vector3.up * 0.2f + Vector3.forward * 0.1f);
                        }
                    }
                }
                else
                {
                    if (shouldPassOut)
                    {
                        if (!__instance.data.fullyPassedOut)
                        {
                            __instance.data.passOutValue = Mathf.MoveTowards(__instance.data.passOutValue, 1f, Time.deltaTime / 5f);
                            if (__instance.data.passOutValue > 0.999f)
                            {
                                __instance.photonView.RPC("RPCA_PassOut", RpcTarget.All);
                            }
                        }
                    }
                    else if (!__instance.data.fullyPassedOut)
                    {
                        __instance.data.passOutValue = Mathf.MoveTowards(__instance.data.passOutValue, 0f, Time.deltaTime / 5f);
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] HandleLife patch error: {ex.Message}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(CharacterAfflictions), "AddStatus")]
    public static class CharacterAfflictionsAddStatusPatch
    {
        private static ProgressiveStaminaManager _staminaManager;
        private static FieldInfo _statusArrayField;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
        }

        static void Prefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, out float __state)
        {
            __state = __instance.GetCurrentStatus(statusType);
        }

        static void Postfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, float __state)
        {
            try
            {
                if (_staminaManager == null || !_staminaManager.IsProgressiveStaminaEnabled())
                    return;

                float baseMax = _staminaManager.GetBaseMaxStamina(__instance.character);
                if (baseMax <= 1.0f) return;

                // Vanilla blocks all status additions (except Curse) when invincible.
                // Without this check, we'd misinterpret the blocked add as vanilla clamping
                // and force-set the value anyway.
                if (__instance.character.data.isInvincible && statusType != CharacterAfflictions.STATUSTYPE.Curse)
                    return;

                float afterAdd = __instance.GetCurrentStatus(statusType);
                float desired = __state + amount;

                // If the game didn't clamp, nothing to fix
                if (afterAdd >= desired) return;

                // Find the internal float array on first use
                if (_statusArrayField == null)
                {
                    foreach (var field in typeof(CharacterAfflictions).GetFields(
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    {
                        if (field.FieldType == typeof(float[]))
                        {
                            float[] arr = (float[])field.GetValue(__instance);
                            if (arr != null && arr.Length > (int)statusType)
                            {
                                _statusArrayField = field;
                                break;
                            }
                        }
                    }
                }

                if (_statusArrayField == null) return;

                float[] statuses = (float[])_statusArrayField.GetValue(__instance);
                int index = (int)statusType;
                if (index < 0 || index >= statuses.Length) return;

                // Calculate how much room this status type has
                float otherSum = 0f;
                for (int i = 0; i < statuses.Length; i++)
                {
                    if (i != index) otherSum += statuses[i];
                }
                float maxForThis = Mathf.Max(baseMax - otherSum, 0f);
                statuses[index] = Mathf.Clamp(desired, 0f, maxForThis);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] AddStatus uncap patch error: {ex.Message}");
            }
        }
    }
}