using System;
using System.Linq;
using UnityEngine;
using BepInEx.Logging;
using Photon.Pun;

namespace Peak.AP
{
    public static class ScoutmasterTrapEffect
    {
        public static void TriggerScoutmasterTrap(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot trigger scoutmaster trap - no characters found");
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
                    log.LogWarning("[PeakPelago] Cannot trigger scoutmaster trap - no valid characters found");
                    return;
                }

                // Pick a random player to be targeted
                var random = new System.Random();
                var targetCharacter = validCharacters[random.Next(validCharacters.Count)];
                
                string characterName = targetCharacter == Character.localCharacter 
                    ? "local player" 
                    : targetCharacter.characterName;

                log.LogInfo($"[PeakPelago] Scoutmaster targeting {characterName}");

                if (Scoutmaster.GetPrimaryScoutmaster(out Scoutmaster scoutmaster))
                {
                    scoutmaster.SetCurrentTarget(targetCharacter, forceForTime: 45f);
                    
                    log.LogInfo($"[PeakPelago] Scoutmaster now hunting {characterName}!");
                }
                else
                {
                    log.LogWarning("[PeakPelago] No Scoutmaster found in the scene");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error triggering scoutmaster trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}