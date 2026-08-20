using System;
using BepInEx.Logging;
using Photon.Pun;

namespace Peak.AP
{
    public static class InstantCrystalTrapEffect
    {
        public static void ApplyInstantCrystalTrap(ManualLogSource log)
        {
            try
            {
                var targetCharacter = TrapHelpers.GetRandomValidCharacter();
                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Instant Crystal Trap - no valid characters found");
                    return;
                }

                if (targetCharacter.photonView?.Owner == null)
                {
                    log.LogWarning("[PeakPelago] Instant Crystal Trap target has no PhotonView owner");
                    return;
                }

                string characterName = targetCharacter == Character.localCharacter
                    ? "local player"
                    : targetCharacter.characterName;
                log.LogInfo($"[PeakPelago] Instant Crystal Trap petrifying {characterName}");

                int actorNumber = targetCharacter.photonView.Owner.ActorNumber;
                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("PetrifyPlayerRPC", RpcTarget.All, actorNumber);
                }
                else
                {
                    PetrifyLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Instant Crystal Trap: {ex.Message}");
            }
        }

        public static void PetrifyLocal(ManualLogSource log)
        {
            var character = Character.localCharacter;
            if (character == null || character.data.dead) return;
            character.data.SetPetrify(100);
            log.LogInfo("[PeakPelago] Instant Crystal Trap - fully petrified");
        }
    }
}
