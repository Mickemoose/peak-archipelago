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

                // AddStatus only works on the owning client, so route remote targets through their own client
                if (targetCharacter == Character.localCharacter)
                {
                    targetCharacter.StartCoroutine(ApplyStatusNextFrame(targetCharacter, CharacterAfflictions.STATUSTYPE.Hunger, hungerAmount, log));
                }
                else if (PeakArchipelagoPlugin._instance?.PhotonView != null && targetCharacter.photonView?.Owner != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "ApplyHungerTrapRPC",
                        Photon.Pun.RpcTarget.All,
                        targetCharacter.photonView.Owner.ActorNumber,
                        hungerAmount
                    );
                }
                else
                {
                    log.LogWarning($"[PeakPelago] Cannot route hunger trap to {characterName} - no PhotonView available");
                    return;
                }

                log.LogInfo($"[PeakPelago] Hunger Trap scheduled for {characterName}!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying hunger trap: {ex.Message}");
            }
        }

        public static IEnumerator ApplyStatusNextFrame(Character targetCharacter, CharacterAfflictions.STATUSTYPE type, float amount, ManualLogSource log)
        {
            yield return new WaitForFixedUpdate();

            if (targetCharacter == null || targetCharacter.refs.afflictions == null)
            {
                yield break;
            }

            if (type == CharacterAfflictions.STATUSTYPE.Hunger && !targetCharacter.refs.afflictions.canGetHungry)
            {
                log.LogWarning("[PeakPelago] Hunger trap had no effect - target is protected by a campfire buff");
                yield break;
            }

            if (!targetCharacter.refs.afflictions.AddStatus(type, amount))
            {
                log.LogWarning($"[PeakPelago] Hunger trap was rejected for status {type}");
            }
        }
    }
}