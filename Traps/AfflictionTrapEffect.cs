using System;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;

namespace Peak.AP
{
    public static class AfflictionTrapEffect
    {
        private static ManualLogSource _log;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
        }

        public enum TargetMode
        {
            LocalPlayer,
            RandomPlayer
        }

        public static void ApplyAfflictionTrap(ManualLogSource log, TargetMode targetMode = TargetMode.RandomPlayer, float amount = 1.0f, CharacterAfflictions.STATUSTYPE type = CharacterAfflictions.STATUSTYPE.Cold)
        {
            try
            {
                Character targetCharacter;

                if (targetMode == TargetMode.RandomPlayer)
                {
                    if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply trap - no characters found");
                        return;
                    }

                    // Filter to only ALIVE characters
                    var validCharacters = Character.AllCharacters.Where(c =>
                        c != null &&
                        c.gameObject.activeInHierarchy &&
                        !c.data.dead &&
                        !c.data.fullyPassedOut &&
                        c.photonView != null &&
                        c.photonView.Owner != null
                    ).ToList();

                    if (validCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply trap - no valid characters found");
                        return;
                    }

                    var random = new Random();
                    targetCharacter = validCharacters[random.Next(validCharacters.Count)];
                }
                else
                {
                    targetCharacter = Character.localCharacter;
                }

                if (targetCharacter == null || targetCharacter.refs.afflictions == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply trap - target character or afflictions not found");
                    return;
                }

                if (targetCharacter.photonView == null || targetCharacter.photonView.Owner == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply trap - target has no PhotonView");
                    return;
                }

                int actorNumber = targetCharacter.photonView.Owner.ActorNumber;
                string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
                
                log.LogInfo($"[PeakPelago] Applying {type} ({amount}) trap to {characterName} (Actor {actorNumber})");

                // Send RPC to ALL clients with the target actor number
                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "ApplyAfflictionToPlayer", 
                        RpcTarget.All,  // Send to ALL clients
                        actorNumber,    // They'll check if it's for them
                        (int)type, 
                        amount
                    );
                    log.LogInfo($"[PeakPelago] Sent affliction RPC to all clients for actor {actorNumber}");
                }
                else
                {
                    log.LogWarning("[PeakPelago] Plugin PhotonView not available");
                }
                
                log.LogInfo($"[PeakPelago] Applied affliction successfully!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}