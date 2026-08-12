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
        public static void ApplyBeetleHordeTrap(ManualLogSource log)
        {
            try
            {
                var targetCharacter = TrapHelpers.GetRandomValidCharacter();
                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply beetle horde trap - no valid characters found");
                    return;
                }

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

        private static IEnumerator SpawnBeetleHorde(Vector3 centerPosition, string characterName, ManualLogSource log)
        {
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

                // Use PhotonNetwork.Instantiate with the prefab path
                GameObject beetle = PhotonNetwork.Instantiate("0_Items/Beetle", spawnPosition, Quaternion.identity);
                
                if (beetle != null)
                {
                    log.LogInfo($"[PeakPelago] Spawned networked beetle {i + 1}/5 at {spawnPosition}");
                    
                    yield return null; // Wait a frame for network sync
                    
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
                        
                        beetleComp.sleeping = false;
                        beetleComp.UpdateSleeping();
                        
                        PhotonView pv = beetle.GetComponent<PhotonView>();
                        if (pv != null)
                        {
                            log.LogInfo($"[PeakPelago] Beetle {i + 1}/5 - ViewID: {pv.ViewID}, IsMine: {pv.IsMine}");
                        }
                    }
                    
                    beetlesSpawned++;
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