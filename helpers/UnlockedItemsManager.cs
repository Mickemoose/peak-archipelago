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
        private static bool _needsRefresh = false;

        public static void Initialize(ManualLogSource log, ArchipelagoSession session)
        {
            _log = log;
            _log?.LogInfo($"[PeakPelago] Initialize - lootData:{LootData.AllSpawnWeightData != null}, captured:{OriginalLootWeights.HasCaptured}");
            _session = session;
            _log?.LogInfo("[PeakPelago] UnlockedItemsManager initialized");
            
            // Session is now available - if loot data is ready, refresh immediately
            if (LootData.AllSpawnWeightData != null && OriginalLootWeights.HasCaptured)
            {
                _log?.LogInfo("[PeakPelago] Loot data already ready, refreshing now");
                RefreshLootTables();
            }
            else
            {
                _needsRefresh = true;
            }
        }

        /// <summary>
        /// Request a loot table refresh. If LootData isn't ready yet, it will be deferred.
        /// </summary>
        public static void RequestRefresh()
        {
            if (LootData.AllSpawnWeightData == null)
            {
                _log?.LogInfo("[PeakPelago] Loot data not ready yet - deferring refresh");
                _needsRefresh = true;
                return;
            }
            
            RefreshLootTables();
        }

        /// <summary>
        /// Check if a deferred refresh is needed and execute it if LootData is now ready.
        /// Should be called from LootTablePatch.Postfix after LootData is populated.
        /// </summary>
        public static void CheckDeferredRefresh()
        {
            if (_needsRefresh && LootData.AllSpawnWeightData != null)
            {
                _log?.LogInfo("[PeakPelago] Executing deferred loot table refresh");
                _needsRefresh = false;
                RefreshLootTables();
            }
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
                _log?.LogInfo($"[PeakPelago] RefreshLootTables called - session:{_session != null}, lootData:{LootData.AllSpawnWeightData != null}, captured:{OriginalLootWeights.HasCaptured}");
                if (_session == null)
                {
                    _log?.LogWarning("[PeakPelago] RefreshLootTables: Session is null, skipping");
                    return;
                }
                
                if (LootData.AllSpawnWeightData == null)
                {
                    _log?.LogWarning("[PeakPelago] RefreshLootTables: AllSpawnWeightData is null - will retry when ready");
                    _needsRefresh = true;
                    return;
                }
                
                if (!OriginalLootWeights.HasCaptured)
                {
                    _log?.LogWarning("[PeakPelago] RefreshLootTables: Original weights not captured yet");
                    return;
                }

                HashSet<ushort> trackableItems = ItemIdMappings.GetAllApTrackedItemIds();
                HashSet<ushort> unlockedItems = GetUnlockedItemIds();
                
                _log?.LogInfo($"[PeakPelago] RefreshLootTables: {trackableItems.Count} trackable, {unlockedItems.Count} unlocked");
                _log?.LogInfo($"[PeakPelago] Unlocked item IDs: {string.Join(", ", unlockedItems.OrderBy(x => x))}");

                int removedCount = 0;
                int restoredCount = 0;

                foreach (var pool in LootData.AllSpawnWeightData.Keys.ToList())
                {
                    foreach (ushort itemId in trackableItems)
                    {
                        if (unlockedItems.Contains(itemId))
                        {
                            // Add unlocked item to pool with equal weight regardless of original presence
                            const int equalWeight = 1;
                            if (!LootData.AllSpawnWeightData[pool].ContainsKey(itemId))
                            {
                                LootData.AllSpawnWeightData[pool].Add(itemId, equalWeight);
                                restoredCount++;
                                _log?.LogDebug($"[PeakPelago] Added unlocked item {itemId} to pool {pool} (weight: {equalWeight})");
                            }
                            else if (LootData.AllSpawnWeightData[pool][itemId] != equalWeight)
                            {
                                LootData.AllSpawnWeightData[pool][itemId] = equalWeight;
                                restoredCount++;
                                _log?.LogDebug($"[PeakPelago] Updated unlocked item {itemId} in pool {pool} (weight: {equalWeight})");
                            }
                        }
                        else
                        {
                            if (LootData.AllSpawnWeightData[pool].ContainsKey(itemId))
                            {
                                LootData.AllSpawnWeightData[pool].Remove(itemId);
                                removedCount++;
                                _log?.LogDebug($"[PeakPelago] Removed locked item {itemId} from pool {pool}");
                            }
                        }
                    }
                }

                _log?.LogInfo($"[PeakPelago] ✓ Refreshed loot tables: removed {removedCount}, restored {restoredCount}");
                _needsRefresh = false;
                foreach (var pool in LootData.AllSpawnWeightData)
                {
                    var nonZeroItems = pool.Value.Where(kvp => kvp.Value > 0).ToList();
                    _log?.LogInfo($"[PeakPelago] Pool {pool.Key}: {nonZeroItems.Count} items with weight > 0");
                    foreach (var item in nonZeroItems.Take(10)) // First 10
                    {
                        _log?.LogInfo($"[PeakPelago]   ID {item.Key}: weight {item.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[PeakPelago] Error refreshing loot tables: {ex.Message}");
                _log?.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }

    public static class OriginalLootWeights
    {
        private static Dictionary<SpawnPool, Dictionary<ushort, int>> _originalWeights =
            [];
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