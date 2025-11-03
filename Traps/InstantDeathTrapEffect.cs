using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace Peak.AP
{
    public static class InstantDeathTrapEffect
    {
        public static void ApplyInstantDeathTrap(ManualLogSource log)
        {
            try
            {
                // Get all valid characters
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply instant death trap - no characters found");
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
                    log.LogWarning("[PeakPelago] Cannot apply instant death trap - no valid characters found");
                    return;
                }

                var random = new System.Random();
                var targetCharacter = validCharacters[random.Next(validCharacters.Count)];

                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply instant death trap - target character not found");
                    return;
                }

                string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
                
                log.LogInfo($"[PeakPelago] Applying Instant Death Trap to: {characterName}");

                targetCharacter.StartCoroutine(KillCharacterNextFrame(targetCharacter, characterName, log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying instant death trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        private static IEnumerator KillCharacterNextFrame(Character targetCharacter, string characterName, ManualLogSource log)
        {
            yield return null;

            try
            {
                log.LogInfo($"[PeakPelago] Executing instant death for {characterName}");
                
                var dieInstantlyMethod = targetCharacter.GetType().GetMethod("DieInstantly", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (dieInstantlyMethod != null)
                {
                    dieInstantlyMethod.Invoke(targetCharacter, null);
                    log.LogInfo($"[PeakPelago] Instant Death Trap killed {characterName}!");
                }
                else
                {
                    log.LogError("[PeakPelago] Could not find DieInstantly method");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Instant death trap failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    log.LogError($"[PeakPelago] Inner: {ex.InnerException.Message}");
                    log.LogError($"[PeakPelago] Inner stack: {ex.InnerException.StackTrace}");
                }
            }
        }
    }
}