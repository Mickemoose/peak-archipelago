using System;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace Peak.AP
{
    /// <summary>
    /// Manages progressive stamina bar upgrades received from Archipelago
    /// </summary>
    public class ProgressiveStaminaManager
    {
        private readonly ManualLogSource _log;
        private float _baseMaxStamina = 1.0f;
        private int _staminaUpgradesReceived = 0;
        private bool _progressiveStaminaEnabled = false;
        private bool _additionalBarsEnabled = false;

        public ProgressiveStaminaManager(ManualLogSource log)
        {
            _log = log;
        }

        /// <summary>
        /// Initialize the stamina system based on options
        /// </summary>
        public void Initialize(bool progressiveStaminaEnabled, bool additionalBarsEnabled)
        {
            _progressiveStaminaEnabled = progressiveStaminaEnabled;
            _additionalBarsEnabled = additionalBarsEnabled;

            if (_progressiveStaminaEnabled)
            {
                _baseMaxStamina = 0.25f;
                _staminaUpgradesReceived = 0;
                _log.LogInfo("[PeakPelago] Progressive Stamina ENABLED - base max stamina set to 0.25");

                // Force update current character's stamina
                UpdateCharacterStamina();
            }
            else
            {
                _baseMaxStamina = 1.0f;
                _log.LogInfo("[PeakPelago] Progressive Stamina DISABLED - using normal 1.0 max stamina");
            }
        }

        /// <summary>
        /// Apply a stamina bar upgrade
        /// </summary>
        public void ApplyStaminaUpgrade()
        {
            if (!_progressiveStaminaEnabled)
            {
                _log.LogWarning("[PeakPelago] Cannot apply stamina upgrade - progressive stamina is disabled");
                return;
            }

            _staminaUpgradesReceived++;
            int maxUpgrades = _additionalBarsEnabled ? 7 : 4;

            if (_staminaUpgradesReceived > maxUpgrades)
            {
                _staminaUpgradesReceived = maxUpgrades;
            }

            _baseMaxStamina = 0.25f + (_staminaUpgradesReceived * 0.25f);
            _log.LogInfo($"[PeakPelago] Applied stamina upgrade #{_staminaUpgradesReceived}: new base max = {_baseMaxStamina}");
            
            UpdateCharacterStamina();
        }

        /// <summary>
        /// Force update the character's stamina to match current max
        /// </summary>
        public void UpdateCharacterStamina()
        {
            if (Character.localCharacter != null)
            {
                // Calculate effective max for LOCAL character specifically
                float statusSum = Character.localCharacter.refs.afflictions.statusSum;
                float effectiveMax = Mathf.Max(_baseMaxStamina - statusSum, 0f);

                // Set current stamina to the new max
                Character.localCharacter.data.currentStamina = effectiveMax;

                // Force UI update
                if (GUIManager.instance != null && GUIManager.instance.bar != null)
                {
                    GUIManager.instance.bar.ChangeBar();
                }
            }
        }

        /// <summary>
        /// Get the base maximum stamina (without status effects)
        /// </summary>
        public float GetBaseMaxStamina()
        {
            return _baseMaxStamina;
        }

        /// <summary>
        /// Get the effective maximum stamina (base - status effects)
        /// </summary>
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
                return _baseMaxStamina;
            }
            float statusSum = Character.observedCharacter.refs.afflictions.statusSum;
            return Mathf.Max(_baseMaxStamina - statusSum, 0f);
        }

        /// <summary>
        /// Get the number of stamina upgrades received
        /// </summary>
        public int GetStaminaUpgradesReceived()
        {
            return _staminaUpgradesReceived;
        }

        /// <summary>
        /// Check if progressive stamina is enabled
        /// </summary>
        public bool IsProgressiveStaminaEnabled()
        {
            return _progressiveStaminaEnabled;
        }

        /// <summary>
        /// Save stamina state to string for persistence
        /// </summary>
        public string SaveState()
        {
            return $"{_staminaUpgradesReceived},{_baseMaxStamina:F2}";
        }

        /// <summary>
        /// Load stamina state from string
        /// </summary>
        public void LoadState(string stateData)
        {
            if (string.IsNullOrEmpty(stateData)) return;

            try
            {
                var parts = stateData.Split(',');
                if (parts.Length >= 2)
                {
                    _staminaUpgradesReceived = int.Parse(parts[0]);
                    _baseMaxStamina = float.Parse(parts[1]);
                    _log.LogInfo($"[PeakPelago] Loaded stamina state: {_staminaUpgradesReceived} upgrades, {_baseMaxStamina:F2} max");

                    UpdateCharacterStamina();
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to load stamina state: {ex.Message}");
            }
        }
    }

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
            Debug.Log("[PeakPelago] Stamina manager set for BarAffliction UpdateAffliction patch");
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
    /// <summary>
    /// Harmony patch to override Character.GetMaxStamina() when progressive stamina is enabled
    /// </summary>
    [HarmonyPatch(typeof(Character), "GetMaxStamina")]
    public static class CharacterGetMaxStaminaPatch
    {
        private static ProgressiveStaminaManager _staminaManager;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
            Debug.Log("[PeakPelago] Stamina manager set for GetMaxStamina patch");
        }

        static bool Prefix(Character __instance, ref float __result)
        {
            try
            {
                if (_staminaManager != null && _staminaManager.IsProgressiveStaminaEnabled())
                {
                    float baseMax = _staminaManager.GetBaseMaxStamina();
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

    /// <summary>
    /// Harmony patch to override Character.ClampStamina() to respect our custom max
    /// </summary>
    [HarmonyPatch(typeof(Character), "ClampStamina")]
    public static class CharacterClampStaminaPatch
    {
        private static ProgressiveStaminaManager _staminaManager;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
            Debug.Log("[PeakPelago] Stamina manager set for ClampStamina patch");
        }

        static void Postfix(Character __instance)
        {
            try
            {
                if (_staminaManager != null && _staminaManager.IsProgressiveStaminaEnabled())
                {
                    float baseMax = _staminaManager.GetBaseMaxStamina();
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
            Debug.Log("[PeakPelago] Stamina manager set for HandlePassedOut patch");
        }

        static bool Prefix(Character __instance)
        {
            try
            {
                if (_staminaManager == null || !_staminaManager.IsProgressiveStaminaEnabled())
                {
                    return true;
                }

                float baseMaxStamina = _staminaManager.GetBaseMaxStamina();
                float statusSum = __instance.refs.afflictions.statusSum;
                
                // Only allow recovery if afflictions drop below the threshold
                if (statusSum < baseMaxStamina && Time.time - __instance.data.lastPassedOut > 3f)
                {
                    if (!__instance.photonView.IsMine)
                    {
                        return false;
                    }
                    
                    __instance.photonView.RPC("RPCA_UnPassOut", RpcTarget.All);
                }
                
                // Handle death timer (copied from original method)
                if (__instance.data.deathTimer > 1f)
                {
                    __instance.refs.items.EquipSlot(Optionable<byte>.None);
                    
                    // Check for zombification
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
    /// <summary>
    /// Patch HandleLife to respect progressive stamina for pass out threshold
    /// Uses hysteresis to prevent oscillation between knocked out and awake states
    /// </summary>

    [HarmonyPatch(typeof(Character), "HandleLife")]
    public static class CharacterHandleLifePatch
    {
        private static ProgressiveStaminaManager _staminaManager;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
            Debug.Log("[PeakPelago] Stamina manager set for HandleLife patch");
        }

        static bool Prefix(Character __instance)
        {
            try
            {
                if (_staminaManager == null || !_staminaManager.IsProgressiveStaminaEnabled())
                {
                    return true;
                }

                float baseMaxStamina = _staminaManager.GetBaseMaxStamina();
                float statusSum = __instance.refs.afflictions.statusSum;
                
                // Check if we should pass out based on progressive stamina threshold
                bool shouldPassOut = statusSum >= baseMaxStamina;
                
                if (__instance.data.isSkeleton)
                {
                    // Skeletons die instead of passing out
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
                    // Normal character pass out logic
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

                // Skip the original method since we handled it
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