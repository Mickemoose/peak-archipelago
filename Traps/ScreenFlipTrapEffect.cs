using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class ScreenFlipTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;
        private static bool _isActive = false;

        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
        }

        public static void ApplyScreenFlipTrap(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Screen Flip Trap already active, skipping");
                    return;
                }

                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Screen Flip Trap - no characters found");
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
                    log.LogWarning("[PeakPelago] Cannot apply Screen Flip Trap - no valid characters found");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Screen Flip Trap to {validCharacters.Count} player(s) via RPC!");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartScreenFlipTrapRPC",
                        RpcTarget.All
                    );
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    ApplyScreenFlipTrapLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Screen Flip Trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static void ApplyScreenFlipTrapLocal(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Screen Flip Trap already active locally");
                    return;
                }

                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Screen Flip Trap - no local character");
                    return;
                }

                log.LogInfo("[PeakPelago] Starting Screen Flip Trap locally");
                _plugin.StartCoroutine(ScreenFlipTrapCoroutine(log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Screen Flip Trap locally: {ex.Message}");
            }
        }

        private static IEnumerator ScreenFlipTrapCoroutine(ManualLogSource log)
        {
            _isActive = true;

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                log.LogError("[PeakPelago] Could not find main camera");
                _isActive = false;
                yield break;
            }

            log.LogInfo("[PeakPelago] Flipping screen vertically for 15 seconds");

            // Store original projection matrix
            Matrix4x4 originalProjection = mainCamera.projectionMatrix;

            // Create a flipped projection matrix (flip Y axis)
            Matrix4x4 flippedProjection = originalProjection;
            flippedProjection[1, 1] *= -1f; // Flip the Y scale

            // Apply flipped projection
            mainCamera.projectionMatrix = flippedProjection;

            // Hold for 15 seconds
            yield return new WaitForSeconds(15f);

            // Restore original projection
            mainCamera.projectionMatrix = originalProjection;

            log.LogInfo("[PeakPelago] Screen flip complete!");
            _isActive = false;
        }
    }
}