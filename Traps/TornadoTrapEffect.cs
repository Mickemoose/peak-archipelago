using System;
using System.Linq;
using UnityEngine;
using BepInEx.Logging;
using Photon.Pun;

namespace Peak.AP
{
    public static class TornadoTrapEffect
    {
        public static void SpawnTornadoOnPlayer(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot spawn tornado - no characters found");
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
                    log.LogWarning("[PeakPelago] Cannot spawn tornado - no valid characters found");
                    return;
                }

                // Pick a random player
                var random = new System.Random();
                var targetCharacter = validCharacters[random.Next(validCharacters.Count)];
                
                string characterName = targetCharacter == Character.localCharacter 
                    ? "local player" 
                    : targetCharacter.characterName;

                // Get player position
                Vector3 spawnPosition = targetCharacter.Center;
                spawnPosition.y += 0.5f; // slightly above ground to avoid clipping

                log.LogInfo($"[PeakPelago] Spawning tornado at {characterName}'s position: {spawnPosition}");

                // Only the host spawns the tornado (it will be synced via Photon)
                if (PhotonNetwork.IsMasterClient)
                {
                    var tornado = PhotonNetwork.Instantiate("Tornado", spawnPosition, Quaternion.identity, 0);
                    
                    if (tornado != null)
                    {
                        log.LogInfo($"[PeakPelago] Spawned tornado trap at {characterName}'s position");
                        
                        // Try to find an existing TornadoSpawner to get its target points
                        var tornadoSpawner = UnityEngine.Object.FindFirstObjectByType<TornadoSpawner>();
                        if (tornadoSpawner != null)
                        {
                            var tornadoView = tornado.GetComponent<PhotonView>();
                            var spawnerView = tornadoSpawner.GetComponent<PhotonView>();
                            
                            if (tornadoView != null && spawnerView != null)
                            {
                                tornadoView.RPC("RPCA_InitTornado", RpcTarget.All, spawnerView.ViewID);
                                log.LogInfo("[PeakPelago] Initialized tornado with existing spawner targets");
                            }
                        }
                        else
                        {
                            log.LogWarning("[PeakPelago] No TornadoSpawner found - tornado will remain stationary but still dangerous");
                        }
                    }
                    else
                    {
                        log.LogError("[PeakPelago] Failed to spawn tornado");
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error spawning tornado trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}