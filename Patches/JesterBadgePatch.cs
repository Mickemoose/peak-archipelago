using HarmonyLib;

namespace Peak.AP
{
    [HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.AddToRunBasedInt))]
    public static class JesterBadgePatch
    {
        private const int CLOWN_LUGGAGE_REQUIRED = 2;

        static void Postfix(AchievementManager __instance, RUNBASEDVALUETYPE type)
        {
            if (type != RUNBASEDVALUETYPE.ClownLuggageOpened) return;
            if (__instance.GetRunBasedInt(RUNBASEDVALUETYPE.ClownLuggageOpened) < CLOWN_LUGGAGE_REQUIRED) return;

            PeakArchipelagoPlugin._instance?.ReportCheckByName("Jester Badge");
        }
    }
}
