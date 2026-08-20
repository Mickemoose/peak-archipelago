using System;
using UnityEngine;
using BepInEx.Logging;
using Photon.Pun;

namespace Peak.AP
{
    public static class AntiSphereEffect
    {
        public const float DEFAULT_LIFETIME = 60f;

        public static void SpawnAntiSphereOnPlayer(ManualLogSource log, PhotonView view, float lifetime = DEFAULT_LIFETIME)
        {
            try
            {
                var targetCharacter = TrapHelpers.GetRandomValidCharacter();
                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot spawn anti-sphere - no valid characters found");
                    return;
                }

                string characterName = targetCharacter == Character.localCharacter
                    ? "local player"
                    : targetCharacter.characterName;

                Vector3 spawnPosition = targetCharacter.Center;
                log.LogInfo($"[PeakPelago] Spawning anti-sphere at {characterName}'s position: {spawnPosition}");

                // AntiSphere is a purely local physics volume, so every client spawns its own copy
                if (view != null && PhotonNetwork.IsConnected)
                {
                    view.RPC("RPC_SpawnAntiSphere", RpcTarget.All, spawnPosition.x, spawnPosition.y, spawnPosition.z, lifetime);
                }
                else
                {
                    SpawnAntiSphereAt(log, spawnPosition, lifetime);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error spawning anti-sphere: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static void SpawnAntiSphereAt(ManualLogSource log, Vector3 position, float lifetime)
        {
            try
            {
                GameObject template = ResolveTemplate();
                if (template == null)
                {
                    log.LogWarning("[PeakPelago] Anti-Sphere - could not find an AntiSphere to clone");
                    return;
                }

                GameObject sphere = UnityEngine.Object.Instantiate(template, position, Quaternion.identity);
                sphere.name = "AntiSphere_PeakPelago";
                sphere.SetActive(true);

                if (lifetime > 0f)
                {
                    UnityEngine.Object.Destroy(sphere, lifetime);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error creating anti-sphere: {ex.Message}");
            }
        }

        private static GameObject ResolveTemplate()
        {
            GameObject prefab = Resources.Load<GameObject>("AntiSphere");
            if (prefab != null) return prefab;

            var existing = UnityEngine.Object.FindAnyObjectByType<global::Peak.AntiSphere>(FindObjectsInactive.Include);
            if (existing != null) return existing.gameObject;

            var all = Resources.FindObjectsOfTypeAll<global::Peak.AntiSphere>();
            if (all.Length > 0) return all[0].gameObject;

            return null;
        }
    }
}
