using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class StormTrapEffect
    {
        private const float Duration = 40f;
        private const float GustForce = 20f;
        private const float ForceRadius = 2f;

        private static readonly FieldInfo UntilSwitchField = AccessTools.Field(typeof(WindChillZone), "untilSwitch");
        private static readonly FieldInfo TimeUntilNextWindField = AccessTools.Field(typeof(WindChillZone), "timeUntilNextWind");

        public static void ApplyStormTrap(ManualLogSource log)
        {
            try
            {
                if (HarvestedTemplates.RainStorm == null && HarvestedTemplates.WindZone == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply storm trap - no storm templates harvested");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Storm Trap! Full storm for {Duration} seconds");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("StartStormTrapRPC", RpcTarget.All);
                }
                else
                {
                    ActivateStormLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying storm trap: {ex.Message}");
            }
        }

        public static void ActivateStormLocal(ManualLogSource log)
        {
            PeakArchipelagoPlugin._instance.StartCoroutine(StormCoroutine(log));
        }

        private static GameObject SpawnZoneClone(GameObject template, string name, Vector3 position, bool stripFog)
        {
            if (template == null) return null;
            var obj = UnityEngine.Object.Instantiate(template);
            obj.name = name;
            obj.transform.position = position;
            if (stripFog)
            {
                foreach (var fog in obj.GetComponentsInChildren<FogConfig>(true))
                {
                    UnityEngine.Object.DestroyImmediate(fog);
                }
            }
            obj.SetActive(true);
            return obj;
        }

        private static void DriveZone(GameObject obj, Vector3 windDir, float remaining)
        {
            if (obj == null) return;
            var zone = obj.GetComponentInChildren<WindChillZone>(true);
            if (zone == null) return;
            zone.windActive = true;
            zone.currentWindDirection = windDir;
            zone.windZoneBounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            UntilSwitchField?.SetValue(zone, remaining + 1f);
            TimeUntilNextWindField?.SetValue(zone, remaining + 1f);
        }

        private static void StopZone(GameObject obj)
        {
            if (obj == null) return;
            var zone = obj.GetComponentInChildren<WindChillZone>(true);
            if (zone != null) zone.windActive = false;
        }

        private static IEnumerator StormCoroutine(ManualLogSource log)
        {
            var previousInstance = WindChillZone.instance;
            Vector3 position = Character.localCharacter != null ? Character.localCharacter.Center : Vector3.zero;
            Vector3 windDir = UnityEngine.Random.insideUnitSphere;
            windDir.y = 0f;
            windDir = windDir.sqrMagnitude > 0.01f ? windDir.normalized : Vector3.forward;

            var rainObj = SpawnZoneClone(HarvestedTemplates.RainStorm, "AP_StormRain", position, false);
            var windObj = SpawnZoneClone(HarvestedTemplates.WindZone, "AP_StormWind", position, true);

            if (rainObj == null && windObj == null) yield break;

            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float remaining = Duration - elapsed;
                DriveZone(rainObj, windDir, remaining);
                DriveZone(windObj, windDir, remaining);

                var character = Character.localCharacter;
                if (character != null && !character.data.dead && !character.data.fullyPassedOut
                    && character.data.currentClimbHandle == null)
                {
                    float intensity = Mathf.Sin(Mathf.PI * elapsed / Duration);
                    character.AddForceAtPosition(windDir * GustForce * intensity, character.Center, ForceRadius);
                }

                yield return null;
            }

            StopZone(rainObj);
            StopZone(windObj);
            Shader.SetGlobalFloat("GlobalWind", 0f);
            if (Character.localCharacter != null)
            {
                Character.localCharacter.refs.climbing.climbingStamMinimumMultiplier = 1f;
            }

            yield return new WaitForSeconds(8f);

            if (rainObj != null) UnityEngine.Object.Destroy(rainObj);
            if (windObj != null) UnityEngine.Object.Destroy(windObj);
            WindChillZone.instance = previousInstance;
            Shader.SetGlobalFloat("_WeatherBlend", 0f);
            if (DayNightManager.instance != null)
            {
                DayNightManager.instance.rainstormWindFactor = 0f;
                DayNightManager.instance.snowstormWindFactor = 0f;
            }
            log.LogInfo("[PeakPelago] Storm Trap complete");
        }
    }
}
