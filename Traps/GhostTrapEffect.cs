using System;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class GhostTrapEffect
    {
        public static void ApplyGhostTrap(ManualLogSource log)
        {
            try
            {
                var targetCharacter = TrapHelpers.GetRandomValidCharacter();
                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply ghost trap - no valid characters found");
                    return;
                }

                if (!PhotonNetwork.IsMasterClient) return;

                string characterName = targetCharacter == Character.localCharacter
                    ? "local player"
                    : targetCharacter.characterName;

                Vector3 center = targetCharacter.Center;
                Vector3 spawnPosition = center + new Vector3(UnityEngine.Random.Range(-10f, 10f), 15f, UnityEngine.Random.Range(-10f, 10f));

                var ghostObj = PhotonNetwork.Instantiate("GhostBall", spawnPosition, Quaternion.identity, 0);
                if (ghostObj == null)
                {
                    log.LogError("[PeakPelago] Failed to instantiate GhostBall");
                    return;
                }

                var ghost = ghostObj.GetComponent<Peak.GhostBall>();
                if (ghost != null)
                {
                    ghost.maxHeight = center.y + 40f;
                    ghost.chaseHeight = center.y + 8f;
                }

                log.LogInfo($"[PeakPelago] Ghost Trap spawned a GhostBall near {characterName}");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying ghost trap: {ex.Message}");
            }
        }
    }
}
