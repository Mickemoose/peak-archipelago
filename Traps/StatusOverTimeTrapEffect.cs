using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class StatusOverTimeTrapEffect
    {
        private static ManualLogSource _log;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
        }

        public enum TargetMode
        {
            LocalPlayer,
            RandomPlayer,
            AllPlayers
        }

        /// <summary>
        /// Apply a status effect over time to target(s)
        /// </summary>
        public static void ApplyStatusOverTime(
            ManualLogSource log,
            TargetMode targetMode = TargetMode.RandomPlayer,
            CharacterAfflictions.STATUSTYPE statusType = CharacterAfflictions.STATUSTYPE.Poison,
            float amountPerTick = 0.05f,
            float tickInterval = 1.0f,
            float duration = 10.0f)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply trap - no characters found");
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
                    log.LogWarning("[PeakPelago] Cannot apply trap - no valid characters found");
                    return;
                }

                // Select target(s) based on mode
                var targetActorNumbers = new List<int>();

                switch (targetMode)
                {
                    case TargetMode.RandomPlayer:
                        var random = new System.Random();
                        var randomChar = validCharacters[random.Next(validCharacters.Count)];
                        targetActorNumbers.Add(randomChar.photonView.Owner.ActorNumber);
                        break;

                    case TargetMode.LocalPlayer:
                        if (Character.localCharacter != null)
                        {
                            targetActorNumbers.Add(Character.localCharacter.photonView.Owner.ActorNumber);
                        }
                        break;

                    case TargetMode.AllPlayers:
                        targetActorNumbers.AddRange(validCharacters.Select(c => c.photonView.Owner.ActorNumber));
                        break;
                }

                if (targetActorNumbers.Count == 0)
                {
                    log.LogWarning("[PeakPelago] No target characters selected");
                    return;
                }

                foreach (var actorNumber in targetActorNumbers)
                {
                    var targetPlayer = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == actorNumber);
                    
                    if (targetPlayer != null && PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                    {
                        string characterName = validCharacters.FirstOrDefault(c => c.photonView.Owner.ActorNumber == actorNumber)?.characterName ?? "Unknown";
                        
                        log.LogInfo($"[PeakPelago] Starting {statusType} over time on {characterName} (Actor {actorNumber}) " +
                                   $"({amountPerTick} per {tickInterval}s for {duration}s)");

                        PeakArchipelagoPlugin._instance.PhotonView.RPC(
                            "StartDOTTrapRPC",
                            RpcTarget.All,
                            actorNumber,
                            (int)statusType,
                            amountPerTick,
                            tickInterval,
                            duration
                        );
                    }
                    else
                    {
                        log.LogWarning($"[PeakPelago] Could not find player or PhotonView for actor {actorNumber}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying status over time trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static IEnumerator ApplyStatusOverTimeCoroutine(
            Character character,
            CharacterAfflictions.STATUSTYPE statusType,
            float amountPerTick,
            float tickInterval,
            float duration,
            ManualLogSource log)
        {
            float elapsed = 0f;
            int tickCount = 0;
            int totalTicks = Mathf.CeilToInt(duration / tickInterval);

            string characterName = character == Character.localCharacter ? "local player" : character.characterName;

            while (elapsed < duration)
            {
                // Only apply if character is still valid and alive
                if (character == null || character.data.dead || !character.gameObject.activeInHierarchy)
                {
                    log.LogInfo($"[PeakPelago] Stopping {statusType} DOT on {characterName} - character invalid/dead");
                    yield break;
                }

                // Apply the status tick locally
                if (character.refs.afflictions != null)
                {
                    character.refs.afflictions.AddStatus(statusType, amountPerTick);
                    tickCount++;

                    log.LogDebug($"[PeakPelago] {statusType} DOT tick {tickCount}/{totalTicks} on {characterName}");
                }

                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
            }

            log.LogInfo($"[PeakPelago] Finished {statusType} over time on {characterName} " +
                       $"(applied {tickCount} ticks)");
        }
    }
}