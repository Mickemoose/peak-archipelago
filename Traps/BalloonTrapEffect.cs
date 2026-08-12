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
                var targetCharacter = TrapHelpers.GetRandomValidCharacter(excludePassedOut: false);
                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply balloon trap - no valid characters found");
                    return;
                }

                var random = new System.Random();

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