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
                var targetCharacter = TrapHelpers.GetRandomValidCharacter();

                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply instant death trap - no valid characters found");
                    return;
                }

                string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
                
                log.LogInfo($"[PeakPelago] Applying Instant Death Trap to: {characterName}");

                // DieInstantly resolves checkpoints against localCharacter, so it has to run on the victim's client
                if (targetCharacter == Character.localCharacter)
                {
                    targetCharacter.StartCoroutine(KillCharacterNextFrame(targetCharacter, characterName, log));
                }
                else if (PeakArchipelagoPlugin._instance?.PhotonView != null && targetCharacter.photonView?.Owner != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "InstantDeathTrapRPC",
                        Photon.Pun.RpcTarget.All,
                        targetCharacter.photonView.Owner.ActorNumber
                    );
                }
                else
                {
                    log.LogWarning($"[PeakPelago] Cannot route instant death trap to {characterName} - no PhotonView available");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying instant death trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static IEnumerator KillCharacterNextFrame(Character targetCharacter, string characterName, ManualLogSource log)
        {
            yield return null;

            try
            {
                log.LogInfo($"[PeakPelago] Executing instant death for {characterName}");

                targetCharacter.DieInstantly();
                log.LogInfo($"[PeakPelago] Instant Death Trap killed {characterName}!");
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