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

                // Apply directly using the CharacterAfflictions' own RPC system
                // This should broadcast to all clients, and each client will apply it to their local character if they own it
                targetCharacter.refs.afflictions.photonView.RPC("ApplyStatusesFromFloatArrayRPC", RpcTarget.All, CreateStatusArray(type, amount));
                
                log.LogInfo($"[PeakPelago] Sent affliction RPC via character's PhotonView!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        private static float[] CreateStatusArray(CharacterAfflictions.STATUSTYPE type, float amount)
        {
            // Create an array with all status types set to 0, except the target type
            int statusCount = Enum.GetNames(typeof(CharacterAfflictions.STATUSTYPE)).Length;
            float[] statusArray = new float[statusCount];
            statusArray[(int)type] = amount;
            return statusArray;
        }
    }
}