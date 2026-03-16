using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class GustTrapEffect
    {
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

                // Send RPC to all clients
                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("StartGustTrapRPC", RpcTarget.All);
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    ActivateGustLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Gust Trap: {ex.Message}");
            }
        }

        public static void ActivateGustLocal(ManualLogSource log)
        {
            PeakArchipelagoPlugin._instance.StartCoroutine(ActivateGustCoroutine(log));
        }

        private static IEnumerator ActivateGustCoroutine(ManualLogSource log)
        {
            GameObject windStormObject = null;
            bool wasActive = false;
            bool weSpawnedIt = false;

            // First, try to find an existing WindStorm in the scene
            windStormObject = GameObject.Find("WindStorm");
            
            if (windStormObject != null)
            {
                log.LogInfo("[PeakPelago] Found existing WindStorm in scene");
                wasActive = windStormObject.activeSelf;
            }
            else
            {
                // If no WindStorm exists, search ALL objects including inactive ones
                log.LogInfo("[PeakPelago] No WindStorm in current biome, searching all objects including inactive...");
                
                GameObject[] allWindStorms = Resources.FindObjectsOfTypeAll<GameObject>();
                
                foreach (var obj in allWindStorms)
                {
                    if (obj.name == "WindStorm" && obj.scene.IsValid())
                    {
                        log.LogInfo($"[PeakPelago] Found WindStorm: {obj.name} in scene, active: {obj.activeInHierarchy}");
                        
                        //instantiate a copy
                        Vector3 spawnPos = Character.localCharacter.transform.position;
                        windStormObject = UnityEngine.Object.Instantiate(obj, spawnPos, Quaternion.identity);
                        windStormObject.name = "GustTrap_WindStorm";
                        weSpawnedIt = true;
                        wasActive = false;
                        break;
                    }
                }
                
                if (windStormObject == null)
                {
                    log.LogError("[PeakPelago] Could not find any WindStorm object!");
                    yield break;
                }
            }

            // Activate the WindStorm
            if (!wasActive)
            {
                log.LogInfo("[PeakPelago] Activating WindStorm...");
                windStormObject.SetActive(true);
            }
            else
            {
                log.LogInfo("[PeakPelago] WindStorm already active, extending duration...");
            }

            yield return new WaitForSeconds(10f);

            if (windStormObject == null)
            {
                log.LogWarning("[PeakPelago] WindStorm object was destroyed during gust trap");
                yield break;
            }

            if (weSpawnedIt)
            {
                log.LogInfo("[PeakPelago] Destroying spawned WindStorm");

                ParticleSystem[] particles = windStormObject.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particles)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                AudioSource[] audioSources = windStormObject.GetComponentsInChildren<AudioSource>();
                foreach (var audio in audioSources)
                {
                    audio.Stop();
                }

                yield return null;

                UnityEngine.Object.Destroy(windStormObject);
            }
            else
            {
                log.LogInfo("[PeakPelago] Deactivating WindStorm");

                ParticleSystem[] particles = windStormObject.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particles)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                AudioSource[] audioSources = windStormObject.GetComponentsInChildren<AudioSource>();
                foreach (var audio in audioSources)
                {
                    audio.Stop();
                }

                if (!wasActive)
                {
                    windStormObject.SetActive(false);
                }
            }

            log.LogInfo("[PeakPelago] Gust Trap complete!");
        }
    }
}

public static class TransformExtensions
{
    public static string GetFullPath(this Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}