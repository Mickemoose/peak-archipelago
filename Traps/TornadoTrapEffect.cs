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
                // Pick a random player
                var targetCharacter = TrapHelpers.GetRandomValidCharacter();
                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot spawn tornado - no valid characters found");
                    return;
                }
                
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
                        var spawnerView = tornadoSpawner != null ? tornadoSpawner.GetComponent<PhotonView>() : null;
                        var tornadoView = tornado.GetComponent<PhotonView>();

                        if (spawnerView != null && tornadoView != null)
                        {
                            tornadoView.RPC("RPCA_InitTornado", RpcTarget.All, spawnerView.ViewID);
                            log.LogInfo("[PeakPelago] Initialized tornado with existing spawner targets");
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