using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;

namespace Peak.AP
{
    public static class PixelTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;
        private static bool _isActive = false;

        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
        }

        public static void ApplyPixelTrap(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Pixel Trap already active, skipping");
                    return;
                }

                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Pixel Trap - no characters found");
                    return;
                }

                var validCharacters = Character.AllCharacters.Where(c => 
                    c != null && 
                    c.gameObject.activeInHierarchy && 
                    !c.data.dead &&
                    !c.data.fullyPassedOut
                ).ToList();

                if (validCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Pixel Trap - no valid characters found");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Pixel Trap to {validCharacters.Count} player(s) via RPC!");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartPixelTrapRPC",
                        RpcTarget.All
                    );
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    ApplyPixelTrapLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Pixel Trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static void ApplyPixelTrapLocal(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Pixel Trap already active locally");
                    return;
                }

                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Pixel Trap - no local character");
                    return;
                }

                log.LogInfo("[PeakPelago] Starting Pixel Trap locally");
                _plugin.StartCoroutine(PixelTrapCoroutine(log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Pixel Trap locally: {ex.Message}");
            }
        }

        private static IEnumerator PixelTrapCoroutine(ManualLogSource log)
        {
            _isActive = true;

            // Get the URP asset via reflection
            var urpAsset = GraphicsSettings.currentRenderPipeline;
            
            if (urpAsset == null)
            {
                log.LogError("[PeakPelago] No render pipeline asset found!");
                _isActive = false;
                PeakArchipelagoPlugin._instance?._trapLinkService?.NotifyTrapComplete();
                yield break;
            }

            var urpAssetType = urpAsset.GetType();

            // Get renderScale property
            var renderScaleProperty = urpAssetType.GetProperty("renderScale");
            var upscalingFilterProperty = urpAssetType.GetProperty("upscalingFilter");

            if (renderScaleProperty == null)
            {
                log.LogError("[PeakPelago] Could not find renderScale property!");
                _isActive = false;
                PeakArchipelagoPlugin._instance?._trapLinkService?.NotifyTrapComplete();
                yield break;
            }

            // Store original values
            float originalRenderScale = (float)renderScaleProperty.GetValue(urpAsset);
            object originalUpscalingFilter = upscalingFilterProperty?.GetValue(urpAsset);

            log.LogInfo($"[PeakPelago] Original render scale: {originalRenderScale}");
            log.LogInfo("[PeakPelago] Setting render scale to 0.05 (super pixelated!) for 15 seconds");

            // Set ultra-low render scale
            renderScaleProperty.SetValue(urpAsset, 0.03f);
            
            // Try to set upscaling filter to Point (value 1)
            if (upscalingFilterProperty != null)
            {
                try
                {
                    // UpscalingFilterSelection.Point = 1
                    var upscalingFilterType = upscalingFilterProperty.PropertyType;
                    var pointValue = Enum.ToObject(upscalingFilterType, 1);
                    upscalingFilterProperty.SetValue(urpAsset, pointValue);
                }
                catch (Exception ex)
                {
                    log.LogWarning($"[PeakPelago] Could not set upscaling filter: {ex.Message}");
                }
            }

            log.LogInfo("[PeakPelago] Screen is now ultra pixelated!");

            // Hold for 15 seconds
            yield return new WaitForSeconds(15f);

            // Restore original settings
            renderScaleProperty.SetValue(urpAsset, originalRenderScale);
            
            if (upscalingFilterProperty != null && originalUpscalingFilter != null)
            {
                upscalingFilterProperty.SetValue(urpAsset, originalUpscalingFilter);
            }

            log.LogInfo("[PeakPelago] Screen restored to normal resolution!");
            _isActive = false;
            PeakArchipelagoPlugin._instance?._trapLinkService?.NotifyTrapComplete();
        }
    }
}