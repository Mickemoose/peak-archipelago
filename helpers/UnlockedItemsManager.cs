using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Archipelago.MultiClient.Net;

namespace Peak.AP
{
    public static class UnlockedItemsManager
    {
        private static ManualLogSource _log;
        private static ArchipelagoSession _session;

        public static void Initialize(ManualLogSource log, ArchipelagoSession session)
        {
            _log = log;
            _session = session;
        }

        public static bool IsItemUnlocked(ushort itemId)
        {
            if (_session == null) return true; // No session = allow all

            // Find AP name for this item ID
            string apItemName = null;
            foreach (var kvp in ItemIdMappings.ApNameToInternalName)
            {
                if (ItemIdMappings.TryGetIdFromApName(kvp.Key, out ushort id) && id == itemId)
                {
                    apItemName = kvp.Key;
                    break;
                }
            }

            if (apItemName == null) return true; // Not tracked by AP

            // Check if received from AP
            return _session.Items.AllItemsReceived.Any(item =>
            {
                string receivedName = _session.Items.GetItemName(item.ItemId, item.ItemGame);
                return receivedName != null && receivedName.Equals(apItemName, StringComparison.OrdinalIgnoreCase);
            });
        }

        public static HashSet<ushort> GetUnlockedItemIds()
        {
            var unlocked = new HashSet<ushort>();
            if (_session == null) return unlocked;

            foreach (var item in _session.Items.AllItemsReceived)
            {
                string itemName = _session.Items.GetItemName(item.ItemId, item.ItemGame);
                if (itemName != null && ItemIdMappings.TryGetIdFromApName(itemName, out ushort gameItemId))
                {
                    unlocked.Add(gameItemId);
                }
            }

            return unlocked;
        }

        public static void RefreshLootTables()
        {
            try
            {
                
                if (LootData.AllSpawnWeightData == null)
                {
                    _log?.LogWarning("[PeakPelago] RefreshLootTables: AllSpawnWeightData is null");
                    return;
                }
                
                if (!OriginalLootWeights.HasCaptured)
                {
                    _log?.LogWarning("[PeakPelago] RefreshLootTables: Original weights not captured yet");
                    return;
                }

                HashSet<ushort> trackableItems = ItemIdMappings.GetAllApTrackedItemIds();
                _log?.LogInfo($"[PeakPelago] ID 0 tracked: {trackableItems.Contains(0)}");
                HashSet<ushort> unlockedItems = GetUnlockedItemIds();
                
                _log?.LogInfo($"[PeakPelago] RefreshLootTables: {trackableItems.Count} trackable, {unlockedItems.Count} unlocked");

                int removedCount = 0;
                int restoredCount = 0;

                foreach (var pool in LootData.AllSpawnWeightData.Keys.ToList())
                {
                    foreach (ushort itemId in trackableItems)
                    {
                        if (unlockedItems.Contains(itemId))
                        {
                            // Restore the item with original weight
                            int originalWeight = OriginalLootWeights.GetOriginalWeight(pool, itemId);
                            if (originalWeight > 0)
                            {
                                if (!LootData.AllSpawnWeightData[pool].ContainsKey(itemId))
                                {
                                    LootData.AllSpawnWeightData[pool].Add(itemId, originalWeight);
                                }
                                else
                                {
                                    LootData.AllSpawnWeightData[pool][itemId] = originalWeight;
                                }
                                restoredCount++;
                            }
                        }
                        else
                        {
                            // REMOVE the item entirely instead of setting to 0
                            if (LootData.AllSpawnWeightData[pool].ContainsKey(itemId))
                            {
                                LootData.AllSpawnWeightData[pool].Remove(itemId);
                                removedCount++;
                            }
                        }
                    }
                }

                _log?.LogInfo($"[PeakPelago] Refreshed loot tables: removed {removedCount}, restored {restoredCount}");
            }
            catch (Exception ex)
            {
                _log?.LogError($"[PeakPelago] Error refreshing loot tables: {ex.Message}");
            }
        }
    }

    public static class OriginalLootWeights
    {
        private static Dictionary<SpawnPool, Dictionary<ushort, int>> _originalWeights =
            new Dictionary<SpawnPool, Dictionary<ushort, int>>();
        private static bool _captured = false;

        public static void CaptureOriginalWeights()
        {
            if (_captured || LootData.AllSpawnWeightData == null) return;

            foreach (var pool in LootData.AllSpawnWeightData)
            {
                _originalWeights[pool.Key] = new Dictionary<ushort, int>(pool.Value);
            }

            _captured = true;
            PeakArchipelagoPlugin._instance?._log?.LogInfo("[PeakPelago] Captured original loot weights");
        }

        public static int GetOriginalWeight(SpawnPool pool, ushort itemId)
        {
            if (_originalWeights.TryGetValue(pool, out var weights))
            {
                if (weights.TryGetValue(itemId, out int weight))
                {
                    return weight;
                }
            }
            return 0;
        }

        public static bool HasCaptured => _captured;
    }
}