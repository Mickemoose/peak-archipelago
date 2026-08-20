using System;
using HarmonyLib;
using UnityEngine;

namespace Peak.AP
{
    [HarmonyPatch(typeof(Character), nameof(Character.AddForce), new Type[] { typeof(Vector3), typeof(float), typeof(float) })]
    public static class KnockbackLinkPatch
    {
        public static bool Suppress;

        private const float MIN_SEND_MAGNITUDE = 10f;

        static void Postfix(Character __instance, Vector3 move)
        {
            if (Suppress) return;
            if (__instance == null || !__instance.IsLocal || __instance.isBot) return;
            if (move.magnitude < MIN_SEND_MAGNITUDE) return;

            PeakArchipelagoPlugin._instance?.NotifyKnockbackLink(move, __instance.characterName);
        }
    }
}
