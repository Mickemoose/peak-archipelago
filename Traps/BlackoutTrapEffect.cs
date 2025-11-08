using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class BlackoutTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;
        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
        }
        public static void ApplyBlackoutTrap(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Blackout Trap - no characters found");
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
                    log.LogWarning("[PeakPelago] Cannot apply Blackout Trap - no valid characters found");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Blackout Trap to {validCharacters.Count} player(s) via RPC!");
                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartBlackoutTrapRPC",
                        RpcTarget.All
                    );
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    ApplyBlackoutTrapLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Blackout Trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static void ApplyBlackoutTrapLocal(ManualLogSource log)
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Blackout Trap - no local character");
                    return;
                }

                log.LogInfo("[PeakPelago] Starting Blackout Trap locally");
                PeakArchipelagoPlugin._instance.StartCoroutine(ApplyBlackoutEffect(log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Blackout Trap locally: {ex.Message}");
            }
        }

        private static IEnumerator ApplyBlackoutEffect(ManualLogSource log)
        {
            if (GUIManager.instance == null)
            {
                log.LogWarning("[PeakPelago] Cannot apply Blackout Trap - GUIManager not found");
                yield break;
            }
            var curseSVFX = GUIManager.instance.curseSVFX;
            if (curseSVFX == null)
            {
                log.LogWarning("[PeakPelago] Cannot apply Blackout Trap - Curse ScreenVFX not found");
                yield break;
            }
            log.LogInfo("[PeakPelago] Starting Blackout effect - Curse ScreenVFX activated!");
            curseSVFX.StartFX(0.5f);
            yield return new WaitForSeconds(15f);
            curseSVFX.EndFX();
            log.LogInfo("[PeakPelago] Blackout Trap complete!");
        }
    }
}