using System;
using System.Collections;
using UnityEngine;
using BepInEx.Logging;
using Photon.Pun;

namespace Peak.AP
{
    public static class FearTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;

        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
        }

        public static void ApplyFearTrap(ManualLogSource log)
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Fear trap - no local character");
                    return;
                }

                log.LogInfo("[PeakPelago] Triggering Fear trap for all players via RPC");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartFearTrapRPC",
                        RpcTarget.All
                    );
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    ApplyFearTrapLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Fear trap: {ex.Message}");
            }
        }

        public static void ApplyFearTrapLocal(ManualLogSource log)
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Fear trap - no local character");
                    return;
                }

                log.LogInfo("[PeakPelago] Starting Fear trap locally");
                _plugin.StartCoroutine(FearTrapCoroutine(log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Fear trap: {ex.Message}");
            }
        }

        private static IEnumerator FearTrapCoroutine(ManualLogSource log)
        {
            float duration = 25f;

            // Find the IllegalScreenEffect in the scene
            var screenEffect = UnityEngine.Object.FindFirstObjectByType<IllegalScreenEffect>();
            if (screenEffect == null)
            {
                log.LogWarning("[PeakPelago] IllegalScreenEffect not found in scene");
                yield break;
            }

            var activeSecondsField = typeof(IllegalScreenEffect).GetField("activeForSeconds", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (activeSecondsField == null)
            {
                log.LogError("[PeakPelago] Could not find activeForSeconds field");
                yield break;
            }

            float originalActiveSeconds = (float)activeSecondsField.GetValue(screenEffect);
            activeSecondsField.SetValue(screenEffect, duration);
            log.LogInfo($"[PeakPelago] Applied blind screen effect for {duration} seconds");
            object mapHandler = GetMapHandler();
            float originalFogDensity = 0f;
            bool fogModified = false;

            if (mapHandler != null)
            {
                try
                {
                    var fogDensityField = mapHandler.GetType().GetField("fogDensity");
                    if (fogDensityField != null)
                    {
                        originalFogDensity = (float)fogDensityField.GetValue(mapHandler);
                        fogDensityField.SetValue(mapHandler, Mathf.Min(originalFogDensity * 3f, 0.1f));
                        fogModified = true;
                        log.LogInfo($"[PeakPelago] Increased fog density from {originalFogDensity} to {Mathf.Min(originalFogDensity * 3f, 0.1f)}");
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning($"[PeakPelago] Could not modify fog: {ex.Message}");
                }
            }

            yield return new WaitForSeconds(duration);

            log.LogInfo("[PeakPelago] Blind screen effect expired");
            if (fogModified && mapHandler != null)
            {
                try
                {
                    var fogDensityField = mapHandler.GetType().GetField("fogDensity");
                    if (fogDensityField != null)
                    {
                        fogDensityField.SetValue(mapHandler, originalFogDensity);
                        log.LogInfo($"[PeakPelago] Restored fog density to {originalFogDensity}");
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning($"[PeakPelago] Could not restore fog: {ex.Message}");
                }
            }

            log.LogInfo("[PeakPelago] Fear trap completed");
        }

        private static object GetMapHandler()
        {
            try
            {
                var singletonType = typeof(UnityEngine.Object).Assembly.GetType("Zorro.Core.Singleton`1");
                if (singletonType != null)
                {
                    var mapHandlerType = typeof(UnityEngine.Object).Assembly.GetType("MapHandler");
                    if (mapHandlerType != null)
                    {
                        var genericType = singletonType.MakeGenericType(mapHandlerType);
                        var instanceProperty = genericType.GetProperty("Instance");
                        if (instanceProperty != null)
                        {
                            return instanceProperty.GetValue(null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[PeakPelago] Failed to get MapHandler: {ex.Message}");
            }
            return null;
        }
    }
}