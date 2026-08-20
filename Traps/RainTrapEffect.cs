using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class RainTrapEffect
    {
        private const float Duration = 40f;

        private static readonly FieldInfo UntilSwitchField = AccessTools.Field(typeof(WindChillZone), "untilSwitch");
        private static readonly FieldInfo TimeUntilNextWindField = AccessTools.Field(typeof(WindChillZone), "timeUntilNextWind");

        public static void ApplyRainTrap(ManualLogSource log)
        {
            try
            {
                if (HarvestedTemplates.RainStorm == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply rain trap - rain storm template not harvested");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Rain Trap! Rain for {Duration} seconds");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("StartRainTrapRPC", RpcTarget.All);
                }
                else
                {
                    ActivateRainLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying rain trap: {ex.Message}");
            }
        }

        public static void ActivateRainLocal(ManualLogSource log)
        {
            PeakArchipelagoPlugin._instance.StartCoroutine(RainCoroutine(log));
        }

        private static IEnumerator RainCoroutine(ManualLogSource log)
        {
            if (HarvestedTemplates.RainStorm == null) yield break;

            var previousInstance = WindChillZone.instance;

            var stormObj = UnityEngine.Object.Instantiate(HarvestedTemplates.RainStorm);
            stormObj.name = "AP_RainStorm";
            stormObj.transform.position = Character.localCharacter != null
                ? Character.localCharacter.Center
                : Vector3.zero;
            stormObj.SetActive(true);

            var zone = stormObj.GetComponentInChildren<WindChillZone>(true);
            if (zone == null)
            {
                log.LogWarning("[PeakPelago] Harvested rain storm has no WindChillZone - cleaning up");
                UnityEngine.Object.Destroy(stormObj);
                WindChillZone.instance = previousInstance;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                zone.windActive = true;
                zone.windZoneBounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
                UntilSwitchField?.SetValue(zone, Duration - elapsed + 1f);
                TimeUntilNextWindField?.SetValue(zone, Duration - elapsed + 1f);
                yield return null;
            }

            zone.windActive = false;
            yield return new WaitForSeconds(2f);

            UnityEngine.Object.Destroy(stormObj);
            WindChillZone.instance = previousInstance;
            log.LogInfo("[PeakPelago] Rain Trap complete");
        }
    }
}
