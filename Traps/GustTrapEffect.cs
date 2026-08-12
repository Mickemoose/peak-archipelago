using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class GustTrapEffect
    {
        private static readonly float Duration = 10f;
        private static readonly float GustForce = 20f;
        private static readonly float ForceRadius = 2f;

        private static readonly FieldInfo UntilSwitchField = typeof(WindChillZone).GetField("untilSwitch",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TimeUntilNextWindField = typeof(WindChillZone).GetField("timeUntilNextWind",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public static void ApplyGustTrap(ManualLogSource log)
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Gust Trap - no local character");
                    return;
                }

                log.LogInfo("[PeakPelago] Applying Gust Trap! Wind will blow for 10 seconds");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("StartGustTrapRPC", RpcTarget.All);
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    ActivateGustLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Gust Trap: {ex.Message}");
            }
        }

        public static void ActivateGustLocal(ManualLogSource log)
        {
            PeakArchipelagoPlugin._instance.StartCoroutine(GustCoroutine(log));
        }

        private static IEnumerator GustCoroutine(ManualLogSource log)
        {
            var zone = WindChillZone.instance;
            if (zone == null)
            {
                log.LogWarning("[PeakPelago] WindChillZone.instance is null, applying force-only gust");
                yield return PeakArchipelagoPlugin._instance.StartCoroutine(ForceOnlyCoroutine(log));
                yield break;
            }

            bool origWindActive = zone.windActive;
            Vector3 origWindDirection = zone.currentWindDirection;
            Bounds origBounds = zone.windZoneBounds;
            float origUntilSwitch = GetUntilSwitch(zone);
            float origTimeUntilNextWind = GetTimeUntilNextWind(zone);

            Vector3 windDir = RandomWindDirection();
            zone.windActive = true;
            zone.currentWindDirection = windDir;
            zone.windZoneBounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            SetUntilSwitch(zone, Duration + 5f);
            SetTimeUntilNextWind(zone, Duration + 5f);

            log.LogInfo($"[PeakPelago] Gust Trap hijacking WindChillZone, direction: {windDir}");

            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;

                zone.windActive = true;
                SetUntilSwitch(zone, Duration - elapsed + 1f);
                zone.windZoneBounds = new Bounds(Vector3.zero, Vector3.one * 100000f);

                var character = Character.localCharacter;
                if (character != null && !character.data.dead && !character.data.fullyPassedOut
                    && character.data.currentClimbHandle == null)
                {
                    float intensity = Mathf.Sin(Mathf.PI * elapsed / Duration);
                    character.AddForceAtPosition(
                        windDir * GustForce * intensity,
                        character.Center,
                        ForceRadius);
                }

                yield return null;
            }

            zone.windActive = false;
            zone.windZoneBounds = origBounds;
            zone.hasBeenActiveFor = 0f;
            Shader.SetGlobalFloat("GlobalWind", 0f);

            if (Character.localCharacter != null)
                Character.localCharacter.refs.climbing.climbingStamMinimumMultiplier = 1f;

            yield return new WaitForSeconds(3f);

            zone.windActive = origWindActive;
            zone.currentWindDirection = origWindDirection;
            SetUntilSwitch(zone, origUntilSwitch);
            SetTimeUntilNextWind(zone, origTimeUntilNextWind);

            log.LogInfo("[PeakPelago] Gust Trap complete, WindChillZone restored");
        }

        private static IEnumerator ForceOnlyCoroutine(ManualLogSource log)
        {
            Vector3 windDir = RandomWindDirection();
            float elapsed = 0f;

            while (elapsed < Duration)
            {
                elapsed += Time.fixedDeltaTime;

                var character = Character.localCharacter;
                if (character != null && !character.data.dead && !character.data.fullyPassedOut
                    && character.data.currentClimbHandle == null)
                {
                    float intensity = Mathf.Sin(Mathf.PI * elapsed / Duration);
                    character.AddForceAtPosition(
                        windDir * GustForce * intensity,
                        character.Center,
                        ForceRadius);
                }

                yield return new WaitForFixedUpdate();
            }

            log.LogInfo("[PeakPelago] Gust Trap (force-only) complete");
        }

        private static float GetUntilSwitch(WindChillZone zone)
        {
            return (float)UntilSwitchField.GetValue(zone);
        }

        private static void SetUntilSwitch(WindChillZone zone, float value)
        {
            UntilSwitchField.SetValue(zone, value);
        }

        private static float GetTimeUntilNextWind(WindChillZone zone)
        {
            return (float)TimeUntilNextWindField.GetValue(zone);
        }

        private static void SetTimeUntilNextWind(WindChillZone zone, float value)
        {
            TimeUntilNextWindField.SetValue(zone, value);
        }

        private static Vector3 RandomWindDirection()
        {
            return Vector3.Lerp(
                Vector3.right * (UnityEngine.Random.value > 0.5f ? 1f : -1f),
                Vector3.forward,
                0.2f).normalized;
        }
    }
}
