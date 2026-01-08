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

                // Capture original weights
                OriginalLootWeights.CaptureOriginalWeights();

                // Add multiplayer only items (special items still go to special pools which are just luggage cursed and the statue)
                var multiplayerItems = new Dictionary<ushort, int>
                {
                    { 70, 15 },  // Blowgun
                };

                var multiplayerSpecialItems = new Dictionary<ushort, int>
                {
                    { 25, 11 },  // Cursed Skull
                    { 67, 11 },  // Scout Effigy
                    { 16, 11 },  // Bugle of Friendship
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