using System;
using System.Collections;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class FrogTrapEffect
    {
        public static void ApplyFrogTrap(ManualLogSource log)
        {
            try
            {
                var targetCharacter = TrapHelpers.GetRandomValidCharacter();
                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply frog trap - no valid characters found");
                    return;
                }

                string characterName = targetCharacter == Character.localCharacter
                    ? "local player"
                    : targetCharacter.characterName;

                log.LogInfo($"[PeakPelago] Spawning Frog near: {characterName}");

                if (PhotonNetwork.IsMasterClient)
                {
                    PeakArchipelagoPlugin._instance.StartCoroutine(SpawnFrog(targetCharacter.Center, characterName, log));
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying frog trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        private static IEnumerator SpawnFrog(Vector3 center, string characterName, ManualLogSource log)
        {
            Vector3 spawnPosition = FindSpawnPosition(center);

            Vector3 toPlayer = center - spawnPosition;
            toPlayer.y = 0f;
            Quaternion rotation = toPlayer.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toPlayer.normalized)
                : Quaternion.identity;

            GameObject frog = PhotonNetwork.Instantiate("0_Items/Frog", spawnPosition, rotation, 0);
            if (frog == null)
            {
                log.LogError("[PeakPelago] Failed to instantiate frog");
                yield break;
            }

            yield return null;

            var frogComponent = frog.GetComponent<FrogTongue>();
            if (frogComponent != null)
            {
                frogComponent.sleeping = false;
                frogComponent.UpdateSleeping();
            }

            log.LogInfo($"[PeakPelago] Frog Trap complete! Spawned frog at {spawnPosition} near {characterName}");
        }

        private static Vector3 FindSpawnPosition(Vector3 center)
        {
            float distance = 12f;
            float startAngle = UnityEngine.Random.Range(0f, 360f);

            for (int i = 0; i < 6; i++)
            {
                float radians = (startAngle + i * 60f) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(radians) * distance, 0f, Mathf.Sin(radians) * distance);
                Vector3 castStart = center + offset + Vector3.up * 8f;

                RaycastHit hit = HelperFunctions.LineCheck(castStart, castStart + Vector3.down * 20f, HelperFunctions.LayerType.TerrainMap);
                if (hit.transform != null && !Physics.Linecast(hit.point + Vector3.up * 1f, center, HelperFunctions.terrainMapMask))
                {
                    return hit.point + Vector3.up * 1f;
                }
            }

            return center + Vector3.up * 2f;
        }
    }
}
