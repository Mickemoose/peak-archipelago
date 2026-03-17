using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using Zorro.Core;

namespace Peak.AP
{
    public static class EmergencyRescueTrapEffect
    {
        public static void ApplyEmergencyRescueTrap(ManualLogSource log)
        {
            try
            {
                if (Singleton<PeakHandler>.Instance == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Emergency Rescue Trap - PeakHandler not found");
                    return;
                }

                if (Singleton<PeakHandler>.Instance.summonedHelicopter)
                {
                    log.LogInfo("[PeakPelago] Emergency Rescue Trap - helicopter already summoned, skipping");
                    return;
                }

                List<Character> players = PlayerHandler.GetAllPlayerCharacters();
                if (players == null || players.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Emergency Rescue Trap - no players found");
                    return;
                }

                Character target = players[UnityEngine.Random.Range(0, players.Count)];
                Vector3 abovePlayer = target.transform.position + Vector3.up * 35f;

                log.LogInfo($"[PeakPelago] Applying Emergency Rescue Trap! Summoning helicopter above {target.photonView.Owner.NickName}...");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("StartEmergencyRescueRPC", Photon.Pun.RpcTarget.All, abovePlayer);
                }
                else
                {
                    RepositionAndSummon(abovePlayer);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Emergency Rescue Trap: {ex.Message}");
            }
        }

        public static void RepositionAndSummon(Vector3 position)
        {
            var handler = Singleton<PeakHandler>.Instance;
            handler.peakSequence.transform.position = position;
            var seq = handler.peakSequence.GetComponent<PeakSequence>();
            if (seq != null)
                seq.totalWinningSeconds = seq.totalSeconds;
            Character.forceWin = true;
            handler.SummonHelicopter();
        }
    }
}
