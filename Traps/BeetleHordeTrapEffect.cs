using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class BeetleHordeTrapEffect
    {
        private static GameObject beetlePrefab = null;

        public static void ApplyBeetleHordeTrap(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply beetle horde trap - no characters found");
                    return;
                }

                // Filter to only ALIVE characters
                var validCharacters = Character.AllCharacters.Where(c => 
                    c != null && 
                    c.gameObject.activeInHierarchy && 
                    !c.data.dead &&
                    !c.data.fullyPassedOut
                ).ToList();

                if (validCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply beetle horde trap - no valid characters found");
                    return;
                }

                var random = new System.Random();
                var targetCharacter = validCharacters[random.Next(validCharacters.Count)];

                string characterName = targetCharacter == Character.localCharacter 
                    ? "local player" 
                    : targetCharacter.characterName;
                
                log.LogInfo($"[PeakPelago] Spawning Beetle Horde near: {characterName}");
                Vector3 targetPosition = targetCharacter.Center;
                
                if (PhotonNetwork.IsMasterClient)
                {
                    PeakArchipelagoPlugin._instance.StartCoroutine(SpawnBeetleHorde(targetPosition, characterName, log));
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying beetle horde trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        private static GameObject FindBeetlePrefab(ManualLogSource log)
        {
            if (beetlePrefab != null)
                return beetlePrefab;

            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name == "Beetle" && obj.GetComponent<Beetle>() != null)
                {
                    if (obj.scene.name == null || obj.scene.name == "")
                    {
                        beetlePrefab = obj;
                        log.LogInfo("[PeakPelago] Found Beetle prefab!");
                        return beetlePrefab;
                    }
                }
            }

            log.LogError("[PeakPelago] Could not find Beetle prefab!");
            return null;
        }

        private static IEnumerator SpawnBeetleHorde(Vector3 centerPosition, string characterName, ManualLogSource log)
        {
            GameObject prefab = FindBeetlePrefab(log);
            if (prefab == null)
            {
                log.LogError("[PeakPelago] Cannot spawn beetles - prefab not found");
                yield break;
            }

            int beetlesSpawned = 0;
            float radius = 5f;
            
            for (int i = 0; i < 5; i++)
            {
                float angle = i * (360f / 5f);
                float radians = angle * Mathf.Deg2Rad;
                
                Vector3 offset = new Vector3(
                    Mathf.Cos(radians) * radius,
                    0f,
                    Mathf.Sin(radians) * radius
                );
                
                Vector3 spawnPosition = centerPosition + offset;
                spawnPosition.y += 2f;

                GameObject beetle = null;
                
                // Spawn the beetle
                beetle = UnityEngine.Object.Instantiate(prefab, spawnPosition, Quaternion.identity);
                
                if (beetle != null)
                {
                    // Get PhotonView and allocate a proper view ID
                    PhotonView pv = beetle.GetComponent<PhotonView>();
                    if (pv != null)
                    {
                        // Allocate a new view ID for this beetle
                        int viewID = PhotonNetwork.AllocateViewID(PhotonNetwork.LocalPlayer.ActorNumber);
                        pv.ViewID = viewID;
                        pv.OwnershipTransfer = OwnershipOption.Takeover;
                        
                        log.LogInfo($"[PeakPelago] Allocated ViewID {viewID} for beetle {i + 1}/5");
                    }
                    else
                    {
                        log.LogWarning($"[PeakPelago] Beetle {i + 1}/5 has no PhotonView!");
                    }
                    
                    yield return null;
                    
                    if (pv != null)
                    {
                        log.LogInfo($"[PeakPelago] Beetle {i + 1}/5 - PhotonView.IsMine: {pv.IsMine}, ViewID: {pv.ViewID}, Owner: {pv.Owner?.NickName ?? "none"}");
                    }
                    
                    Beetle beetleComp = beetle.GetComponent<Beetle>();
                    if (beetleComp != null)
                    {
                        // Force into Walking state via reflection
                        var mobStateField = typeof(Mob).GetField("_mobState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (mobStateField != null)
                        {
                            mobStateField.SetValue(beetleComp, 1); // 1 = MobState.Walking
                            log.LogInfo($"[PeakPelago] Set beetle {i + 1}/5 to Walking state");
                        }
                        else
                        {
                            log.LogWarning($"[PeakPelago] Could not find _mobState field for beetle {i + 1}/5");
                        }
                        
                        beetleComp.sleeping = false;
                        beetleComp.UpdateSleeping();
                        
                        log.LogInfo($"[PeakPelago] Beetle {i + 1}/5 final state - mobState: {beetleComp.mobState}, sleeping: {beetleComp.sleeping}, aggroDistance: {beetleComp.aggroDistance}");
                    }
                    else
                    {
                        log.LogWarning($"[PeakPelago] Beetle {i + 1}/5 has no Beetle component!");
                    }
                    
                    beetlesSpawned++;
                    log.LogInfo($"[PeakPelago] Spawned beetle {i + 1}/5 at {spawnPosition}");
                }
                else
                {
                    log.LogError($"[PeakPelago] Failed to instantiate beetle {i + 1}/5");
                }
                
                yield return new WaitForSeconds(0.3f);
            }
            
            log.LogInfo($"[PeakPelago] Beetle Horde Trap complete! Spawned {beetlesSpawned}/5 beetles near {characterName}!");
        }
    }
}