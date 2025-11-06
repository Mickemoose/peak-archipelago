using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class GustTrapEffect
    {
        private static Bounds _originalBounds;
        private static bool _boundsModified = false;
        
        public static void ApplyGustTrap(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Gust Trap - no characters found");
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
                    log.LogWarning("[PeakPelago] Cannot apply Gust Trap - no valid characters found");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Gust Trap! Wind will blow for 10 seconds");

                // Only the host starts the trap
                if (PhotonNetwork.IsMasterClient)
                {
                    PeakArchipelagoPlugin._instance.StartCoroutine(ActivateGust(log));
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Gust Trap: {ex.Message}");
            }
        }

        private static IEnumerator ActivateGust(ManualLogSource log)
        {
            WindChillZone windZone = WindChillZone.instance;
            
            if (windZone == null)
            {
                windZone = UnityEngine.Object.FindFirstObjectByType<WindChillZone>();
            }
            
            if (windZone == null)
            {
                log.LogError("[PeakPelago] Cannot find WindChillZone!");
                yield break;
            }

            var photonView = windZone.GetComponent<PhotonView>();
            
            if (photonView == null)
            {
                log.LogError("[PeakPelago] WindChillZone has no PhotonView!");
                yield break;
            }

            // Tell ALL clients to expand the wind bounds to cover the entire map
            if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
            {
                // Send current bounds center so all clients expand from the same point
                Vector3 boundsCenter = windZone.windZoneBounds.center;
                PeakArchipelagoPlugin._instance.PhotonView.RPC("ExpandWindBoundsRPC", RpcTarget.All, boundsCenter.x, boundsCenter.y, boundsCenter.z);
            }

            yield return new WaitForSeconds(0.2f); // Wait for bounds to expand on all clients

            // Generate random wind direction
            Vector3 windDirection = GenerateRandomWindDirection();
            
            // Activate wind (goes to ALL clients via game's existing RPC)
            photonView.RPC("RPCA_ToggleWind", RpcTarget.All, true, windDirection, 10f);
            
            log.LogInfo($"[PeakPelago] Gust Trap activated - wind direction: {windDirection}");

            // Wait 10 seconds
            yield return new WaitForSeconds(10f);

            // Tell ALL clients to restore original bounds
            if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
            {
                PeakArchipelagoPlugin._instance.PhotonView.RPC("RestoreWindBoundsRPC", RpcTarget.All);
            }

            log.LogInfo("[PeakPelago] Gust Trap complete!");
        }

        private static Vector3 GenerateRandomWindDirection()
        {
            float randomValue = UnityEngine.Random.value;
            Vector3 baseDirection = Vector3.right * (randomValue > 0.5f ? 1f : -1f);
            return Vector3.Lerp(baseDirection, Vector3.forward, 0.2f).normalized;
        }

        // Called via RPC to expand bounds on all clients
        public static void ExpandWindBounds(Vector3 center)
        {
            try
            {
                WindChillZone windZone = WindChillZone.instance;
                if (windZone == null)
                {
                    Debug.LogError("[PeakPelago] Cannot find WindChillZone to expand bounds!");
                    return;
                }

                if (!_boundsModified)
                {
                    // Save original bounds
                    _originalBounds = windZone.windZoneBounds;
                    _boundsModified = true;
                    Debug.Log($"[PeakPelago] Saved original wind bounds: {_originalBounds}");
                }

                // Make the bounds MASSIVE to cover entire map
                windZone.windZoneBounds = new Bounds(center, new Vector3(10000f, 10000f, 10000f));
                Debug.Log($"[PeakPelago] Expanded wind bounds to cover entire map!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] Error expanding wind bounds: {ex.Message}");
            }
        }

        // Called via RPC to restore bounds on all clients
        public static void RestoreWindBounds()
        {
            try
            {
                WindChillZone windZone = WindChillZone.instance;
                if (windZone == null)
                {
                    Debug.LogError("[PeakPelago] Cannot find WindChillZone to restore bounds!");
                    return;
                }

                if (_boundsModified)
                {
                    // Restore original bounds
                    windZone.windZoneBounds = _originalBounds;
                    _boundsModified = false;
                    Debug.Log($"[PeakPelago] Restored original wind bounds!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] Error restoring wind bounds: {ex.Message}");
            }
        }
    }
}