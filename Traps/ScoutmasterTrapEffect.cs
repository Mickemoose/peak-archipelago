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
                var targetCharacter = TrapHelpers.GetRandomValidCharacter();
                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot trigger scoutmaster trap - no valid characters found");
                    return;
                }
                
                string characterName = targetCharacter == Character.localCharacter 
                    ? "local player" 
                    : targetCharacter.characterName;

                log.LogInfo($"[PeakPelago] Scoutmaster targeting {characterName}");

                if (Scoutmaster.GetPrimaryScoutmaster(out Scoutmaster scoutmaster))
                {
                    // SetCurrentTarget silently drops the RPC while the Scoutmaster is prevented from spawning
                    if (scoutmaster.preventSpawning)
                    {
                        log.LogWarning("[PeakPelago] Scoutmaster cannot spawn in this run - trap had no effect");
                        return;
                    }

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