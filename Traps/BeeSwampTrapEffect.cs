using System;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Photon.Pun;

namespace Peak.AP
{
    public static class BeeSwarmTrapEffect
    {
        public enum TargetMode
        {
            LocalPlayer,
            RandomPlayer
        }

        /// <summary>
        /// Spawns a bee swarm near a target character, either the local player or a random player.
        /// </summary>
        public static void ApplyBeeSwarmTrap(ManualLogSource log, TargetMode targetMode = TargetMode.RandomPlayer)
        {
            try
            {
                Character targetCharacter;

                if (targetMode == TargetMode.RandomPlayer)
                {
                    // Use the static AllCharacters list for random selection
                    if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot spawn bee swarm - no characters found");
                        return;
                    }

                    // Filter to only active, alive characters
                    var validCharacters = Character.AllCharacters.Where(c =>
                        c != null &&
                        c.gameObject.activeInHierarchy &&
                        !c.data.dead &&
                        !c.data.fullyPassedOut
                    ).ToList();

                    if (validCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot spawn bee swarm - no valid characters found");
                        return;
                    }

                    // Pick a random character
                    var random = new System.Random();
                    targetCharacter = validCharacters[random.Next(validCharacters.Count)];
                }
                else
                {
                    // Default to local player
                    targetCharacter = Character.localCharacter;
                    
                    if (targetCharacter == null)
                    {
                        log.LogWarning("[PeakPelago] Cannot spawn bee swarm - no local character");
                        return;
                    }
                }

                // Calculate spawn position near the target character
                Vector3 spawnPosition = targetCharacter.Center + targetCharacter.transform.forward * 3f;
                spawnPosition += Vector3.up * 1f;

                string characterName = targetCharacter == Character.localCharacter 
                    ? "local player" 
                    : targetCharacter.characterName ?? "player";

                log.LogInfo($"[PeakPelago] Spawning bee swarm near {characterName} at position {spawnPosition}");

                // Spawn the bee swarm using Photon
                var beeSwarm = PhotonNetwork.Instantiate("BeeSwarm", spawnPosition, Quaternion.identity, 0);
                
                if (beeSwarm != null)
                {
                    log.LogInfo($"[PeakPelago] Bee Swarm trap spawned successfully targeting {characterName}!");
                }
                else
                {
                    log.LogError("[PeakPelago] Failed to spawn bee swarm");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error spawning bee swarm trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}