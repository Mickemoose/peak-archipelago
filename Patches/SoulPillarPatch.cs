using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    [HarmonyPatch(typeof(Peak.ScoutmasterSoulPillar), "RPC_Break")]
    public static class SoulPillarPatch
    {
        public static ManualLogSource Log;
        public static bool SoulOwnedSynced;
        private static float _lastBlockedAt = -10f;

        static bool Prefix(Peak.ScoutmasterSoulPillar __instance, int type, PhotonView view)
        {
            if (type != 0) return true;
            if (SoulOwnedSynced) return true;
            if (__instance._broken) return true;

            __instance.charactersTryingToBreak = 0;
            if (__instance.sphereAnimator != null) __instance.sphereAnimator.SetBool("Interacting", false);
            if (__instance.chargeSFX != null) __instance.chargeSFX.Stop();
            if (__instance.sphereGlow != null) __instance.sphereGlow.Stop();

            if (Time.unscaledTime - _lastBlockedAt > 2f)
            {
                _lastBlockedAt = Time.unscaledTime;
                bool isLocalInteractor = view != null && view.IsMine;
                PeakArchipelagoPlugin._instance?.OnPillarBreakBlocked(isLocalInteractor);
            }
            return false;
        }

        public static void ApplyPillarVisuals()
        {
            var pillars = Object.FindObjectsByType<Peak.ScoutmasterSoulPillar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var pillar in pillars)
            {
                if (pillar == null) continue;
                if (pillar.scoutmasterAnimator != null)
                {
                    pillar.scoutmasterAnimator.gameObject.SetActive(false);
                }
            }
            if (pillars.Length > 0)
            {
                Log?.LogInfo($"[PeakPelago] Soul pillar ghost hidden on {pillars.Length} pillar(s) - the soul is never displayed in the pillar");
            }
        }
    }
}
