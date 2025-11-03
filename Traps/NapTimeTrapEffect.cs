using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace Peak.AP
{
    public static class NapTimeTrapEffect
    {
        public enum TargetMode
        {
            LocalPlayer,
            RandomPlayer
        }

        public static void ApplyNapTrap(ManualLogSource log, TargetMode targetMode = TargetMode.RandomPlayer)
        {
            try
            {
                Character targetCharacter;

                if (targetMode == TargetMode.RandomPlayer)
                {
                    // Use the static AllCharacters list for random selection
                    if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply nap trap - no characters found");
                        return;
                    }

                    // Filter to only active, alive characters
                    var validCharacters = Character.AllCharacters.Where(c => 
                        c != null && 
                        c.gameObject.activeInHierarchy && 
                        !c.data.dead
                    ).ToList();

                    if (validCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply nap trap - no valid characters found");
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
                }

                if (targetCharacter == null || targetCharacter.refs.afflictions == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply nap trap - target character or afflictions not found");
                    return;
                }

                float drowsyAmount = 1.0f;

                string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
                log.LogInfo($"[PeakPelago] Applying Nap Time Trap to {characterName}");

                // Apply it during the next fixed update to ensure proper timing
                targetCharacter.StartCoroutine(ApplyStatusNextFrame(targetCharacter, CharacterAfflictions.STATUSTYPE.Drowsy, drowsyAmount));
                
                log.LogInfo($"[PeakPelago] Nap Time Trap scheduled for {characterName}!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying nap time trap: {ex.Message}");
            }
        }

        private static IEnumerator ApplyStatusNextFrame(Character targetCharacter, CharacterAfflictions.STATUSTYPE type, float amount)
        {
            yield return new WaitForFixedUpdate();
            targetCharacter.refs.afflictions.AddStatus(type, amount);
        }
    }
}