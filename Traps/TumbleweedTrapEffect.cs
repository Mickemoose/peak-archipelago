using System;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Photon.Pun;

namespace Peak.AP
{
    public static class TumbleweedTrapEffect
    {
        public static void ApplyTumbleweedTrap(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply tumbleweed trap - no characters found");
                    return;
                }

                var validCharacters = Character.AllCharacters.Where(c => 
                    c != null && 
                    c.gameObject.activeInHierarchy && 
                    !c.data.dead
                ).ToList();

                if (validCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply tumbleweed trap - no valid characters found");
                    return;
                }

                var random = new System.Random();
                var targetCharacter = validCharacters[random.Next(validCharacters.Count)];

                string characterName = targetCharacter == Character.localCharacter 
                    ? "local player" 
                    : targetCharacter.characterName;
                
                log.LogInfo($"[PeakPelago] Spawning Tumbleweed targeting: {characterName}");
                Vector3 spawnOffset = Vector3.right * 15f;
                spawnOffset += Vector3.up * 2f;
                Vector3 spawnPosition = targetCharacter.Center + spawnOffset;
                var tumbleweed = PhotonNetwork.Instantiate("TumbleWeed", spawnPosition, Quaternion.identity, 0);
                
                if (tumbleweed != null)
                {
                    log.LogInfo($"[PeakPelago] Tumbleweed trap spawned at {spawnPosition} targeting {characterName}!");
                }
                else
                {
                    log.LogError("[PeakPelago] Failed to spawn tumbleweed");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying tumbleweed trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}