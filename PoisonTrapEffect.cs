using System;
using BepInEx.Logging;

namespace Peak.AP
{
    public static class PoisonTrapEffect
    {
        public enum PoisonTrapType
        {
            Minor,
            Normal,
            Deadly
        }

        public static void ApplyPoisonTrap(PoisonTrapType trapType, ManualLogSource log)
        {
            try
            {
                if (Character.localCharacter == null || Character.localCharacter.refs.afflictions == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply poison - no local character or afflictions");
                    return;
                }

                float poisonAmount;
                string severity;

                switch (trapType)
                {
                    case PoisonTrapType.Minor:
                        poisonAmount = 0.025f;
                        severity = "Minor";
                        break;
                    
                    case PoisonTrapType.Normal:
                        poisonAmount = 0.05f;
                        severity = "Normal";
                        break;
                    
                    case PoisonTrapType.Deadly:
                        poisonAmount = 0.75f;
                        severity = "Deadly";
                        break;
                    
                    default:
                        poisonAmount = 0.25f;
                        severity = "Normal";
                        break;
                }

                Character.localCharacter.refs.afflictions.AddStatus(
                    CharacterAfflictions.STATUSTYPE.Poison, 
                    poisonAmount
                );
                
                log.LogInfo($"[PeakPelago] Applied {severity} Poison Trap - {poisonAmount} poison added");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying poison trap: {ex.Message}");
            }
        }
    }
}