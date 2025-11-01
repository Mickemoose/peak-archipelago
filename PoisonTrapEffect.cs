using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace Peak.AP
{
    public static class PoisonTrapEffect
    {
        public enum PoisonTrapType
        {
            Minor,
            Normal,
            Deadly
        }

        public enum TargetMode
        {
            LocalPlayer,
            RandomPlayer
        }

        public static void ApplyPoisonTrap(PoisonTrapType trapType, ManualLogSource log, TargetMode targetMode = TargetMode.RandomPlayer)
        {
            try
            {
                Character targetCharacter;

                if (targetMode == TargetMode.RandomPlayer)
                {
                    // Use the static AllCharacters list for random selection
                    if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply poison trap - no characters found");
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
                        log.LogWarning("[PeakPelago] Cannot apply poison trap - no valid characters found");
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
                    log.LogWarning("[PeakPelago] Cannot apply poison - target character or afflictions not found");
                    return;
                }

                float poisonAmount;
                string severity;

                switch (trapType)
                {
                    case PoisonTrapType.Minor:
                        poisonAmount = 0.1f;
                        severity = "Minor";
                        break;
                    
                    case PoisonTrapType.Normal:
                        poisonAmount = 0.25f;
                        severity = "Normal";
                        break;
                    
                    case PoisonTrapType.Deadly:
                        poisonAmount = 0.5f;
                        severity = "Deadly";
                        break;
                    
                    default:
                        poisonAmount = 0.25f;
                        severity = "Normal";
                        break;
                }

                string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
                log.LogInfo($"[PeakPelago] Applying {severity} Poison Trap to {characterName}");

                // Apply it during the next fixed update to ensure proper timing
                targetCharacter.StartCoroutine(ApplyStatusNextFrame(targetCharacter, CharacterAfflictions.STATUSTYPE.Poison, poisonAmount));
                
                log.LogInfo($"[PeakPelago] {severity} Poison Trap scheduled - {poisonAmount} poison will be added to {characterName}");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying poison trap: {ex.Message}");
            }
        }

        private static IEnumerator ApplyStatusNextFrame(Character targetCharacter, CharacterAfflictions.STATUSTYPE type, float amount)
        {
            yield return new WaitForFixedUpdate();
            targetCharacter.refs.afflictions.AddStatus(type, amount);
        }
    }
}