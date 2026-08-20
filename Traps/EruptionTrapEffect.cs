using System;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

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
            if (prefab != null && _cachedEruptionPrefab == null)
            {
                _cachedEruptionPrefab = prefab;
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

                // Send RPC to ALL clients
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

        private static GameObject ResolveEruptionPrefab(ManualLogSource log)
        {
            if (_cachedEruptionPrefab != null) return _cachedEruptionPrefab;

            var loaded = Resources.Load<GameObject>("Eruption");
            if (loaded != null)
            {
                _cachedEruptionPrefab = loaded;
                return _cachedEruptionPrefab;
            }

            var activeSpawner = UnityEngine.Object.FindFirstObjectByType<EruptionSpawner>();
            if (activeSpawner != null && activeSpawner.eruption != null)
            {
                _cachedEruptionPrefab = activeSpawner.eruption;
                return _cachedEruptionPrefab;
            }

            return null;
        }

        public static void SpawnEruptionLocal(Vector3 position, ManualLogSource log)
        {
            try
            {

                GameObject eruptionPrefab = ResolveEruptionPrefab(log);
                if (eruptionPrefab == null)
                {
                    log.LogError("[PeakPelago] Resources.Load(\"Eruption\") returned null - eruption asset missing from game data");
                    return;
                }

                UnityEngine.Object.Instantiate(eruptionPrefab, position, Quaternion.LookRotation(Vector3.up));
                
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