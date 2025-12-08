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

                // Capture original weights before any modifications
                OriginalLootWeights.CaptureOriginalWeights();

                // Add multiplayer-focused items
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

                SpawnPool[] specialPools = { SpawnPool.RespawnCoffin, SpawnPool.LuggageCursed };
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

                // Zero out tracked items until unlocked via AP
                UnlockedItemsManager.RefreshLootTables();
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
        static bool Prefix(SpawnPool spawnPool, int count, ref List<UnityEngine.GameObject> __result)
        {
            if (LootData.AllSpawnWeightData == null ||
                !LootData.AllSpawnWeightData.ContainsKey(spawnPool) ||
                LootData.AllSpawnWeightData[spawnPool].Count == 0)
            {
                __result = new List<UnityEngine.GameObject>();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(LootData), "GetRandomItem")]
    public static class GetRandomItemPatch
    {
        static bool Prefix(SpawnPool spawnPool, ref UnityEngine.GameObject __result)
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
}