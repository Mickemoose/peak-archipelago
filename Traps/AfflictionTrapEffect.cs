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
        /// CharacterAfflictions.STATUSTYPE.Injury,
		/// CharacterAfflictions.STATUSTYPE.Hunger,
		/// CharacterAfflictions.STATUSTYPE.Cold,
		/// CharacterAfflictions.STATUSTYPE.Poison,
		/// CharacterAfflictions.STATUSTYPE.Crab,
		/// CharacterAfflictions.STATUSTYPE.Curse,
		/// CharacterAfflictions.STATUSTYPE.Drowsy,
		/// CharacterAfflictions.STATUSTYPE.Weight,
		/// CharacterAfflictions.STATUSTYPE.Hot,
		/// CharacterAfflictions.STATUSTYPE.Thorns
        /// </summary>
        /// <param name="log"></param>
        /// <param name="targetMode"></param>
        /// <param name="amount"></param>
        /// <param name="type"></param>
        public static void ApplyAfflictionTrap(ManualLogSource log, TargetMode targetMode = TargetMode.RandomPlayer, float amount = 1.0f, CharacterAfflictions.STATUSTYPE type = CharacterAfflictions.STATUSTYPE.Cold)
        {
            try
            {
                Character targetCharacter;

                if (targetMode == TargetMode.RandomPlayer)
                {
                    // Use the static AllCharacters list for random selection
                    if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply trap - no characters found");
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
                        log.LogWarning("[PeakPelago] Cannot apply trap - no valid characters found");
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
                    log.LogWarning("[PeakPelago] Cannot apply trap - target character or afflictions not found");
                    return;
                }

                string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
                log.LogInfo($"[PeakPelago] Applying Trap to {characterName}");

                // Use the character's PhotonView to apply the affliction over the network
                if (targetCharacter.refs.view != null)
                {
                    // Call the RPC on the target character's PhotonView
                    targetCharacter.refs.view.RPC("RPCA_AddAffliction", RpcTarget.All, (int)type, amount);
                    log.LogInfo($"[PeakPelago] Trap applied to {characterName} via RPC!");
                }
                else
                {
                    log.LogWarning($"[PeakPelago] Cannot apply trap - target character {characterName} has no PhotonView");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying trap: {ex.Message}");
            }
        }
    }
}