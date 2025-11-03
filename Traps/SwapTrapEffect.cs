using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace Peak.AP
{
    public static class SwapTrapEffect
    {
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
                    !c.data.fullyPassedOut
                ).ToList();

                if (validCharacters.Count < 2)
                {
                    log.LogWarning("[PeakPelago] Cannot apply position swap trap - need at least 2 players");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Position Swap Trap to {validCharacters.Count} players");

                // Store all current positions
                List<Vector3> positions = new List<Vector3>();
                foreach (var character in validCharacters)
                {
                    positions.Add(character.transform.position);
                }
                // Shuffle em
                var random = new System.Random();
                var shuffledPositions = positions.OrderBy(x => random.Next()).ToList();

                bool anyChanged = false;
                for (int i = 0; i < validCharacters.Count; i++)
                {
                    if (Vector3.Distance(positions[i], shuffledPositions[i]) > 0.1f)
                    {
                        anyChanged = true;
                        break;
                    }
                }

                // If no positions changed, manually swap first two
                if (!anyChanged && validCharacters.Count >= 2)
                {
                    var temp = shuffledPositions[0];
                    shuffledPositions[0] = shuffledPositions[1];
                    shuffledPositions[1] = temp;
                }

                // Apply the shuffled positions
                for (int i = 0; i < validCharacters.Count; i++)
                {
                    var character = validCharacters[i];
                    var newPosition = shuffledPositions[i];
                    
                    character.transform.position = newPosition;
                    
                    string characterName = character == Character.localCharacter ? "local player" : character.characterName;
                    log.LogInfo($"[PeakPelago] Swapped {characterName} to new position: {newPosition}");
                }

            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying position swap trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}