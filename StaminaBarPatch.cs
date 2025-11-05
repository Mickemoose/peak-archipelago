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
                float statusSum = Character.observedCharacter.refs.afflictions.statusSum;
                
                // The outline extends to the BASE max (where afflictions end)
                __instance.staminaBarOutline.sizeDelta = new Vector2(
                    14f + baseMaxStamina * __instance.fullBar.sizeDelta.x, 
                    __instance.staminaBarOutline.sizeDelta.y
                );

                // The green bar shows effective stamina (base minus afflictions)
                float effectiveMax = Mathf.Max(baseMaxStamina - statusSum, 0f);
                
                __instance.maxStaminaBar.sizeDelta = new Vector2(
                    Mathf.Lerp(
                        __instance.maxStaminaBar.sizeDelta.x,
                        Mathf.Max(0f, effectiveMax * __instance.fullBar.sizeDelta.x + __instance.staminaBarOffset),
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