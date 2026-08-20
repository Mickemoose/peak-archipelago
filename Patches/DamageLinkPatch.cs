using HarmonyLib;

namespace Peak.AP
{
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus))]
    public static class DamageLinkInjuryPatch
    {
        public static bool Suppress;

        static void Postfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool __result)
        {
            if (!__result || Suppress) return;
            if (statusType != CharacterAfflictions.STATUSTYPE.Injury) return;

            var character = __instance.character;
            if (character == null || !character.IsLocal || character.isBot) return;

            PeakArchipelagoPlugin._instance?.NotifyDamageLinkInjury(amount, character.characterName);
        }
    }
}
