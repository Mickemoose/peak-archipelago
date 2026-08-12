using System;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class EruptionTrapEffect
    {
        private static ManualLogSource _log;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
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

        public static void SpawnEruptionLocal(Vector3 position, ManualLogSource log)
        {
            try
            {

                // Find an EruptionSpawner in the scene to get the eruption prefab
                EruptionSpawner spawner = UnityEngine.Object.FindFirstObjectByType<EruptionSpawner>();
                
                if (spawner == null)
                {
                    log.LogError("[PeakPelago] Could not find EruptionSpawner in scene - might not be in Caldera");
                    return;
                }

                // Access the eruption field via reflection
                var eruptionField = typeof(EruptionSpawner).GetField("eruption", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (eruptionField == null)
                {
                    log.LogError("[PeakPelago] Could not find eruption field");
                    return;
                }

                GameObject eruptionPrefab = eruptionField.GetValue(spawner) as GameObject;
                
                if (eruptionPrefab == null)
                {
                    log.LogError("[PeakPelago] Eruption prefab is null");
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