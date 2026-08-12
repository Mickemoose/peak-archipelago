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
                PeakArchipelagoPlugin._instance?._trapLinkService?.NotifyTrapComplete();
                yield break;
            }

            var activeSecondsField = typeof(IllegalScreenEffect).GetField("activeForSeconds",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (activeSecondsField == null)
            {
                log.LogError("[PeakPelago] Could not find activeForSeconds field");
                PeakArchipelagoPlugin._instance?._trapLinkService?.NotifyTrapComplete();
                yield break;
            }

            activeSecondsField.SetValue(screenEffect, duration);
            log.LogInfo($"[PeakPelago] Applied blind screen effect for {duration} seconds");

            yield return new WaitForSeconds(duration);

            log.LogInfo("[PeakPelago] Blind screen effect expired");

            PeakArchipelagoPlugin._instance?._trapLinkService?.NotifyTrapComplete();
            log.LogInfo("[PeakPelago] Fear trap completed");
        }
    }
}