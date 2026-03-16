using System;
using BepInEx.Logging;
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

                log.LogInfo("[PeakPelago] Applying Emergency Rescue Trap! Summoning helicopter...");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("StartEmergencyRescueRPC", Photon.Pun.RpcTarget.All);
                }
                else
                {
                    Singleton<PeakHandler>.Instance.SummonHelicopter();
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Emergency Rescue Trap: {ex.Message}");
            }
        }
    }
}
