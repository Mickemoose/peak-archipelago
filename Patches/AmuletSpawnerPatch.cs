using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class AmuletSpawnerGatePatch
    {
        public static ManualLogSource Log;
        private static readonly List<MonoBehaviour> _blockedSpawners = new List<MonoBehaviour>();

        public static bool IsLockedAmuletPrefab(GameObject prefab, string spawnerName)
        {
            if (prefab == null) return false;

            ushort itemId;
            if (!ItemIdMappings.NameToId.TryGetValue(prefab.name, out itemId))
            {
                var item = prefab.GetComponentInChildren<Item>(true);
                if (item != null) itemId = item.itemID;
            }

            if (prefab.name.StartsWith("Amulet") || prefab.name.Contains("Gem"))
            {
                Log?.LogInfo($"[PeakPelago] Amulet spawner check: spawner='{spawnerName}', prefab='{prefab.name}', resolvedId={itemId}, canSpawn={UnlockedItemsManager.CanSpawnScoutAmulet(itemId)}");
            }

            if (itemId == 0) return false;

            return !UnlockedItemsManager.CanSpawnScoutAmulet(itemId);
        }

        public static void BlockSpawner(MonoBehaviour spawner)
        {
            if (!_blockedSpawners.Contains(spawner))
            {
                _blockedSpawners.Add(spawner);
            }
            Log?.LogInfo($"[PeakPelago] Blocked amulet spawner '{spawner.name}' - unlock not yet received");
        }

        private static bool StillLocked(MonoBehaviour spawner)
        {
            if (spawner is Spawner s)
            {
                return s.spawnMode == Spawner.SpawnMode.SingleItem && IsLockedAmuletPrefab(s.spawnedObjectPrefab, s.name);
            }
            if (spawner is SingleItemSpawner sis)
            {
                return IsLockedAmuletPrefab(sis.prefab, sis.name);
            }
            return false;
        }

        public static void RetriggerBlockedSpawners()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            for (int i = _blockedSpawners.Count - 1; i >= 0; i--)
            {
                var spawner = _blockedSpawners[i];
                if (spawner == null)
                {
                    _blockedSpawners.RemoveAt(i);
                    continue;
                }
                if (StillLocked(spawner)) continue;

                _blockedSpawners.RemoveAt(i);
                Log?.LogInfo($"[PeakPelago] Amulet unlock received - spawning '{spawner.name}'");
                if (spawner is Spawner s) s.TrySpawnItems();
                else if (spawner is SingleItemSpawner sis) sis.TrySpawnItems();
            }
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.TrySpawnItems))]
    public static class AmuletPoolSpawnerGatePatch
    {
        static bool Prefix(Spawner __instance, ref List<PhotonView> __result)
        {
            if (__instance == null || __instance.spawnMode != Spawner.SpawnMode.SingleItem) return true;
            if (!AmuletSpawnerGatePatch.IsLockedAmuletPrefab(__instance.spawnedObjectPrefab, __instance.name)) return true;

            __result = new List<PhotonView>();
            AmuletSpawnerGatePatch.BlockSpawner(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(SingleItemSpawner), nameof(SingleItemSpawner.TrySpawnItems))]
    public static class AmuletSingleItemSpawnerGatePatch
    {
        static bool Prefix(SingleItemSpawner __instance, ref List<PhotonView> __result)
        {
            if (__instance == null) return true;
            if (!AmuletSpawnerGatePatch.IsLockedAmuletPrefab(__instance.prefab, __instance.name)) return true;

            __result = new List<PhotonView>();
            AmuletSpawnerGatePatch.BlockSpawner(__instance);
            return false;
        }
    }
}
