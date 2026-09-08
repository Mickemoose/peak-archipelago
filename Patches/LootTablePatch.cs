using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Peak.AP;
using Photon.Pun;
using UnityEngine;

namespace PeakArchipelago
{
    [HarmonyPatch(typeof(LootData), "PopulateLootData")]
    public static class LootTablePatch
    {
        private static ManualLogSource _log => PeakArchipelagoPlugin._instance?._log;
        private static readonly List<KeyValuePair<SpawnPool, ushort>> _injectedOptionalEntries = new List<KeyValuePair<SpawnPool, ushort>>();

        public static int RemoveInjectedOptionalEntries()
        {
            int removed = 0;
            if (LootData.AllSpawnWeightData != null)
            {
                foreach (var entry in _injectedOptionalEntries)
                {
                    if (LootData.AllSpawnWeightData.TryGetValue(entry.Key, out var pool) && pool.Remove(entry.Value))
                    {
                        removed++;
                    }
                }
            }
            _injectedOptionalEntries.Clear();
            return removed;
        }

        private static void Inject(SpawnPool pool, ushort itemId, int weight)
        {
            if (!LootData.AllSpawnWeightData.ContainsKey(pool)) return;
            var entries = LootData.AllSpawnWeightData[pool];
            if (entries.ContainsKey(itemId) && entries[itemId] != 0) return;
            entries[itemId] = weight;
            if (UnlockedItemsManager.OptionalMultiplayerOnlyItemIds.Contains(itemId))
            {
                _injectedOptionalEntries.Add(new KeyValuePair<SpawnPool, ushort>(pool, itemId));
            }
        }

        static void Postfix()
        {
            try
            {
                _injectedOptionalEntries.Clear();
                if (LootData.AllSpawnWeightData == null)
                {
                    _log?.LogWarning("[PeakPelago] LootData.AllSpawnWeightData is null");
                    return;
                }

                bool includeOptional = UnlockedItemsManager.MultiplayerOnlyItemsEnabled;

                // Add multiplayer only items BEFORE capturing original weights
                // so they get proper original weights for unlock/lock cycling
                var multiplayerItems = new Dictionary<ushort, int>
                {
                    { 70, 1 },  // Blowgun
                    { 166, 1 },
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
                    SpawnPool.LuggageClimber, SpawnPool.LuggageAncient, SpawnPool.LuggageGloom,
                    SpawnPool.LuggageCitadel, SpawnPool.LuggageClown
                };

                foreach (var item in multiplayerSpecialItems)
                {
                    if (!includeOptional && UnlockedItemsManager.OptionalMultiplayerOnlyItemIds.Contains(item.Key)) continue;
                    foreach (SpawnPool pool in specialPools)
                    {
                        Inject(pool, item.Key, item.Value);
                    }
                }

                foreach (var item in multiplayerItems)
                {
                    if (!includeOptional && UnlockedItemsManager.OptionalMultiplayerOnlyItemIds.Contains(item.Key)) continue;
                    foreach (SpawnPool pool in luggagePools)
                    {
                        Inject(pool, item.Key, item.Value);
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
        static bool Prefix(Item __instance, ref bool __result)
        {
            // AP manages the loot tables directly - skip the banInSolo check
            if (PeakArchipelagoPlugin._instance?.Session == null) return true;

            var lootData = __instance.GetComponent<LootData>();

            if (!UnlockedItemsManager.MultiplayerOnlyItemsEnabled && lootData != null && lootData.banInSolo &&
                UnlockedItemsManager.OptionalMultiplayerOnlyItemIds.Contains(__instance.itemID) &&
                (PhotonNetwork.OfflineMode || (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount <= 1)))
            {
                __result = false;
                return false;
            }

            string validityName = (lootData != null && lootData.useOtherItemForSpawningValidity != null)
                ? lootData.useOtherItemForSpawningValidity.gameObject.name
                : __instance.gameObject.name;

            if (!RunSettings.IsItemEnabled(validityName))
            {
                __result = false;
                return false;
            }

            if (lootData != null && lootData.excludeBasedOnCustomRunSetting && RunSettings.IsCustomRun &&
                RunSettings.GetValue(lootData.excludeIfSettingDisabled) == 0)
            {
                __result = false;
                return false;
            }

            __result = true;
            return false;
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