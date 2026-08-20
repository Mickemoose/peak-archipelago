using HarmonyLib;

namespace Peak.AP
{
    [HarmonyPatch(typeof(Character), "RPCEndGame")]
    public static class RunEndedPatch
    {
        static void Postfix()
        {
            PeakArchipelagoPlugin.RunEnded = true;
        }
    }

    [HarmonyPatch(typeof(EruptionSpawner), "Start")]
    public static class EruptionPrefabCapturePatch
    {
        static void Postfix(EruptionSpawner __instance)
        {
            if (__instance != null && __instance.eruption != null)
            {
                EruptionTrapEffect.SetCachedPrefab(__instance.eruption);
            }
        }
    }

    [HarmonyPatch(typeof(PeakHandler), nameof(PeakHandler.EndCutsceneFinal))]
    public static class NadirEndingPatch
    {
        static void Postfix()
        {
            PeakArchipelagoPlugin._instance?.OnRunEndedInNadir();
        }
    }
}
