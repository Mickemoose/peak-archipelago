using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace Peak.AP
{
    public static class SlipTrapEffect
    {
        public enum TargetMode
        {
            LocalPlayer,
            RandomPlayer
        }

        public static void ApplySlipTrap(ManualLogSource log, TargetMode targetMode = TargetMode.RandomPlayer)
        {
            try
            {
                Character targetCharacter;

                if (targetMode == TargetMode.RandomPlayer)
                {
                    if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply slippery trap - no characters found");
                        return;
                    }

                    var validCharacters = Character.AllCharacters.Where(c => 
                        c != null && 
                        c.gameObject.activeInHierarchy && 
                        !c.data.dead
                    ).ToList();

                    if (validCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply slippery trap - no valid characters found");
                        return;
                    }

                    var random = new System.Random();
                    targetCharacter = validCharacters[random.Next(validCharacters.Count)];
                }
                else
                {
                    targetCharacter = Character.localCharacter;
                }

                if (targetCharacter == null || targetCharacter.refs == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply slippery trap - target character or refs not found");
                    return;
                }

                string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
                log.LogInfo($"[PeakPelago] Applying Slippery Trap to {characterName}");

                // Apply it during the next fixed update to ensure proper timing
                targetCharacter.StartCoroutine(ApplySlipEffectNextFrame(targetCharacter, log));

                log.LogInfo($"[PeakPelago] Slippery Trap scheduled for {characterName}!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying slippery trap: {ex.Message}");
            }
        }

        private static IEnumerator ApplySlipEffectNextFrame(Character targetCharacter, ManualLogSource log)
        {
            yield return new WaitForFixedUpdate();

            try
            {
                Rigidbody footR = targetCharacter.GetBodypartRig(BodypartType.Foot_R);
                Rigidbody footL = targetCharacter.GetBodypartRig(BodypartType.Foot_L);
                Rigidbody hip = targetCharacter.GetBodypartRig(BodypartType.Hip);
                Rigidbody head = targetCharacter.GetBodypartRig(BodypartType.Head);

                if (footR == null || footL == null || hip == null || head == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply slip effect - missing body parts");
                    yield break;
                }

                targetCharacter.RPCA_Fall(2f);
                Vector3 forwardUpForce = (targetCharacter.data.lookDirection_Flat + Vector3.up) * 200f;
                footR.AddForce(forwardUpForce, ForceMode.Impulse);
                footL.AddForce(forwardUpForce, ForceMode.Impulse);
                hip.AddForce(Vector3.up * 1500f, ForceMode.Impulse);
                head.AddForce(targetCharacter.data.lookDirection_Flat * -300f, ForceMode.Impulse);
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error in slip effect coroutine: {ex.Message}");
            }
        }
    }
}