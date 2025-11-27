using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using Peak.AP;

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

                // Define multiplayer-focused items to add to loot pools
                // Format: (itemId, weight)
                var multiplayerItems = new Dictionary<ushort, int>
                {
                    { 70, 15 },  // Blowgun (HealingDart Variant)
                };

                var multiplayerSpecialItems = new Dictionary<ushort, int>
                {
                    { 25, 11 },  // Cursed Skull
                    { 67, 11 },  // Scout Effigy
                    { 16, 11 },  // Bugle of Friendship (Bugle_Magic)
                };

                SpawnPool[] specialPools =
                [
                    SpawnPool.RespawnCoffin,
                    SpawnPool.LuggageCursed,
                ];

                // All luggage spawn pools
                SpawnPool[] luggagePools =
                [
                    SpawnPool.LuggageBeach,
                    SpawnPool.LuggageJungle,
                    SpawnPool.LuggageTundra,
                    SpawnPool.LuggageMesa,
                    SpawnPool.LuggageCaldera,
                    SpawnPool.LuggageRoots,
                    SpawnPool.LuggageClimber,
                ];

                foreach (var item in multiplayerSpecialItems)
                {
                    ushort itemId = item.Key;
                    int weight = item.Value;

                    foreach (SpawnPool pool in specialPools)
                    {
                        if (LootData.AllSpawnWeightData.ContainsKey(pool))
                        {
                            if (!LootData.AllSpawnWeightData[pool].ContainsKey(itemId) || 
                                LootData.AllSpawnWeightData[pool][itemId] == 0)
                            {
                                LootData.AllSpawnWeightData[pool][itemId] = weight;
                            }
                        }
                    }
                }

                foreach (var item in multiplayerItems)
                {
                    ushort itemId = item.Key;
                    int weight = item.Value;

                    foreach (SpawnPool pool in luggagePools)
                    {
                        if (LootData.AllSpawnWeightData.ContainsKey(pool))
                        {
                            if (!LootData.AllSpawnWeightData[pool].ContainsKey(itemId) || 
                                LootData.AllSpawnWeightData[pool][itemId] == 0)
                            {
                                LootData.AllSpawnWeightData[pool][itemId] = weight;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[PeakPelago] Error modifying loot tables: {ex.Message}");
            }
        }
    }
}