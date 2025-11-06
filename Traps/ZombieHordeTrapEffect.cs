using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class ZombieHordeTrapEffect
    {
        public static void ApplyZombieHordeTrap(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply zombie horde trap - no characters found");
                    return;
                }

                var validCharacters = Character.AllCharacters.Where(c => 
                    c != null && 
                    c.gameObject.activeInHierarchy && 
                    !c.data.dead
                ).ToList();

                if (validCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply zombie horde trap - no valid characters found");
                    return;
                }

                var random = new System.Random();
                var targetCharacter = validCharacters[random.Next(validCharacters.Count)];

                string characterName = targetCharacter == Character.localCharacter 
                    ? "local player" 
                    : targetCharacter.characterName;
                
                log.LogInfo($"[PeakPelago] Spawning Zombie Horde near: {characterName}");
                Vector3 targetPosition = targetCharacter.Center;
                PeakArchipelagoPlugin._instance.StartCoroutine(SpawnZombieHorde(targetPosition, characterName, log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying zombie horde trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        private static IEnumerator SpawnZombieHorde(Vector3 centerPosition, string characterName, ManualLogSource log)
        {
            int zombiesSpawned = 0;
            float radius = 5f;
            
            for (int i = 0; i < 5; i++)
            {
                // Calculate spawn position in a circle
                float angle = i * (360f / 5f);
                float radians = angle * Mathf.Deg2Rad;
                
                Vector3 offset = new Vector3(
                    Mathf.Cos(radians) * radius,
                    0f,
                    Mathf.Sin(radians) * radius
                );
                
                Vector3 spawnPosition = centerPosition + offset;
                spawnPosition.y += 1f;

                try
                {
                    var zombie = PhotonNetwork.Instantiate("MushroomZombie", spawnPosition, Quaternion.identity, 0);
                    if (zombie != null)
                    {
                        zombiesSpawned++;
                        log.LogInfo($"[PeakPelago] Spawned zombie {i + 1}/5 at {spawnPosition}");
                        
                        var zombieComponent = zombie.GetComponent<MushroomZombie>();
                        if (zombieComponent != null)
                        {
                            // Register with the ZombieManager
                            if (ZombieManager.Instance != null)
                            {
                                ZombieManager.Instance.RegisterZombie(zombieComponent);
                            }
                            
                            // Enable the zombie via RPC
                            var photonView = zombie.GetComponent<PhotonView>();
                            if (photonView != null)
                            {
                                // Call the RPC to enable the zombie on all clients
                                ZombieManager.Instance.photonView.RPC("RPC_EnableZombie", RpcTarget.All, photonView.ViewID);
                            }
                        }
                    }
                    else
                    {
                        log.LogError($"[PeakPelago] Failed to spawn zombie {i + 1}/5");
                    }
                }
                catch (Exception ex)
                {
                    log.LogError($"[PeakPelago] Error spawning zombie {i + 1}: {ex.Message}");
                }
                yield return new WaitForSeconds(0.2f);
            }
            
            log.LogInfo($"[PeakPelago] Zombie Horde Trap complete! Spawned {zombiesSpawned}/5 zombies near {characterName}!");
        }
    }
}