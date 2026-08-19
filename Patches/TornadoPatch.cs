using HarmonyLib;

namespace Peak.AP
{
    // A trap-spawned tornado has no TornadoSpawner to take target points from, so
    // targetParent stays null and vanilla target selection throws every frame.
    [HarmonyPatch(typeof(Tornado), "PickTarget")]
    public static class TornadoPickTargetPatch
    {
        static bool Prefix(Tornado __instance)
        {
            return __instance.targetParent != null;
        }
    }

    [HarmonyPatch(typeof(Tornado), "RPCA_SelectTargetPos")]
    public static class TornadoSelectTargetPosPatch
    {
        static bool Prefix(Tornado __instance)
        {
            return __instance.targetParent != null;
        }
    }
}
