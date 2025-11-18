using System;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class DropEverythingTrapEffect
    {
        private static ManualLogSource _log;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
        }

        public static void ApplyDropEverythingTrap(ManualLogSource log)
        {
            try
            {
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Drop Everything Trap - no characters found");
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
                    log.LogWarning("[PeakPelago] Cannot apply Drop Everything Trap - no valid characters found");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Drop Everything Trap to {validCharacters.Count} player(s) via RPC!");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartDropEverythingTrapRPC",
                        RpcTarget.All
                    );
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, applying locally only");
                    ApplyDropEverythingTrapLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Drop Everything Trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static void ApplyDropEverythingTrapLocal(ManualLogSource log)
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Drop Everything Trap - no local character");
                    return;
                }

                if (Character.localCharacter.refs.items == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Drop Everything Trap - character has no items component");
                    return;
                }

                log.LogInfo("[PeakPelago] Dropping all items for local player (including backpack)");
                
                // Drop all items including backpack
                Character.localCharacter.refs.items.DropAllItems(includeBackpack: true);

                log.LogInfo("[PeakPelago] Drop Everything Trap complete!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Drop Everything Trap locally: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}