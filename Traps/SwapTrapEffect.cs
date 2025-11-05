using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Photon.Pun;
using HarmonyLib;

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

                // Log the swaps we're about to do
                for (int i = 0; i < actorNumbers.Count; i++)
                {
                    log.LogInfo($"[PeakPelago] Will swap Actor {actorNumbers[i]} from {originalPositions[i]} to {shuffledPositions[i]}");
                }

                // Execute the swaps via RPC handler
                SwapTrapRPCHandler.ExecuteSwaps(_plugin, actorNumbers.ToArray(), shuffledPositions.ToArray());

                log.LogInfo("[PeakPelago] Position swap trap complete!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying position swap trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }

    // Helper class to handle the RPC on the Character class
    public static class SwapTrapRPCHandler
    {
        public static void ExecuteSwaps(PeakArchipelagoPlugin plugin, int[] actorNumbers, Vector3[] positions)
        {
            // Use the local character's PhotonView to broadcast
            if (Character.localCharacter != null && Character.localCharacter.photonView != null)
            {
                Character.localCharacter.photonView.RPC("SwapTrapWarpRPC", RpcTarget.All, actorNumbers, positions);
            }
            else
            {
                plugin._log.LogError("[PeakPelago] Cannot execute swaps - no local character!");
            }
        }
    }

    // Harmony patch to add the RPC method to the Character class
    [HarmonyPatch(typeof(Character))]
    public static class CharacterSwapTrapPatch
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(Character __instance)
        {
            // Register our custom RPC method
            // This runs after every Character is created
        }

        // Our custom RPC method that gets added to Character
        [PunRPC]
        public static void SwapTrapWarpRPC(int[] actorNumbers, Vector3[] positions)
        {
            try
            {
                var plugin = PeakArchipelagoPlugin._instance;
                if (plugin == null) return;

                plugin._log.LogInfo($"[PeakPelago] CLIENT: Received position swap RPC with {actorNumbers.Length} swaps");
                
                if (Character.localCharacter == null)
                {
                    plugin._log.LogWarning("[PeakPelago] CLIENT: No local character!");
                    return;
                }

                int myActorNumber = Character.localCharacter.photonView.Owner.ActorNumber;
                plugin._log.LogInfo($"[PeakPelago] CLIENT: I am actor {myActorNumber}");
                
                // Find my target position
                for (int i = 0; i < actorNumbers.Length; i++)
                {
                    if (actorNumbers[i] == myActorNumber)
                    {
                        Vector3 targetPosition = positions[i];
                        plugin._log.LogInfo($"[PeakPelago] CLIENT: Warping to {targetPosition}");
                        
                        var warpMethod = Character.localCharacter.GetType().GetMethod("WarpPlayer",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (warpMethod != null)
                        {
                            warpMethod.Invoke(Character.localCharacter, new object[] { targetPosition, true });
                            plugin._log.LogInfo($"[PeakPelago] CLIENT: Warp executed!");
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] Error in SwapTrapWarpRPC: {ex.Message}");
            }
        }
    }
}