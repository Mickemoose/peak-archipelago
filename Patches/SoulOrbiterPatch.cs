using HarmonyLib;
using Photon.Pun;

namespace Peak.AP
{
    [HarmonyPatch(typeof(Peak.ScoutmasterGhostOrbiter), "Start")]
    public static class SoulOrbiterSuppressPatch
    {
        static void Postfix(Peak.ScoutmasterGhostOrbiter __instance)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            var view = __instance.view != null ? __instance.view : __instance.GetComponent<PhotonView>();
            if (view == null || !view.IsMine) return;

            if (!SoulOrbiterManager.IsOurs(view.ViewID))
            {
                PhotonNetwork.Destroy(__instance.gameObject);
            }
        }
    }
}
