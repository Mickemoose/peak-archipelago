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

            if (itemId == 0) return false;

            return !UnlockedItemsManager.CanSpawnGatedItem(itemId);
        }

        public static void BlockSpawner(MonoBehaviour spawner)
        {
            if (!_blockedSpawners.Contains(spawner))
            {
                _blockedSpawners.Add(spawner);
            }
            Log?.LogInfo($"[PeakPelago] Blocked amulet spawner '{spawner.name}' - unlock not yet received");
        }

        private const ushort FannyPackId = 166;
        private const ushort BackpackId = 6;

        private static bool StillLocked(MonoBehaviour spawner)
        {
            GameObject prefab;
            string spawnerName;
            if (spawner is Spawner s)
            {
                if (s.spawnMode != Spawner.SpawnMode.SingleItem) return false;
                prefab = s.spawnedObjectPrefab;
                spawnerName = s.name;
            }
            else if (spawner is SingleItemSpawner sis)
            {
                prefab = sis.prefab;
                spawnerName = sis.name;
            }
            else
            {
                return false;
            }

            ushort prefabId = ResolvePrefabItemId(prefab);
            if (prefabId == BackpackId)
            {
                return !UnlockedItemsManager.CanSpawnGatedItem(BackpackId) &&
                       !UnlockedItemsManager.CanSpawnGatedItem(FannyPackId);
            }
            if (IsStatueLootSpot(prefabId)) return false;
            return IsLockedAmuletPrefab(prefab, spawnerName);
        }

        public static bool TryHandleBackpackSpawner(MonoBehaviour spawner, GameObject prefab, bool kinematic, ref List<PhotonView> result)
        {
            if (spawner == null || prefab == null) return false;
            if (ResolvePrefabItemId(prefab) != BackpackId) return false;
            if (UnlockedItemsManager.CanSpawnGatedItem(BackpackId)) return false;

            result = new List<PhotonView>();
            if (UnlockedItemsManager.CanSpawnGatedItem(FannyPackId))
            {
                if (!PhotonNetwork.IsMasterClient) return true;
                ItemDatabase.TryGetItem(FannyPackId, out var fanny);
                if (fanny == null)
                {
                    Log?.LogWarning("[PeakPelago] Backpack spawner substitution failed - Fanny Pack not in item database");
                    return true;
                }
                var obj = PhotonNetwork.InstantiateItemRoom(fanny.gameObject.name,
                    spawner.transform.position + Vector3.up * 0.1f, spawner.transform.rotation);
                if (obj != null)
                {
                    var view = obj.GetComponent<PhotonView>();
                    if (view != null)
                    {
                        if (kinematic)
                        {
                            view.RPC("SetKinematicRPC", RpcTarget.AllBuffered, true, obj.transform.position, obj.transform.rotation);
                        }
                        result.Add(view);
                    }
                    Log?.LogInfo($"[PeakPelago] Backpack spawner '{spawner.name}' spawned a Fanny Pack instead (Progressive Pack 1/2)");
                }
                return true;
            }

            BlockSpawner(spawner);
            return true;
        }


        public static bool IsStatueLootSpot(ushort itemId)
        {
            if (itemId == 47) return UnlockedItemsManager.LootSanityActive;
            return UnlockedItemsManager.IsAmuletChainItem(itemId) && UnlockedItemsManager.ScoutAmuletsFreelyPlaced;
        }

        public static SpawnPool ResolveBiomePool(Component obj)
        {
            var biome = obj != null ? obj.GetComponentInParent<global::Biome>() : null;
            if (biome == null) return SpawnPool.LuggageBeach;
            return UnlockedItemsManager.GetLuggagePoolForBiome(biome.biomeType);
        }

        private static GameObject DrawLootExcludingChainItems(SpawnPool pool)
        {
            if (LootData.AllSpawnWeightData == null) return null;
            if (!LootData.AllSpawnWeightData.TryGetValue(pool, out var weights)) return null;

            int total = 0;
            foreach (var kvp in weights)
            {
                if (kvp.Value <= 0) continue;
                if (UnlockedItemsManager.IsAmuletChainItem(kvp.Key)) continue;
                total += kvp.Value;
            }
            if (total <= 0) return null;

            int roll = UnityEngine.Random.Range(0, total);
            foreach (var kvp in weights)
            {
                if (kvp.Value <= 0) continue;
                if (UnlockedItemsManager.IsAmuletChainItem(kvp.Key)) continue;
                roll -= kvp.Value;
                if (roll < 0)
                {
                    ItemDatabase.TryGetItem(kvp.Key, out var item);
                    return item != null ? item.gameObject : null;
                }
            }
            return null;
        }

        public static void SpawnLootAt(SpawnPool pool, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation, bool kinematic, List<PhotonView> result, string context)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            var lootPrefab = DrawLootExcludingChainItems(pool);
            if (lootPrefab == null)
            {
                Log?.LogInfo($"[PeakPelago] {context}: no eligible loot in pool {pool}, spawning nothing");
                return;
            }

            var obj = PhotonNetwork.InstantiateItemRoom(lootPrefab.name, position, rotation);
            if (obj != null)
            {
                var view = obj.GetComponent<PhotonView>();
                if (view != null)
                {
                    if (kinematic)
                    {
                        view.RPC("SetKinematicRPC", RpcTarget.AllBuffered, true, obj.transform.position, obj.transform.rotation);
                    }
                    result?.Add(view);
                }
                Log?.LogInfo($"[PeakPelago] {context} spawned loot item '{lootPrefab.name}'");
            }
        }

        private static ushort ResolvePrefabItemId(GameObject prefab)
        {
            if (prefab == null) return 0;
            if (ItemIdMappings.NameToId.TryGetValue(prefab.name, out ushort itemId)) return itemId;
            var item = prefab.GetComponentInChildren<Item>(true);
            return item != null ? item.itemID : (ushort)0;
        }

        public static bool TryReplaceWithLootSpawn(SingleItemSpawner spawner, ref List<PhotonView> result)
        {
            if (spawner == null || spawner.prefab == null) return false;
            ushort itemId = ResolvePrefabItemId(spawner.prefab);
            if (itemId == 0 || !IsStatueLootSpot(itemId)) return false;

            result = new List<PhotonView>();
            SpawnLootAt(ResolveBiomePool(spawner), spawner.transform.position + UnityEngine.Vector3.up * 0.1f,
                spawner.transform.rotation, spawner.isKinematic, result, spawner.name);
            return true;
        }

        public static bool TryReplaceWithLootSpawn(Spawner spawner, ref List<PhotonView> result)
        {
            if (spawner == null || spawner.spawnMode != Spawner.SpawnMode.SingleItem || spawner.spawnedObjectPrefab == null) return false;
            ushort itemId = ResolvePrefabItemId(spawner.spawnedObjectPrefab);
            if (itemId == 0 || !IsStatueLootSpot(itemId)) return false;

            result = new List<PhotonView>();
            SpawnLootAt(ResolveBiomePool(spawner), spawner.transform.position + UnityEngine.Vector3.up * 0.1f,
                spawner.transform.rotation, false, result, spawner.name);
            return true;
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
            if (AmuletSpawnerGatePatch.TryReplaceWithLootSpawn(__instance, ref __result)) return false;
            if (AmuletSpawnerGatePatch.TryHandleBackpackSpawner(__instance, __instance.spawnedObjectPrefab, false, ref __result)) return false;
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
            if (AmuletSpawnerGatePatch.TryReplaceWithLootSpawn(__instance, ref __result)) return false;
            if (AmuletSpawnerGatePatch.TryHandleBackpackSpawner(__instance, __instance.prefab, __instance.isKinematic, ref __result)) return false;
            if (!AmuletSpawnerGatePatch.IsLockedAmuletPrefab(__instance.prefab, __instance.name)) return true;

            __result = new List<PhotonView>();
            AmuletSpawnerGatePatch.BlockSpawner(__instance);
            return false;
        }
    }
}
