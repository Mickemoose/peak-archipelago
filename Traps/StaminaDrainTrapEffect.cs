using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class StaminaDrainTrapEffect
    {
        private static ManualLogSource _log;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
        }

        public static void ApplyStaminaDrainTrap(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Stamina Drain Trap - no characters found");
                    return;
                }

                var validCharacters = Character.AllCharacters.Where(c =>
                    c != null &&
                    c.gameObject.activeInHierarchy &&
                    !c.data.dead &&
                    c.photonView != null &&
                    c.photonView.Owner != null
                ).ToList();

                if (validCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Stamina Drain Trap - no valid characters found");
                    return;
                }

                var random = new System.Random();
                var target = validCharacters[random.Next(validCharacters.Count)];
                int actorNumber = target.photonView.Owner.ActorNumber;

                log.LogInfo($"[PeakPelago] Applying Stamina Drain Trap to {target.characterName} (Actor {actorNumber})");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartStaminaDrainTrapRPC",
                        RpcTarget.All,
                        actorNumber
                    );
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    if (Character.localCharacter != null)
                    {
                        PeakArchipelagoPlugin._instance.StartCoroutine(
                            StaminaDrainCoroutine(Character.localCharacter, log));
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Stamina Drain Trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static IEnumerator StaminaDrainCoroutine(Character character, ManualLogSource log)
        {
            float elapsed = 0f;
            float tickInterval = 0.5f;
            float duration = 15f;
            float drainPerTick = 0.1f;
            int tickCount = 0;

            string characterName = character == Character.localCharacter ? "local player" : character.characterName;
            log.LogInfo($"[PeakPelago] Starting Stamina Drain on {characterName} ({drainPerTick} per {tickInterval}s for {duration}s)");

            while (elapsed < duration)
            {
                if (character == null || character.data.dead || !character.gameObject.activeInHierarchy)
                {
                    log.LogInfo($"[PeakPelago] Stopping Stamina Drain on {characterName} - character invalid/dead");
                    PeakArchipelagoPlugin._instance?._trapLinkService?.NotifyTrapComplete();
                    yield break;
                }

                // Drain extra stamina first, then current stamina
                float remaining = drainPerTick;
                if (character.data.extraStamina > 0)
                {
                    float deduction = Mathf.Min(character.data.extraStamina, remaining);
                    character.data.extraStamina -= deduction;
                    remaining -= deduction;
                }
                if (remaining > 0)
                {
                    character.data.currentStamina = Mathf.Max(0f, character.data.currentStamina - remaining);
                }

                tickCount++;
                log.LogDebug($"[PeakPelago] Stamina Drain tick {tickCount} on {characterName}");

                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
            }

            PeakArchipelagoPlugin._instance?._trapLinkService?.NotifyTrapComplete();
            log.LogInfo($"[PeakPelago] Finished Stamina Drain on {characterName} ({tickCount} ticks)");
        }
    }
}
