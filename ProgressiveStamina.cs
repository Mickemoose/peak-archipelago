using System;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

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
                _log.LogWarning("[PeakPelago] Received stamina upgrade but progressive stamina is disabled");
                return;
            }

            _staminaUpgradesReceived++;
            int maxUpgrades = _additionalBarsEnabled ? 8 : 4;

            if (_staminaUpgradesReceived > maxUpgrades)
            {
                _log.LogWarning($"[PeakPelago] Received more stamina upgrades than maximum ({maxUpgrades})");
                _staminaUpgradesReceived = maxUpgrades;
            }

            _baseMaxStamina = 0.25f + (_staminaUpgradesReceived * 0.25f);
            _log.LogInfo($"[PeakPelago] *** STAMINA UPGRADE APPLIED ***");
            _log.LogInfo($"[PeakPelago] Upgrades: {_staminaUpgradesReceived}/{maxUpgrades}");
            _log.LogInfo($"[PeakPelago] Base Max Stamina: {_baseMaxStamina:F2}");

            UpdateCharacterStamina();
        }

        /// <summary>
        /// Force update the character's stamina to match current max
        /// </summary>
        private void UpdateCharacterStamina()
        {
            if (Character.localCharacter != null)
            {
                // Get the effective max stamina (accounting for status effects)
                float effectiveMax = GetEffectiveMaxStamina();
                
                // Set current stamina to the new max
                Character.localCharacter.data.currentStamina = effectiveMax;
                
                // Force UI update
                if (GUIManager.instance != null && GUIManager.instance.bar != null)
                {
                    GUIManager.instance.bar.ChangeBar();
                }
                
                _log.LogInfo($"[PeakPelago] Character stamina updated to {effectiveMax:F2}");
            }
            else
            {
                _log.LogDebug("[PeakPelago] Character not spawned yet, stamina will be set on spawn");
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
            if (!_progressiveStaminaEnabled || Character.localCharacter == null)
            {
                // If disabled or no character, use default calculation
                if (Character.localCharacter != null)
                {
                    return Mathf.Max(1.0f - Character.localCharacter.refs.afflictions.statusSum, 0f);
                }
                return 1.0f;
            }

            // Calculate effective max = base - status effects, minimum 0
            float statusSum = Character.localCharacter.refs.afflictions.statusSum;
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
                    // Calculate: baseMax - statusEffects, minimum 0
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
                    float effectiveMax = _staminaManager.GetEffectiveMaxStamina();
                    __instance.data.currentStamina = Mathf.Clamp(__instance.data.currentStamina, 0f, effectiveMax);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] ClampStamina patch error: {ex.Message}");
            }
        }
    }
}