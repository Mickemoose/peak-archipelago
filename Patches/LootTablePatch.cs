using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Peak.AP;
using UnityEngine;

namespace PeakArchipelago
{
    [HarmonyPatch(typeof(LootData), "PopulateLootData")]
    public static class LootTablePatch
    {
        private static ManualLogSource _log => PeakArchipelagoPlugin._instance?._log;

        static void Postfix()
        {
            try
            {
                if (LootData.AllSpawnWeightData == null)
                {
                    _log?.LogWarning("[PeakPelago] LootData.AllSpawnWeightData is null");
                    return;
                }

                // Add multiplayer only items BEFORE capturing original weights
                // so they get proper original weights for unlock/lock cycling
                var multiplayerItems = new Dictionary<ushort, int>
                {
                    { 70, 1 },  // Blowgun
                };

                var multiplayerSpecialItems = new Dictionary<ushort, int>
                {
                    { 25, 1 },  // Cursed Skull
                    { 67, 1 },  // Scout Effigy
                    { 16, 1 },  // Bugle of Friendship
                };

                SpawnPool[] specialPools = [SpawnPool.RespawnCoffin, SpawnPool.LuggageCursed];
                SpawnPool[] luggagePools =
                {
                    SpawnPool.LuggageBeach, SpawnPool.LuggageJungle, SpawnPool.LuggageTundra,
                    SpawnPool.LuggageMesa, SpawnPool.LuggageCaldera, SpawnPool.LuggageRoots,
                    SpawnPool.LuggageClimber
                };

                foreach (var item in multiplayerSpecialItems)
                {
                    foreach (SpawnPool pool in specialPools)
                    {
                        if (LootData.AllSpawnWeightData.ContainsKey(pool))
                        {
                            if (!LootData.AllSpawnWeightData[pool].ContainsKey(item.Key) ||
                                LootData.AllSpawnWeightData[pool][item.Key] == 0)
                            {
                                LootData.AllSpawnWeightData[pool][item.Key] = item.Value;
                            }
                        }
                    }
                }

                foreach (var item in multiplayerItems)
                {
                    foreach (SpawnPool pool in luggagePools)
                    {
                        if (LootData.AllSpawnWeightData.ContainsKey(pool))
                        {
                            if (!LootData.AllSpawnWeightData[pool].ContainsKey(item.Key) ||
                                LootData.AllSpawnWeightData[pool][item.Key] == 0)
                            {
                                LootData.AllSpawnWeightData[pool][item.Key] = item.Value;
                            }
                        }
                    }
                }

                // Capture original weights AFTER multiplayer items are added
                OriginalLootWeights.CaptureOriginalWeights();

                UnlockedItemsManager.CheckDeferredRefresh();
            }
            catch (Exception ex)
            {
                _log?.LogError($"[PeakPelago] Error modifying loot tables: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(LootData), "GetRandomItems")]
    public static class GetRandomItemsPatch
    {
        static bool Prefix(SpawnPool spawnPool, int count, ref List<GameObject> __result)
        {
            if (LootData.AllSpawnWeightData == null ||
                !LootData.AllSpawnWeightData.ContainsKey(spawnPool) ||
                LootData.AllSpawnWeightData[spawnPool].Count == 0)
            {
                __result = [];
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(LootData), "GetRandomItem")]
    public static class GetRandomItemPatch
    {
        static bool Prefix(SpawnPool spawnPool, ref GameObject __result)
        {
            if (LootData.AllSpawnWeightData == null ||
                !LootData.AllSpawnWeightData.ContainsKey(spawnPool) ||
                LootData.AllSpawnWeightData[spawnPool].Count == 0)
            {
                __result = null;
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(Item), "IsValidToSpawn")]
    public static class ItemIsValidToSpawnPatch
    {
        static bool Prefix(ref bool __result)
        {
            // AP manages the loot tables directly - skip the banInSolo check
            if (PeakArchipelagoPlugin._instance?.Session != null)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(LootData), nameof(LootData.GetRandomItem))]
    public static class LootDataGetRandomItemPatch
    {
        public static bool Prefix(SpawnPool spawnPool, ref GameObject __result)
        {
            if (LootData.AllSpawnWeightData == null)
            {
                LootData.PopulateLootData();
            }

            if (!LootData.AllSpawnWeightData.TryGetValue(spawnPool, out var pool))
            {
                __result = null;
                return false; 
            }

            // Check if pool has any items with weight > 0
            var validItems = pool.Where(kvp => kvp.Value > 0).ToList();
            if (validItems.Count == 0)
            {
                __result = null;
                return false; // Skip original - empty pool returns null
            }

            // Let original method handle it since pool has valid items
            return true;
        }
    }
}