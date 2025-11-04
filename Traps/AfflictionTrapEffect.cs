using System;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;

namespace Peak.AP
{
    public static class AfflictionTrapEffect
    {
        public enum TargetMode
        {
            LocalPlayer,
            RandomPlayer
        }

        /// <summary>
        /// Applies an affliction trap to a target character, either the local player or a random player.
        /// Available status types:
        /// - CharacterAfflictions.STATUSTYPE.Injury
        /// - CharacterAfflictions.STATUSTYPE.Hunger
        /// - CharacterAfflictions.STATUSTYPE.Cold
        /// - CharacterAfflictions.STATUSTYPE.Poison
        /// - CharacterAfflictions.STATUSTYPE.Crab
        /// - CharacterAfflictions.STATUSTYPE.Curse
        /// - CharacterAfflictions.STATUSTYPE.Drowsy
        /// - CharacterAfflictions.STATUSTYPE.Weight
        /// - CharacterAfflictions.STATUSTYPE.Hot
        /// - CharacterAfflictions.STATUSTYPE.Thorns
        /// </summary>
        public static void ApplyAfflictionTrap(
            ManualLogSource log, 
            TargetMode targetMode, 
            float amount, 
            CharacterAfflictions.STATUSTYPE type,
            PhotonView modPhotonView)
        {
            try
            {
                if (modPhotonView == null)
                {
                    log.LogError("[PeakPelago] Cannot apply affliction trap - PhotonView is null");
                    return;
                }

                Character targetCharacter;

                if (targetMode == TargetMode.RandomPlayer)
                {
                    // Get all valid characters
                    if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply trap - no characters found");
                        return;
                    }

                    // Filter to only active, alive characters
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

                    // Pick a random character
                    var random = new System.Random();
                    targetCharacter = validCharacters[random.Next(validCharacters.Count)];
                }
                else
                {
                    // Target local player
                    targetCharacter = Character.localCharacter;
                }

                if (targetCharacter == null || targetCharacter.photonView == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply trap - target character or PhotonView not found");
                    return;
                }

                int targetActorNumber = targetCharacter.photonView.Owner.ActorNumber;
                string characterName = targetCharacter == Character.localCharacter 
                    ? "local player" 
                    : targetCharacter.characterName;

                log.LogInfo($"[PeakPelago] Sending {type} trap ({amount}) to actor {targetActorNumber} ({characterName})");

                // Send RPC to all clients - each client will check if it's their character
                modPhotonView.RPC("ApplyAfflictionToPlayer", RpcTarget.All, targetActorNumber, (int)type, amount);
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying affliction trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}