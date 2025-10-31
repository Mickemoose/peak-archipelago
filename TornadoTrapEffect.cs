using System;
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
                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot spawn tornado - no local character");
                    return;
                }

                // get player position
                Vector3 spawnPosition = Character.localCharacter.Center;
                spawnPosition.y += 0.5f; // slightly above ground to avoid clipping lol

                // spawn the tornado at players position
                var tornado = PhotonNetwork.Instantiate("Tornado", spawnPosition, Quaternion.identity, 0);
                
                if (tornado != null)
                {
                    log.LogInfo($"[PeakPelago] Spawned tornado trap at player position: {spawnPosition}");
                    
                    // try to find an existing TornadoSpawner to get its target points
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
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error spawning tornado trap: {ex.Message}");
            }
        }
    }
}