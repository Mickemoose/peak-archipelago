using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class ZoomTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;
        private static bool _isActive = false;

        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
        }

        public static void ApplyZoomTrap(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Zoom Trap already active, skipping");
                    return;
                }

                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Zoom Trap - no characters found");
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
                    log.LogWarning("[PeakPelago] Cannot apply Zoom Trap - no valid characters found");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Zoom Trap to {validCharacters.Count} player(s) via RPC!");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartZoomTrapRPC",
                        RpcTarget.All
                    );
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    ApplyZoomTrapLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Zoom Trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static void ApplyZoomTrapLocal(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Zoom Trap already active locally");
                    return;
                }

                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Zoom Trap - no local character");
                    return;
                }

                log.LogInfo("[PeakPelago] Starting Zoom Trap locally");
                _plugin.StartCoroutine(ZoomTrapCoroutine(log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Zoom Trap locally: {ex.Message}");
            }
        }

        private static IEnumerator ZoomTrapCoroutine(ManualLogSource log)
        {
            _isActive = true;
            Camera mainCamera = null;
            float originalFov = 60f;

            try
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    log.LogError("[PeakPelago] Could not find main camera");
                    yield break;
                }

                originalFov = mainCamera.fieldOfView;
                float targetFov = 20f;
                float nearClip = mainCamera.nearClipPlane;
                float farClip = mainCamera.farClipPlane;

                log.LogInfo($"[PeakPelago] Starting zoom from {originalFov} to {targetFov} via projection matrix");

                // Zoom in over 1 second
                float elapsed = 0f;
                while (elapsed < 1f)
                {
                    elapsed += Time.deltaTime;
                    float currentFov = Mathf.Lerp(originalFov, targetFov, elapsed);
                    
                    // Calculate custom projection matrix with our FOV
                    mainCamera.projectionMatrix = Matrix4x4.Perspective(
                        currentFov, 
                        mainCamera.aspect, 
                        nearClip, 
                        farClip
                    );
                    
                    yield return null;
                }

                // Set to exact target FOV
                mainCamera.projectionMatrix = Matrix4x4.Perspective(targetFov, mainCamera.aspect, nearClip, farClip);
                log.LogInfo($"[PeakPelago] Holding zoom at {targetFov} for 13 seconds");

                // Hold for 13 seconds - keep setting projection matrix in case something resets it
                float holdTime = 0f;
                while (holdTime < 13f)
                {
                    holdTime += Time.deltaTime;
                    mainCamera.projectionMatrix = Matrix4x4.Perspective(targetFov, mainCamera.aspect, nearClip, farClip);
                    yield return null;
                }

                log.LogInfo("[PeakPelago] Zooming out");

                // Zoom out over 2 seconds
                elapsed = 0f;
                while (elapsed < 2f)
                {
                    elapsed += Time.deltaTime;
                    float currentFov = Mathf.Lerp(targetFov, originalFov, elapsed / 2f);
                    
                    mainCamera.projectionMatrix = Matrix4x4.Perspective(
                        currentFov, 
                        mainCamera.aspect, 
                        nearClip, 
                        farClip
                    );
                    
                    yield return null;
                }

                // Reset to default projection matrix
                mainCamera.ResetProjectionMatrix();
                log.LogInfo("[PeakPelago] Zoom Trap complete!");
            }
            finally
            {
                // Ensure camera is reset and flag is cleared even if something goes wrong
                mainCamera?.ResetProjectionMatrix();
                _isActive = false;
            }
        }
    }
}