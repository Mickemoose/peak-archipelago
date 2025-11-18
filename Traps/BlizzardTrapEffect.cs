using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class BlizzardTrapEffect
    {
        public static void ApplyBlizzardTrap(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Blizzard Trap - no characters found");
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
                    log.LogWarning("[PeakPelago] Cannot apply Blizzard Trap - no valid characters found");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Blizzard Trap! Snow will blow for 10 seconds");

                // Send RPC to all clients
                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("StartBlizzardTrapRPC", RpcTarget.All);
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    ActivateBlizzardLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Blizzard Trap: {ex.Message}");
            }
        }

        public static void ActivateBlizzardLocal(ManualLogSource log)
        {
            PeakArchipelagoPlugin._instance.StartCoroutine(ActivateBlizzardCoroutine(log));
        }

        private static IEnumerator ActivateBlizzardCoroutine(ManualLogSource log)
        {
            GameObject snowStormObject = null;
            bool wasActive = false;
            bool weSpawnedIt = false;

            // First, try to find an existing SnowStorm in the scene
            snowStormObject = GameObject.Find("SnowStorm");
            
            if (snowStormObject != null)
            {
                log.LogInfo("[PeakPelago] Found existing SnowStorm in scene");
                wasActive = snowStormObject.activeSelf;
            }
            else
            {
                // If no SnowStorm exists, search ALL objects including inactive ones
                log.LogInfo("[PeakPelago] No SnowStorm in current biome, searching all objects including inactive...");
                
                GameObject[] allSnowStorms = Resources.FindObjectsOfTypeAll<GameObject>();
                
                foreach (var obj in allSnowStorms)
                {
                    if (obj.name == "SnowStorm" && obj.scene.IsValid())
                    {
                        log.LogInfo($"[PeakPelago] Found SnowStorm: {obj.name} in scene, active: {obj.activeInHierarchy}");
                        
                        // Instantiate a copy
                        Vector3 spawnPos = Character.localCharacter.transform.position;
                        snowStormObject = UnityEngine.Object.Instantiate(obj, spawnPos, Quaternion.identity);
                        snowStormObject.name = "BlizzardTrap_SnowStorm";
                        weSpawnedIt = true;
                        wasActive = false;
                        break;
                    }
                }
                
                if (snowStormObject == null)
                {
                    log.LogError("[PeakPelago] Could not find any SnowStorm object!");
                    yield break;
                }
            }

            // Activate the SnowStorm
            if (!wasActive)
            {
                log.LogInfo("[PeakPelago] Activating SnowStorm...");
                snowStormObject.SetActive(true);
            }
            else
            {
                log.LogInfo("[PeakPelago] SnowStorm already active, extending duration...");
            }

            yield return new WaitForSeconds(10f);

            if (weSpawnedIt)
            {
                log.LogInfo("[PeakPelago] Destroying spawned SnowStorm");
                
                snowStormObject.SetActive(false);
                
                log.LogInfo("[PeakPelago] SnowStorm disabled");
                
                // Wait a moment
                yield return new WaitForSeconds(0.5f);
                
                // Now destroy it
                UnityEngine.Object.Destroy(snowStormObject);
                
                log.LogInfo("[PeakPelago] SnowStorm destroyed");
            }
            else if (!wasActive)
            {
                log.LogInfo("[PeakPelago] Deactivating SnowStorm");
                
                // Stop particles before deactivating
                ParticleSystem[] particles = snowStormObject.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particles)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                
                AudioSource[] audioSources = snowStormObject.GetComponentsInChildren<AudioSource>(true);
                foreach (var audio in audioSources)
                {
                    audio.Stop();
                }
                
                // Wait before deactivating
                yield return new WaitForSeconds(0.5f);
                
                snowStormObject.SetActive(false);
            }

            log.LogInfo("[PeakPelago] Blizzard Trap complete!");
        }
    }
}