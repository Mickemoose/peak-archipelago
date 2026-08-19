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

            int maxUpgrades = _additionalBarsEnabled ? 7 : 3;
            
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

            staminaItems = Mathf.Min(staminaItems, _additionalBarsEnabled ? 7 : 3);

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
                if (__instance.isPetrify)
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
        private static readonly FieldInfo _unPassOutCalledField = AccessTools.Field(typeof(Character), "UnPassOutCalled");
        private static readonly FieldInfo _passOutFailsafeTickField = AccessTools.Field(typeof(Character), "passOutFailsafeTick");
        private static readonly MethodInfo _zombieFailsafeMethod = AccessTools.Method(typeof(Character), "ZombieFailsafe");

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

                if (__instance.data.shouldPetrify)
                {
                    __instance.DieInstantly();
                    return false;
                }

                float baseMaxStamina = _staminaManager.GetBaseMaxStamina(__instance);
                float statusSum = __instance.refs.afflictions.statusSum;

                if (statusSum < baseMaxStamina && Time.time - __instance.data.lastPassedOut > 3f)
                {
                    bool unPassOutCalled = _unPassOutCalledField != null && (bool)_unPassOutCalledField.GetValue(__instance);
                    if (!unPassOutCalled)
                    {
                        __instance.photonView.RPC("RPCA_UnPassOut", RpcTarget.All);
                        _passOutFailsafeTickField?.SetValue(__instance, 0f);
                    }
                    else if (_passOutFailsafeTickField != null)
                    {
                        float tick = (float)_passOutFailsafeTickField.GetValue(__instance) + Time.deltaTime;
                        _passOutFailsafeTickField.SetValue(__instance, tick);
                        if (tick > 3f) _unPassOutCalledField?.SetValue(__instance, false);
                    }
                }

                _zombieFailsafeMethod?.Invoke(__instance, null);

                if (__instance.data.deathTimer > 1f)
                {
                    __instance.refs.items.EquipSlot(Optionable<byte>.None);
                    if (!__instance.TryCheckpoint())
                    {
                        if (__instance.refs.afflictions.willZombify && !__instance.data.zombified)
                        {
                            if (!PhotonNetwork.IsMasterClient)
                                __instance.data.zombified = true;
                            __instance.photonView.RPC("RPCA_Zombify", RpcTarget.MasterClient);
                        }
                        else
                        {
                            __instance.photonView.RPC("RPCA_Die", RpcTarget.All);
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

                bool shouldPassOut = statusSum >= baseMaxStamina || __instance.data.shouldPetrify;

                if (shouldPassOut)
                {
                    if (__instance.data.isSkeleton)
                    {
                        if (!__instance.TryCheckpoint())
                        {
                            __instance.photonView.RPC("RPCA_Die", RpcTarget.All);
                        }
                    }
                    else
                    {
                        __instance.data.passOutValue = Mathf.MoveTowards(__instance.data.passOutValue, 1f, Time.deltaTime / 5f);
                        if (__instance.data.passOutValue > 0.999f)
                        {
                            if (__instance.data.shouldPetrify)
                            {
                                __instance.DieInstantly();
                            }
                            else
                            {
                                __instance.photonView.RPC("RPCA_PassOut", RpcTarget.All);
                            }
                        }
                    }
                }
                else
                {
                    __instance.data.passOutValue = Mathf.MoveTowards(__instance.data.passOutValue, 0f, Time.deltaTime / 5f);
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

    [HarmonyPatch(typeof(CharacterAfflictions), "GetStatusCap")]
    public static class CharacterAfflictionsGetStatusCapPatch
    {
        private static ProgressiveStaminaManager _staminaManager;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
        }

        static void Postfix(CharacterAfflictions __instance, ref float __result)
        {
            try
            {
                if (_staminaManager == null || !_staminaManager.IsProgressiveStaminaEnabled())
                    return;

                float baseMax = _staminaManager.GetBaseMaxStamina(__instance.character);
                if (baseMax <= 1.0f) return;

                if (__result < baseMax)
                {
                    __result = baseMax;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] GetStatusCap patch error: {ex.Message}");
            }
        }
    }
}