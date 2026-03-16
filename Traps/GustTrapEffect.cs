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
            // Find any WindStorm template (active or inactive) and spawn our own copy
            GameObject template = null;

            // Check active objects first
            template = GameObject.Find("WindStorm");

            if (template == null)
            {
                foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (obj.name == "WindStorm" && obj.scene.IsValid())
                    {
                        template = obj;
                        break;
                    }
                }
            }

            if (template == null)
            {
                log.LogError("[PeakPelago] Could not find any WindStorm template!");
                yield break;
            }

            Vector3 spawnPos = Character.localCharacter.transform.position;
            bool wasTemplateActive = template.activeSelf;
            template.SetActive(false);
            var windStorm = UnityEngine.Object.Instantiate(template, spawnPos, Quaternion.identity);
            if (wasTemplateActive) template.SetActive(true);
            windStorm.name = "GustTrap_WindStorm";

            foreach (var pv in windStorm.GetComponentsInChildren<PhotonView>(true))
                UnityEngine.Object.DestroyImmediate(pv);

            windStorm.SetActive(true);
            log.LogInfo("[PeakPelago] Spawned GustTrap WindStorm");

            yield return new WaitForSeconds(10f);

            if (windStorm == null)
            {
                log.LogWarning("[PeakPelago] WindStorm was destroyed during gust trap");
                yield break;
            }

            foreach (var ps in windStorm.GetComponentsInChildren<ParticleSystem>())
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var audio in windStorm.GetComponentsInChildren<AudioSource>())
                audio.Stop();

            yield return null;

            UnityEngine.Object.Destroy(windStorm);
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