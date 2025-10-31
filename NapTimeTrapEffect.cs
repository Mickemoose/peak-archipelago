using System;
using BepInEx.Logging;

namespace Peak.AP
{
    public static class NapTimeTrapEffect
    {

        public static void ApplyNapTrap(ManualLogSource log)
        {
            try
            {
                if (Character.localCharacter == null || Character.localCharacter.refs.afflictions == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply nap time trap - no local character or afflictions");
                    return;
                }

                float drowsyAmount = 0.25f;


                Character.localCharacter.refs.afflictions.AddStatus(
                    CharacterAfflictions.STATUSTYPE.Drowsy, 
                    drowsyAmount
                );
                
                log.LogInfo($"[PeakPelago] Applied Nap Time Trap");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying nap time trap: {ex.Message}");
            }
        }
    }
}