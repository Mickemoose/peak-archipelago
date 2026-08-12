using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace Peak.AP
{
    public static class HungryHungryCamperTrapEffect
    {
        public enum TargetMode
        {
            LocalPlayer,
            RandomPlayer
        }

        public static void ApplyHungerTrap(ManualLogSource log, TargetMode targetMode = TargetMode.RandomPlayer)
        {
            try
            {
                Character targetCharacter;

                if (targetMode == TargetMode.RandomPlayer)
                {
                    targetCharacter = TrapHelpers.GetRandomValidCharacter(excludePassedOut: false);

                    if (targetCharacter == null)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply hunger trap - no characters found");
                        return;
                    }
                }
                else
                {
                    // Default to local player
                    targetCharacter = Character.localCharacter;
                }

                if (targetCharacter == null || targetCharacter.refs.afflictions == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply hunger trap - target character or afflictions not found");
                    return;
                }

                float hungerAmount = 0.7f;

                string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
                log.LogInfo($"[PeakPelago] Applying Hunger Trap to {characterName}");

                // Apply it during the next fixed update to ensure proper timing
                targetCharacter.StartCoroutine(ApplyStatusNextFrame(targetCharacter, CharacterAfflictions.STATUSTYPE.Hunger, hungerAmount));

                log.LogInfo($"[PeakPelago] Hunger Trap scheduled for {characterName}!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying hunger trap: {ex.Message}");
            }
        }

        private static IEnumerator ApplyStatusNextFrame(Character targetCharacter, CharacterAfflictions.STATUSTYPE type, float amount)
        {
            yield return new WaitForFixedUpdate();
            targetCharacter.refs.afflictions.AddStatus(type, amount);
        }
    }
}