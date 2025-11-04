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
                var getBodypartRigMethod = targetCharacter.GetType().GetMethod(
                    "GetBodypartRig", 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance
                );

                if (getBodypartRigMethod == null)
                {
                    log.LogError("[PeakPelago] Could not find GetBodypartRig method");
                    
                    var allMethods = targetCharacter.GetType().GetMethods(
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance
                    );
                    var bodypartMethods = allMethods.Where(m => m.Name.Contains("Bodypart") || m.Name.Contains("bodypart")).ToList();
                    log.LogInfo($"[PeakPelago] Available methods containing 'Bodypart': {string.Join(", ", bodypartMethods.Select(m => m.Name))}");
                    
                    yield break;
                }

                var bodypartTypeType = System.AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => {
                        try { return a.GetTypes(); }
                        catch { return []; }
                    })
                    .FirstOrDefault(t => t.Name == "BodypartType");
                    
                if (bodypartTypeType == null)
                {
                    log.LogError("[PeakPelago] Could not find BodypartType enum");
                    yield break;
                }

                log.LogInfo($"[PeakPelago] Found BodypartType: {bodypartTypeType.FullName}");

                object footRType = Enum.Parse(bodypartTypeType, "Foot_R");
                object footLType = Enum.Parse(bodypartTypeType, "Foot_L");
                object hipType = Enum.Parse(bodypartTypeType, "Hip");
                object headType = Enum.Parse(bodypartTypeType, "Head");

                Rigidbody footR = getBodypartRigMethod.Invoke(targetCharacter, [footRType]) as Rigidbody;
                Rigidbody footL = getBodypartRigMethod.Invoke(targetCharacter, [footLType]) as Rigidbody;
                Rigidbody hip = getBodypartRigMethod.Invoke(targetCharacter, [hipType]) as Rigidbody;
                Rigidbody head = getBodypartRigMethod.Invoke(targetCharacter, [headType]) as Rigidbody;

                if (footR == null || footL == null || hip == null || head == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply slip effect - missing body parts");
                    log.LogWarning($"[PeakPelago] footR: {footR != null}, footL: {footL != null}, hip: {hip != null}, head: {head != null}");
                    yield break;
                }

                targetCharacter.RPCA_Fall(2f);
                
                Vector3 forwardUpForce = (targetCharacter.data.lookDirection_Flat + Vector3.up) * 200f;
                footR.AddForce(forwardUpForce, ForceMode.Impulse);
                footL.AddForce(forwardUpForce, ForceMode.Impulse);
                hip.AddForce(Vector3.up * 1500f, ForceMode.Impulse);
                head.AddForce(targetCharacter.data.lookDirection_Flat * -300f, ForceMode.Impulse);
                
                log.LogInfo($"[PeakPelago] Successfully applied slip effect to {targetCharacter.characterName ?? "player"}!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error in slip effect coroutine: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}