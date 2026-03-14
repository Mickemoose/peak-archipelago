using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class InvertedMouseTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;
        public static bool IsActive { get; private set; } = false;

        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
        }

        public static void ApplyInvertedMouseTrap(ManualLogSource log)
        {
            try
            {
                if (IsActive)
                {
                    log.LogInfo("[PeakPelago] Inverted Mouse Trap already active, skipping");
                    return;
                }

                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Inverted Mouse Trap - no characters found");
                    return;
                }

                var validCharacters = Character.AllCharacters.Where(c =>
                    c != null &&
                    c.gameObject.activeInHierarchy &&
                    !c.data.dead &&
                    !c.data.fullyPassedOut
                ).ToList();

                if (validCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Inverted Mouse Trap - no valid characters found");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Inverted Mouse Trap to {validCharacters.Count} player(s) via RPC!");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartInvertedMouseTrapRPC",
                        RpcTarget.All
                    );
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    ApplyInvertedMouseTrapLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Inverted Mouse Trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static void ApplyInvertedMouseTrapLocal(ManualLogSource log)
        {
            try
            {
                if (IsActive)
                {
                    log.LogInfo("[PeakPelago] Inverted Mouse Trap already active locally");
                    return;
                }

                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Inverted Mouse Trap - no local character");
                    return;
                }

                log.LogInfo("[PeakPelago] Starting Inverted Mouse Trap locally");
                _plugin.StartCoroutine(InvertedMouseTrapCoroutine(log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Inverted Mouse Trap locally: {ex.Message}");
            }
        }

        private static IEnumerator InvertedMouseTrapCoroutine(ManualLogSource log)
        {
            IsActive = true;
            log.LogInfo("[PeakPelago] Inverting mouse controls for 10 seconds");

            yield return new WaitForSeconds(10f);

            log.LogInfo("[PeakPelago] Inverted Mouse Trap complete!");
            IsActive = false;
            PeakArchipelagoPlugin._instance?._trapLinkService?.NotifyTrapComplete();
        }
    }

    /// <summary>
    /// Patches CharacterMovement.CameraLook to negate lookInput before it's applied,
    /// then restores it after so other systems aren't affected.
    /// </summary>
    [HarmonyPatch(typeof(CharacterMovement), "CameraLook")]
    public static class InvertedMouseCameraLookPatch
    {
        [HarmonyPrefix]
        static void Prefix(CharacterMovement __instance)
        {
            if (!InvertedMouseTrapEffect.IsActive) return;

            var character = Traverse.Create(__instance).Field("character").GetValue<Character>();
            if (character != null && character.IsLocal)
            {
                character.input.lookInput.x = -character.input.lookInput.x;
                character.input.lookInput.y = -character.input.lookInput.y;
            }
        }

        [HarmonyPostfix]
        static void Postfix(CharacterMovement __instance)
        {
            if (!InvertedMouseTrapEffect.IsActive) return;

            var character = Traverse.Create(__instance).Field("character").GetValue<Character>();
            if (character != null && character.IsLocal)
            {
                // Restore original values so other systems aren't affected
                character.input.lookInput.x = -character.input.lookInput.x;
                character.input.lookInput.y = -character.input.lookInput.y;
            }
        }
    }
}
