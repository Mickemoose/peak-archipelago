using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    [HarmonyPatch(typeof(ScoutStatue), nameof(ScoutStatue.SpawnScoutsHonor))]
    public static class ScoutStatueHonorGatePatch
    {
        public static ManualLogSource Log;

        static bool Prefix()
        {
            if (UnlockedItemsManager.CanCreateScoutsHonor())
            {
                return true;
            }

            Log?.LogInfo("[PeakPelago] Scout Statue blocked from creating Scout's Honor - Scout's Honor Unlock not yet received");
            return false;
        }

        public static void RetriggerStatues()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!UnlockedItemsManager.CanCreateScoutsHonor()) return;

            var statues = Object.FindObjectsByType<ScoutStatue>(FindObjectsSortMode.None);
            foreach (var statue in statues)
            {
                if (statue != null)
                {
                    statue.OnAmuletsChanged();
                }
            }

            if (statues.Length > 0)
            {
                Log?.LogInfo($"[PeakPelago] Rechecked {statues.Length} Scout Statue(s) after Scout's Honor Unlock");
            }
        }
    }

    [HarmonyPatch(typeof(ScoutStatue), nameof(ScoutStatue.SpawnGem_Master))]
    public static class ScoutStatueGemGatePatch
    {
        static bool Prefix()
        {
            if (UnlockedItemsManager.CanSpawnStrangeGem())
            {
                return true;
            }

            ScoutStatueHonorGatePatch.Log?.LogInfo("[PeakPelago] Scout Statue blocked from spawning Strange Gem - Strange Gem Unlock not yet received");
            return false;
        }

        public static void RetriggerGemSpawns()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!UnlockedItemsManager.CanSpawnStrangeGem()) return;

            var statues = Object.FindObjectsByType<ScoutStatue>(FindObjectsSortMode.None);
            foreach (var statue in statues)
            {
                if (statue != null)
                {
                    statue.SpawnGem_Master();
                }
            }

            if (statues.Length > 0)
            {
                ScoutStatueHonorGatePatch.Log?.LogInfo($"[PeakPelago] Spawned Strange Gem on {statues.Length} Scout Statue(s) after Strange Gem Unlock");
            }
        }
    }
}
