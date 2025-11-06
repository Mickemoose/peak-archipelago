using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;
using Zorro.Core;

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
                    if (existing != 1.0f) // Has non-default stamina
                    {
                        _log.LogInfo($"[PeakPelago] Player already has stamina: {existing:F2} - preserving it");
                        return;
                    }
                }
            }

            if (_progressiveStaminaEnabled)
            {
                _log.LogInfo("[PeakPelago] Progressive Stamina ENABLED - base max stamina set to 0.25 for new players");

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

            float currentStamina = GetPlayerStamina(PhotonNetwork.LocalPlayer);
            int maxUpgrades = _additionalBarsEnabled ? 7 : 4;
            int currentUpgrades = Mathf.RoundToInt((currentStamina - 0.25f) / 0.25f);
            
            if (currentUpgrades >= maxUpgrades)
            {
                _log.LogInfo($"[PeakPelago] Already at max stamina upgrades ({maxUpgrades})");
                return;
            }

            float newStamina = currentStamina + 0.25f;
            SetPlayerStamina(PhotonNetwork.LocalPlayer, newStamina);
            
            _log.LogInfo($"[PeakPelago] Applied stamina upgrade: new base max = {newStamina}");
        }

        public float GetPlayerStamina(Photon.Realtime.Player player)
        {
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
            return _progressiveStaminaEnabled ? 0.25f : 1.0f;
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
            
            if (PhotonNetwork.LocalPlayer == null) return 0;
            
            float currentStamina = GetPlayerStamina(PhotonNetwork.LocalPlayer);
            return Mathf.RoundToInt((currentStamina - 0.25f) / 0.25f);
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

        public string SaveState()
        {
            if (PhotonNetwork.LocalPlayer == null) return "0,0.25";
            
            int upgrades = GetStaminaUpgradesReceived();
            float stamina = GetPlayerStamina(PhotonNetwork.LocalPlayer);
            return $"{upgrades},{stamina:F2}";
        }

        public void LoadState(string stateData)
        {
            if (string.IsNullOrEmpty(stateData)) return;

            try
            {
                var parts = stateData.Split(',');
                if (parts.Length >= 2)
                {
                    float stamina = float.Parse(parts[1]);
                    
                    // Try to apply immediately if Photon is ready, otherwise store it
                    if (PhotonNetwork.LocalPlayer != null)
                    {
                        SetPlayerStamina(PhotonNetwork.LocalPlayer, stamina);
                        _log.LogInfo($"[PeakPelago] Loaded stamina state: {stamina:F2} max");
                    }
                    else
                    {
                        _pendingStaminaLoad = stamina;
                        _log.LogInfo($"[PeakPelago] Stored pending stamina load: {stamina:F2} max (will apply when Photon connects)");
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
                __instance.size = bar.fullBar.sizeDelta.x * currentStatus;
                __instance.width = __instance.size;
                __instance.rtf.sizeDelta = new Vector2(__instance.width, __instance.rtf.sizeDelta.y);
                float startX = bar.maxStaminaBar.sizeDelta.x + (__instance.width * 0.5f);
                float stackOffset = 0f;
                for (int i = 0; i < bar.afflictions.Length; i++)
                {
                    if (bar.afflictions[i] == __instance)
                    {
                        break;
                    }
                    
                    if (bar.afflictions[i].gameObject.activeSelf && bar.afflictions[i].width > 0.01f)
                    {
                        stackOffset += bar.afflictions[i].width;
                    }
                }
                
                float finalX = startX + stackOffset;
                __instance.rtf.anchoredPosition = new Vector2(finalX, __instance.rtf.anchoredPosition.y);

                return false;
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
                {
                    return true;
                }

                float baseMaxStamina = _staminaManager.GetBaseMaxStamina(__instance);
                float statusSum = __instance.refs.afflictions.statusSum;
                
                if (statusSum < baseMaxStamina && Time.time - __instance.data.lastPassedOut > 3f)
                {
                    if (!__instance.photonView.IsMine)
                    {
                        return false;
                    }
                    
                    __instance.photonView.RPC("RPCA_UnPassOut", RpcTarget.All);
                }
                
                if (__instance.data.deathTimer > 1f)
                {
                    __instance.refs.items.EquipSlot(Optionable<byte>.None);
                    
                    if (__instance.refs.afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Spores) >= 0.5f 
                        && !__instance.data.zombified)
                    {
                        if (!PhotonNetwork.IsMasterClient)
                        {
                            __instance.data.zombified = true;
                        }
                        __instance.photonView.RPC("RPCA_Zombify", RpcTarget.MasterClient, 
                            __instance.Center + Vector3.up * 0.2f + Vector3.forward * 0.1f);
                    }
                    else
                    {
                        __instance.photonView.RPC("RPCA_Die", RpcTarget.All, 
                            __instance.Center + Vector3.up * 0.2f + Vector3.forward * 0.1f);
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
                        __instance.data.passOutValue = Mathf.MoveTowards(__instance.data.passOutValue, 1f, Time.deltaTime / 5f);
                        if (__instance.data.passOutValue > 0.999f)
                        {
                            __instance.photonView.RPC("RPCA_Die", RpcTarget.All, 
                                __instance.Center + Vector3.up * 0.2f + Vector3.forward * 0.1f);
                        }
                    }
                    else
                    {
                        __instance.data.passOutValue = Mathf.MoveTowards(__instance.data.passOutValue, 0f, Time.deltaTime / 5f);
                    }
                }
                else
                {
                    if (shouldPassOut)
                    {
                        __instance.data.passOutValue = Mathf.MoveTowards(__instance.data.passOutValue, 1f, Time.deltaTime / 5f);
                        if (__instance.data.passOutValue > 0.999f)
                        {
                            __instance.photonView.RPC("RPCA_PassOut", RpcTarget.All);
                        }
                    }
                    else
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
}