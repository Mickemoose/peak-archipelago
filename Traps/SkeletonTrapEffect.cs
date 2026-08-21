using System;
using BepInEx.Logging;

namespace Peak.AP
{
    public static class SkeletonTrapEffect
    {
        public static void ApplySkeletonTrap(ManualLogSource log)
        {
            try
            {
                var target = TrapHelpers.GetRandomValidCharacter();
                if (target == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply skeleton trap - no valid characters found");
                    return;
                }

                if (target.data.isSkeleton)
                {
                    log.LogInfo("[PeakPelago] Skeleton trap target is already a skeleton");
                    return;
                }

                string characterName = target == Character.localCharacter
                    ? "local player"
                    : target.characterName;
                log.LogInfo($"[PeakPelago] Applying Skeleton Trap! {characterName} is now a living skeleton");

                target.data.SetSkeleton(true);
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying skeleton trap: {ex.Message}");
            }
        }
    }
}
