using System;
using HarmonyLib;
using UnityEngine;

namespace Peak.AP
{
    /// <summary>
    /// Harmony patch to fix the stamina bar UI when max stamina exceeds 1.0
    /// </summary>
    [HarmonyPatch(typeof(StaminaBar), "Update")]
    public static class StaminaBarUpdatePatch
    {
        private static ProgressiveStaminaManager _staminaManager;

        public static void SetStaminaManager(ProgressiveStaminaManager manager)
        {
            _staminaManager = manager;
            Debug.Log("[PeakPelago] Stamina manager set for StaminaBar patch");
        }

        static void Postfix(StaminaBar __instance)
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

                // Get the base max stamina from our manager
                float baseMaxStamina = _staminaManager.GetBaseMaxStamina();

                // Recalculate the outline size to accommodate larger stamina
                float statusSum = Character.observedCharacter.refs.afflictions.statusSum;
                
                // Use the ACTUAL max stamina (base or base + status, whichever is larger)
                float displayMax = Mathf.Max(baseMaxStamina, statusSum);
                
                // Adjust the stamina bar outline to fit the new max
                __instance.staminaBarOutline.sizeDelta = new Vector2(
                    14f + (baseMaxStamina + statusSum) * __instance.fullBar.sizeDelta.x, 
                    __instance.staminaBarOutline.sizeDelta.y
                );

                __instance.maxStaminaBar.sizeDelta = new Vector2(
                    Mathf.Lerp(
                        __instance.maxStaminaBar.sizeDelta.x,
                        Mathf.Max(0f, baseMaxStamina * __instance.fullBar.sizeDelta.x + __instance.staminaBarOffset),
                        Time.deltaTime * 10f
                    ),
                    __instance.maxStaminaBar.sizeDelta.y
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] StaminaBar Update patch error: {ex.Message}");
            }
        }
    }
}