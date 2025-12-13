using System;
using BepInEx.Logging;
using HarmonyLib;

namespace Peak.AP
{
    [HarmonyPatch(typeof(ScoutCannonAchievementZone), "TestAchievement")]
    public static class ScoutCannonAchievementZonePatch
    {
        private static ManualLogSource _log => PeakArchipelagoPlugin._instance?._log;

        static void Postfix(ScoutCannonAchievementZone __instance, bool ___playerWasLaunched)
        {
            try
            {
                if (PeakArchipelagoPlugin._instance == null) return;

                // Same conditions apply though we check them again here for safety
                if (___playerWasLaunched &&
                    Character.localCharacter != null &&
                    Character.localCharacter.data.fallSeconds <= 0f &&
                    Character.localCharacter.data.isGrounded &&
                    Character.localCharacter.Center.y >= __instance.bounds.min.y)
                {
                    _log?.LogInfo("[PeakPelago] Daredevil Badge conditions met - reporting check");
                    PeakArchipelagoPlugin._instance.ReportCheckByName("Daredevil Badge");
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[PeakPelago] ScoutCannonAchievementZone patch error: {ex.Message}");
            }
        }
    }
}