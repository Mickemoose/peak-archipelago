using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Photon.Pun;

namespace Peak.AP
{
    public static class SwapTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;

        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
        }

        public static void ApplyPositionSwapTrap(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply position swap trap - no characters found");
                    return;
                }

                var validCharacters = Character.AllCharacters.Where(c => 
                    c != null && 
                    c.gameObject.activeInHierarchy && 
                    !c.data.dead &&
                    !c.data.fullyPassedOut &&
                    c.photonView != null &&
                    c.photonView.Owner != null
                ).ToList();

                if (validCharacters.Count < 2)
                {
                    log.LogWarning("[PeakPelago] Cannot apply position swap trap - need at least 2 players");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Position Swap Trap to {validCharacters.Count} players");

                // Store original positions using Center for accuracy
                List<Vector3> originalPositions = new List<Vector3>();
                List<int> actorNumbers = new List<int>();
                
                foreach (var character in validCharacters)
                {
                    int actorNumber = character.photonView.Owner.ActorNumber;
                    Vector3 position = character.Center;
                    
                    actorNumbers.Add(actorNumber);
                    originalPositions.Add(position);
                    
                    log.LogInfo($"[PeakPelago] Actor {actorNumber} at {position}");
                }

                // Shuffle the positions (not the actors!)
                var random = new System.Random();
                var shuffledPositions = originalPositions.OrderBy(x => random.Next()).ToList();

                // Ensure at least one person moved
                bool anyMoved = false;
                for (int i = 0; i < originalPositions.Count; i++)
                {
                    if (Vector3.Distance(originalPositions[i], shuffledPositions[i]) > 0.1f)
                    {
                        anyMoved = true;
                        break;
                    }
                }

                // If nobody moved, force a swap between first two players
                if (!anyMoved && shuffledPositions.Count >= 2)
                {
                    var temp = shuffledPositions[0];
                    shuffledPositions[0] = shuffledPositions[1];
                    shuffledPositions[1] = temp;
                    log.LogInfo("[PeakPelago] Forced swap between first two players");
                }

                // Log and send swaps via RPC
                for (int i = 0; i < actorNumbers.Count; i++)
                {
                    log.LogInfo($"[PeakPelago] Will swap Actor {actorNumbers[i]} from {originalPositions[i]} to {shuffledPositions[i]}");
                    
                    // Send RPC to each player to warp them
                    var targetPlayer = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == actorNumbers[i]);
                    
                    if (targetPlayer != null && PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                    {
                        PeakArchipelagoPlugin._instance.PhotonView.RPC(
                            "SwapTrapWarpRPC",
                            targetPlayer,
                            actorNumbers[i],
                            shuffledPositions[i].x,
                            shuffledPositions[i].y,
                            shuffledPositions[i].z
                        );
                    }
                    else
                    {
                        log.LogWarning($"[PeakPelago] Could not find player or PhotonView for actor {actorNumbers[i]}");
                    }
                }

                log.LogInfo("[PeakPelago] Position swap trap complete!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying position swap trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}