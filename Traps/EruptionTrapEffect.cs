using System;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class EruptionTrapEffect
    {
        private static ManualLogSource _log;
        private static GameObject _cachedEruptionPrefab;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
        }

        public static bool HasCachedPrefab => _cachedEruptionPrefab != null;

        public static void SetCachedPrefab(GameObject prefab)
        {
            if (prefab == null || _cachedEruptionPrefab != null) return;
            _cachedEruptionPrefab = HarvestedTemplates.Keep(prefab, "Eruption", _log);
            if (_cachedEruptionPrefab != null)
            {
                _log?.LogInfo("[PeakPelago] Eruption prefab cached");
            }
        }

        public static void ApplyEruptionTrap(ManualLogSource log)
        {
            try
            {
                var randomPlayer = TrapHelpers.GetRandomValidCharacter();
                if (randomPlayer == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Eruption Trap - no valid characters found");
                    return;
                }

                Vector3 targetPosition = randomPlayer.Center;

                string characterName = randomPlayer == Character.localCharacter
                    ? "local player"
                    : randomPlayer.characterName;

                log.LogInfo($"[PeakPelago] Spawning eruption at {characterName}'s location: {targetPosition}");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "SpawnEruptionTrapRPC",
                        RpcTarget.All,
                        targetPosition
                    );
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Eruption Trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        private static GameObject ResolveEruptionPrefab()
        {
            if (_cachedEruptionPrefab != null) return _cachedEruptionPrefab;

            foreach (var spawner in Resources.FindObjectsOfTypeAll<EruptionSpawner>())
            {
                if (spawner == null || spawner.eruption == null) continue;
                SetCachedPrefab(spawner.eruption);
                if (_cachedEruptionPrefab != null) return _cachedEruptionPrefab;
            }

            var loaded = Resources.Load<GameObject>("Eruption");
            if (loaded != null)
            {
                SetCachedPrefab(loaded);
            }

            return _cachedEruptionPrefab;
        }

        public static void SpawnEruptionLocal(Vector3 position, ManualLogSource log)
        {
            try
            {
                GameObject eruptionPrefab = ResolveEruptionPrefab();
                if (eruptionPrefab == null)
                {
                    log.LogError("[PeakPelago] No eruption template available - none was harvested from WilIsland or captured from an EruptionSpawner");
                    return;
                }

                var eruption = UnityEngine.Object.Instantiate(eruptionPrefab, position, Quaternion.LookRotation(Vector3.up));
                eruption.name = "AP_Eruption";
                eruption.SetActive(true);

                log.LogInfo($"[PeakPelago] Eruption spawned at position {position}!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error spawning eruption: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}
