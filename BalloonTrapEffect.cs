using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace Peak.AP
{
    public static class BalloonTrapEffect
    {
        public static void ApplyBalloonTrap(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply balloon trap - no characters found");
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
                    log.LogWarning("[PeakPelago] Cannot apply balloon trap - no valid characters found");
                    return;
                }

                // Pick a random character
                var random = new System.Random();
                var targetCharacter = validCharacters[random.Next(validCharacters.Count)];

                // Get the character's balloon component
                var balloonComponent = targetCharacter.GetComponent<CharacterBalloons>();
                if (balloonComponent == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply balloon trap - target character has no balloon component");
                    return;
                }

                int maxColors = balloonComponent.balloonColors != null ? balloonComponent.balloonColors.Length : 6;
                
                string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
                log.LogInfo($"[PeakPelago] Applying Balloon Trap to: {characterName}");

                // Apply during the next fixed update to ensure proper timing
                targetCharacter.StartCoroutine(ApplyBalloonsNextFrame(balloonComponent, maxColors, random));
                
                log.LogInfo($"[PeakPelago] Balloon trap scheduled for {characterName}!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying balloon trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        private static IEnumerator ApplyBalloonsNextFrame(CharacterBalloons balloonComponent, int maxColors, System.Random random)
        {
            yield return new WaitForFixedUpdate();
            
            // Tie 9 balloons to the character using the existing TieNewBalloon method
            for (int i = 0; i < 9; i++)
            {
                int colorIndex = random.Next(0, maxColors);
                balloonComponent.TieNewBalloon(colorIndex);
            }
        }
    }
}