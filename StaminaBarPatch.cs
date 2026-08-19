using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

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

                float targetMaxWidth = Mathf.Max(0f, effectiveMax * __instance.fullBar.sizeDelta.x + __instance.staminaBarOffset);
                float lerpedMaxWidth = Mathf.Lerp(
                    __instance.maxStaminaBar.sizeDelta.x,
                    targetMaxWidth,
                    Time.deltaTime * 10f
                );

                __instance.maxStaminaBar.sizeDelta = new Vector2(
                    lerpedMaxWidth,
                    __instance.maxStaminaBar.sizeDelta.y
                );

                if (__instance.afflictions.Length > 0)
                {
                    Transform mainParent = null;
                    Transform petrifyParent = null;

                    for (int i = 0; i < __instance.afflictions.Length; i++)
                    {
                        var aff = __instance.afflictions[i];
                        if (aff == null || aff.rtf == null)
                            continue;

                        if (aff.isPetrify)
                        {
                            if (petrifyParent == null) petrifyParent = aff.rtf.parent;
                        }
                        else if (mainParent == null)
                        {
                            mainParent = aff.rtf.parent;
                        }
                    }

                    if (petrifyParent == null && __instance.petrifyAffliction != null && __instance.petrifyAffliction.rtf != null)
                    {
                        petrifyParent = __instance.petrifyAffliction.rtf.parent;
                    }

                    // The petrify bar lives in the extra stamina bar, which vanilla lays out on its own
                    bool petrifyIsSeparate = petrifyParent != null && petrifyParent != mainParent;

                    // Disable the LayoutGroup so we can position afflictions manually
                    if (mainParent != null)
                    {
                        var hlg = mainParent.GetComponent<HorizontalLayoutGroup>();
                        if (hlg != null && hlg.enabled)
                        {
                            hlg.enabled = false;
                        }
                    }

                    if (petrifyIsSeparate)
                    {
                        var extraHlg = petrifyParent.GetComponent<HorizontalLayoutGroup>();
                        if (extraHlg != null && !extraHlg.enabled)
                        {
                            extraHlg.enabled = true;
                        }
                    }

                    // Position afflictions manually after the green bar
                    float stackOffset = 0f;
                    for (int i = 0; i < __instance.afflictions.Length; i++)
                    {
                        var aff = __instance.afflictions[i];
                        if (aff == null || aff.rtf == null)
                            continue;
                        if (petrifyIsSeparate && aff.isPetrify)
                            continue;
                        if (!aff.gameObject.activeSelf || aff.width <= 0.01f)
                            continue;

                        float x = lerpedMaxWidth + stackOffset + (aff.width * 0.5f);
                        aff.rtf.anchoredPosition = new Vector2(x, aff.rtf.anchoredPosition.y);
                        stackOffset += aff.width;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] StaminaBar Update patch error: {ex.Message}");
            }
        }
    }
}
