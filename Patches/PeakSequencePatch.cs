using HarmonyLib;

namespace PeakArchipelago
{
    [HarmonyPatch(typeof(PeakSequence), "OnDisable")]
    public static class PeakSequencePatch
    {
        static void Postfix()
        {
            Character.forceWin = false;
        }
    }
}
