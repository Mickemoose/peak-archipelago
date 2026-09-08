using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using Zorro.Core;

namespace Peak.AP
{
    public static class EmergencyRescueTrapEffect
    {
        private static PeakHandler _cachedHandler;

        public static PeakHandler ResolvePeakHandler()
        {
            var active = Singleton<PeakHandler>.Instance;
            if (active != null) return active;

            if (_cachedHandler != null && _cachedHandler.gameObject.scene.isLoaded) return _cachedHandler;
            _cachedHandler = null;

            foreach (var handler in Resources.FindObjectsOfTypeAll<PeakHandler>())
            {
                if (handler == null || !handler.gameObject.scene.isLoaded) continue;
                _cachedHandler = handler;
                break;
            }

            return _cachedHandler;
        }

        public static bool HelicopterAlreadySummoned()
        {
            var handler = ResolvePeakHandler();
            return handler != null && handler.summonedHelicopter;
        }

        public static void ApplyEmergencyRescueTrap(ManualLogSource log)
        {
            try
            {
                var handler = ResolvePeakHandler();
                if (handler == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Emergency Rescue Trap - PeakHandler not found in the loaded level");
                    return;
                }

                if (handler.summonedHelicopter)
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
                    RepositionAndSummon(abovePlayer, log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Emergency Rescue Trap: {ex.Message}");
            }
        }

        public static void RepositionAndSummon(Vector3 position, ManualLogSource log)
        {
            var handler = ResolvePeakHandler();
            if (handler == null || handler.peakSequence == null)
            {
                log?.LogWarning("[PeakPelago] Emergency Rescue - no PeakHandler or peak sequence in the loaded level, cannot summon helicopter");
                return;
            }

            var sequence = handler.peakSequence;
            var parent = sequence.transform.parent;
            if (parent != null && !parent.gameObject.activeInHierarchy)
            {
                sequence.transform.SetParent(null, true);
                log?.LogInfo("[PeakPelago] Emergency Rescue - detached the peak sequence from the inactive peak segment");
            }

            sequence.transform.position = position;
            var seq = sequence.GetComponent<PeakSequence>();
            if (seq != null)
                seq.totalWinningSeconds = seq.totalSeconds;
            // Ascent 8+ requires a Nadir win, so don't hand out a plain forced win there
            if (!Ascents.shouldRequireNadir)
            {
                Character.forceWin = true;
            }
            handler.SummonHelicopter();
        }
    }
}
