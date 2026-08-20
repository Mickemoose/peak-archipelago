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
        private static bool _itemSanityEnabled = false;
        private static int _lootSanityMode = 0; // 0=None, 1=Luggage, 2=Trees/Bushes, 3=All
        private static bool _logicalScoutStatue = false;
        private static bool _scoutAmuletSanity = false;
        private static int _progressiveMountainCount = 0;
        private static StateData _stateData;
        private static Dictionary<string, int> _lootBiomeAssignments = new Dictionary<string, int>();

        /// <summary>
        /// Item IDs for multiplayer-only items that should always be equalized
        /// even when ItemSanity is off, since they don't normally appear in solo.
        /// </summary>
        private static readonly HashSet<ushort> MultiplayerOnlyItemIds = new HashSet<ushort>
        {
            70,  // Blowgun (HealingDart Variant)
            25,  // Cursed Skull
            67,  // Scout Effigy
            16,  // Bugle of Friendship (Bugle_Magic)
            173, // Ritual Dagger
        };

        private const ushort SCOUTS_TENACITY_ITEM_ID = 182;
        private const ushort SCOUTS_GENEROSITY_ITEM_ID = 180;
        private const ushort SCOUTS_AMBITION_ITEM_ID = 179;
        private const ushort SCOUTS_INITIATIVE_ITEM_ID = 170;
        private const ushort SCOUTS_HONOR_ITEM_ID = 183;

        /// <summary>
        /// The four Scout amulets and Scout's Honor. Excluded from the randomized loot pools
        /// unless Scout Amulet Sanity is enabled.
        /// </summary>
        private static readonly HashSet<ushort> ScoutAmuletItemIds = new HashSet<ushort>
        {
            SCOUTS_TENACITY_ITEM_ID,
            SCOUTS_GENEROSITY_ITEM_ID,
            SCOUTS_AMBITION_ITEM_ID,
            SCOUTS_INITIATIVE_ITEM_ID,
            SCOUTS_HONOR_ITEM_ID,
        };

        private static readonly HashSet<SpawnPool> LuggagePools = new HashSet<SpawnPool>
        {
            SpawnPool.LuggageBeach,
            SpawnPool.LuggageJungle,
            SpawnPool.LuggageTundra,
            SpawnPool.LuggageMesa,
            SpawnPool.LuggageCaldera,
            SpawnPool.LuggageRoots,
            SpawnPool.LuggageClimber,
            SpawnPool.LuggageAncient,
            SpawnPool.LuggageCursed,
            SpawnPool.LuggageGloom,
            SpawnPool.LuggageCitadel,
            SpawnPool.LuggageClown,
        };

        private static readonly HashSet<SpawnPool> NaturalPools = new HashSet<SpawnPool>
        {
            SpawnPool.MushroomCluster,
            SpawnPool.BerryBushBeach,
            SpawnPool.BerryBushJungle,
            SpawnPool.SpikyVine,
            SpawnPool.CoconutTree,
            SpawnPool.WillowTreeJungle,
            SpawnPool.JungleVine,
            SpawnPool.WinterberryTree,
            SpawnPool.Nest,
            SpawnPool.Cactus,
            SpawnPool.Redwood,
        };

        /// <summary>
        /// Maps acquire location names to the biome Progressive Mountain count required.
        /// 0 = no biome requirement (beach/shore).
        /// </summary>
        private static readonly Dictionary<string, int> AcquireBiomeRequirements = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // Tropics/Roots = 1 Progressive Mountain
            { "Acquire Red Clusterberry", 1 },
            { "Acquire Yellow Clusterberry", 1 },
            { "Acquire Black Clusterberry", 1 },
            { "Acquire Purple Kingberry", 1 },
            { "Acquire Yellow Kingberry", 1 },
            { "Acquire Green Kingberry", 1 },
            { "Acquire Brown Berrynana", 1 },
            { "Acquire Blue Berrynana", 1 },
            { "Acquire Pink Berrynana", 1 },
            { "Acquire Yellow Berrynana", 1 },
            { "Acquire Yellow Berrynana Peel", 1 },
            { "Acquire Pink Berrynana Peel", 1 },
            { "Acquire Blue Berrynana Peel", 1 },
            { "Acquire Brown Berrynana Peel", 1 },
            { "Acquire Honeycomb", 1 },
            { "Acquire Beehive", 1 },
            { "Acquire Magic Bean", 1 },
            { "Acquire Tick", 1 },
            { "Acquire Red Shroomberry", 1 },
            { "Acquire Blue Shroomberry", 1 },
            { "Acquire Yellow Shroomberry", 1 },
            { "Acquire Green Shroomberry", 1 },
            { "Acquire Purple Shroomberry", 1 },
            { "Acquire Mandrake", 1 },
            { "Acquire Bounce Shroom", 1 },
            { "Acquire Cloud Fungus", 1 },
            // Mesa/Alpine = 2 Progressive Mountain
            { "Acquire Cactus", 2 },
            { "Acquire Aloe Vera", 2 },
            { "Acquire Sunscreen", 2 },
            { "Acquire Ancient Idol", 2 },
            { "Acquire Red Prickleberry", 2 },
            { "Acquire Gold Prickleberry", 2 },
            { "Acquire Scorpion", 2 },
            { "Acquire Torch", 2 },
            { "Acquire Parasol", 2 },
            { "Acquire Dynamite", 2 },
            { "Acquire Orange Winterberry", 2 },
            { "Acquire Napberry", 2 },
            { "Acquire Yellow Winterberry", 2 },
            { "Acquire Heat Pack", 2 },
            // Caldera = 3 Progressive Mountain
            { "Acquire Big Egg", 3 },
            { "Acquire Egg", 3 },
            { "Acquire Cooked Bird", 3 },
            // Kiln = 4 Progressive Mountain
            { "Acquire Strange Gem", 4 },
        };

        private static readonly Dictionary<int, HashSet<SpawnPool>> BiomeLevelToLuggagePools = new Dictionary<int, HashSet<SpawnPool>>
        {
            { 0, new HashSet<SpawnPool> { SpawnPool.LuggageBeach } },
            { 1, new HashSet<SpawnPool> { SpawnPool.LuggageJungle, SpawnPool.LuggageRoots } },
            { 2, new HashSet<SpawnPool> { SpawnPool.LuggageTundra, SpawnPool.LuggageMesa } },
            { 3, new HashSet<SpawnPool> { SpawnPool.LuggageCaldera } },
        };

        private static readonly Dictionary<int, HashSet<SpawnPool>> BiomeLevelToNaturalPools = new Dictionary<int, HashSet<SpawnPool>>
        {
            { 0, new HashSet<SpawnPool> { SpawnPool.BerryBushBeach, SpawnPool.CoconutTree, SpawnPool.MushroomCluster } },
            { 1, new HashSet<SpawnPool> { SpawnPool.BerryBushJungle, SpawnPool.WillowTreeJungle, SpawnPool.JungleVine, SpawnPool.SpikyVine, SpawnPool.MushroomCluster, SpawnPool.Redwood } },
            { 2, new HashSet<SpawnPool> { SpawnPool.WinterberryTree, SpawnPool.Cactus } },
            { 3, new HashSet<SpawnPool> { SpawnPool.Nest } },
        };

        private static readonly HashSet<SpawnPool> AnyBiomeLuggagePools = new HashSet<SpawnPool>
        {
            SpawnPool.LuggageClimber, SpawnPool.LuggageAncient, SpawnPool.LuggageCursed,
        };

        private static HashSet<SpawnPool> GetPoolsForBiomeLevel(int biomeLevel)
        {
            var pools = new HashSet<SpawnPool>();
            switch (_lootSanityMode)
            {
                case 1: // Luggage
                    if (BiomeLevelToLuggagePools.TryGetValue(biomeLevel, out var lugPools))
                        pools.UnionWith(lugPools);
                    pools.UnionWith(AnyBiomeLuggagePools);
                    break;
                case 2: // Trees/Bushes
                    if (BiomeLevelToNaturalPools.TryGetValue(biomeLevel, out var natPools))
                        pools.UnionWith(natPools);
                    break;
                case 3: // All
                    if (BiomeLevelToLuggagePools.TryGetValue(biomeLevel, out var allLug))
                        pools.UnionWith(allLug);
                    pools.UnionWith(AnyBiomeLuggagePools);
                    if (BiomeLevelToNaturalPools.TryGetValue(biomeLevel, out var allNat))
                        pools.UnionWith(allNat);
                    break;
            }
            return pools;
        }

        private static Dictionary<string, string> _acquireToApName;

        private static Dictionary<string, string> GetAcquireToApNameMap()
        {
            if (_acquireToApName != null) return _acquireToApName;
            _acquireToApName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in ItemIdMappings.ApNameToInternalName)
            {
                string apUnlock = kvp.Key;
                if (apUnlock.EndsWith(" Unlock"))
                {
                    string itemName = apUnlock.Substring(0, apUnlock.Length - " Unlock".Length);
                    string acquireName = "Acquire " + itemName;
                    _acquireToApName[acquireName] = kvp.Value;
                }
            }
            return _acquireToApName;
        }

        public static void Initialize(ManualLogSource log, ArchipelagoSession session,
            bool itemSanityEnabled = false, int lootSanityMode = 0,
            bool logicalScoutStatue = false, int progressiveMountainCount = 0,
            StateData stateData = null, Dictionary<string, int> lootBiomeAssignments = null,
            bool scoutAmuletSanity = false)
        {
            _log = log;
            _session = session;
            _itemSanityEnabled = itemSanityEnabled;
            _lootSanityMode = lootSanityMode;
            _logicalScoutStatue = logicalScoutStatue;
            _scoutAmuletSanity = scoutAmuletSanity;
            _progressiveMountainCount = progressiveMountainCount;
            _stateData = stateData;
            _lootBiomeAssignments = lootBiomeAssignments ?? new Dictionary<string, int>();
            _log?.LogInfo($"[PeakPelago] UnlockedItemsManager initialized (ItemSanity: {(_itemSanityEnabled ? "ON" : "OFF")}, LootSanity: {_lootSanityMode}, LogicalScoutStatue: {(_logicalScoutStatue ? "ON" : "OFF")}, ScoutAmuletSanity: {(_scoutAmuletSanity ? "ON" : "OFF")})");

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

        public static void UpdateMountainCount(int count)
        {
            _progressiveMountainCount = count;
            if (_logicalScoutStatue)
            {
                RefreshScoutStatuePool();
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
        /// Called from LootTablePatch.Postfix after LootData is populated.
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

        public static bool CanCreateScoutsHonor()
        {
            if (!_itemSanityEnabled || !_scoutAmuletSanity) return true;
            return IsItemUnlocked(SCOUTS_HONOR_ITEM_ID);
        }

        private const ushort STRANGE_GEM_ITEM_ID = 112;

        public static bool CanSpawnStrangeGem()
        {
            if (!_itemSanityEnabled) return true;
            return IsItemUnlocked(STRANGE_GEM_ITEM_ID);
        }

        public static string GetTrackerSpawnCategory(ushort itemId)
        {
            if (ScoutAmuletItemIds.Contains(itemId) || itemId == STRANGE_GEM_ITEM_ID) return "Statues";

            switch (_lootSanityMode)
            {
                case 1: return "Luggage";
                case 2: return "Bushes & Trees";
                case 3: return "Luggage & Foliage";
            }

            if (OriginalLootWeights.HasCaptured)
            {
                bool inLuggage = LuggagePools.Any(p => OriginalLootWeights.GetOriginalWeight(p, itemId) > 0);
                bool inNatural = NaturalPools.Any(p => OriginalLootWeights.GetOriginalWeight(p, itemId) > 0);
                if (inLuggage && inNatural) return "Luggage & Foliage";
                if (inLuggage) return "Luggage";
                if (inNatural) return "Bushes & Trees";
                if (OriginalLootWeights.GetOriginalWeight(SpawnPool.RespawnCoffin, itemId) > 0) return "Statues";
            }

            return "World";
        }

        public static bool IsAmuletChainItem(ushort itemId)
        {
            return ScoutAmuletItemIds.Contains(itemId) || itemId == STRANGE_GEM_ITEM_ID;
        }

        public static bool CanSpawnScoutAmulet(ushort itemId)
        {
            if (itemId == STRANGE_GEM_ITEM_ID) return CanSpawnStrangeGem();
            if (!ScoutAmuletItemIds.Contains(itemId)) return true;
            if (!_itemSanityEnabled || !_scoutAmuletSanity) return true;
            return IsItemUnlocked(itemId);
        }

        private const string ProgressiveAmuletUnlockName = "Progressive Amulet Unlock";

        private static readonly ushort[] AmuletChainOrder =
        {
            STRANGE_GEM_ITEM_ID,
            SCOUTS_TENACITY_ITEM_ID,
            SCOUTS_GENEROSITY_ITEM_ID,
            SCOUTS_AMBITION_ITEM_ID,
            SCOUTS_INITIATIVE_ITEM_ID,
            SCOUTS_HONOR_ITEM_ID,
        };

        private static int GetProgressiveAmuletCount()
        {
            if (_session == null) return 0;
            return _session.Items.AllItemsReceived.Count(item =>
            {
                string name = _session.Items.GetItemName(item.ItemId, item.ItemGame);
                return name != null && name.Equals(ProgressiveAmuletUnlockName, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static readonly Dictionary<ushort, ushort> VariantAliases = new Dictionary<ushort, ushort>
        {
            { 153, 154 },
            { 164, 98 },
        };

        public static bool IsItemUnlocked(ushort itemId)
        {
            if (_session == null) return true;

            if (VariantAliases.TryGetValue(itemId, out ushort canonicalId))
            {
                itemId = canonicalId;
            }

            int chainIndex = Array.IndexOf(AmuletChainOrder, itemId);
            if (chainIndex >= 0 && GetProgressiveAmuletCount() > chainIndex) return true;

            string apItemName = null;
            foreach (var kvp in ItemIdMappings.ApNameToInternalName)
            {
                if (ItemIdMappings.TryGetIdFromApName(kvp.Key, out ushort id) && id == itemId)
                {
                    apItemName = kvp.Key;
                    break;
                }
            }

            if (apItemName == null) return true;

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

            int progressiveAmulets = 0;
            foreach (var item in _session.Items.AllItemsReceived)
            {
                string itemName = _session.Items.GetItemName(item.ItemId, item.ItemGame);
                if (itemName == null) continue;
                if (itemName.Equals(ProgressiveAmuletUnlockName, StringComparison.OrdinalIgnoreCase))
                {
                    progressiveAmulets++;
                    continue;
                }
                if (ItemIdMappings.TryGetIdFromApName(itemName, out ushort gameItemId))
                {
                    unlocked.Add(gameItemId);
                }
            }

            for (int i = 0; i < progressiveAmulets && i < AmuletChainOrder.Length; i++)
            {
                unlocked.Add(AmuletChainOrder[i]);
            }

            foreach (var alias in VariantAliases)
            {
                if (unlocked.Contains(alias.Value))
                {
                    unlocked.Add(alias.Key);
                }
            }

            return unlocked;
        }

        /// <summary>
        /// Determine which pools should be affected by LootSanity based on the mode.
        /// </summary>
        private static HashSet<SpawnPool> GetLootSanityPools()
        {
            var pools = new HashSet<SpawnPool>();
            switch (_lootSanityMode)
            {
                case 1: // Luggage
                    pools.UnionWith(LuggagePools);
                    break;
                case 2: // Trees/Bushes
                    pools.UnionWith(NaturalPools);
                    break;
                case 3: // All
                    pools.UnionWith(LuggagePools);
                    pools.UnionWith(NaturalPools);
                    break;
                // case 0 (None): empty set
            }
            return pools;
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
                    _log?.LogWarning("[PeakPelago] RefreshLootTables: Original weights not captured yet - deferring");
                    _needsRefresh = true;
                    return;
                }

                HashSet<ushort> trackableItems = ItemIdMappings.GetAllApTrackedItemIds();
                HashSet<ushort> unlockedItems = GetUnlockedItemIds();
                HashSet<SpawnPool> lootSanityPools = GetLootSanityPools();

                int removedCount = 0;
                int restoredCount = 0;

                foreach (var pool in LootData.AllSpawnWeightData.Keys.ToList())
                {
                    // RespawnCoffin is owned by RefreshScoutStatuePool when LogicalScoutStatue is on, and left
                    // vanilla when ItemSanity is off. Otherwise fall through so ItemSanity still gates it.
                    if (pool == SpawnPool.RespawnCoffin && (_logicalScoutStatue || !_itemSanityEnabled)) continue;

                    bool isLootSanityPool = lootSanityPools.Contains(pool);

                    // Always process multiplayer-only items regardless of settings
                    foreach (ushort itemId in MultiplayerOnlyItemIds)
                    {
                        if (unlockedItems.Contains(itemId) || !_itemSanityEnabled)
                        {
                            const int equalWeight = 1;
                            if (!LootData.AllSpawnWeightData[pool].ContainsKey(itemId))
                            {
                                LootData.AllSpawnWeightData[pool].Add(itemId, equalWeight);
                                restoredCount++;
                            }
                            else if (LootData.AllSpawnWeightData[pool][itemId] != equalWeight)
                            {
                                LootData.AllSpawnWeightData[pool][itemId] = equalWeight;
                                restoredCount++;
                            }
                        }
                        else
                        {
                            if (LootData.AllSpawnWeightData[pool].ContainsKey(itemId))
                            {
                                LootData.AllSpawnWeightData[pool].Remove(itemId);
                                removedCount++;
                            }
                        }
                    }

                    foreach (ushort itemId in trackableItems)
                    {
                        if (MultiplayerOnlyItemIds.Contains(itemId)) continue; // Already handled
                        if (!_scoutAmuletSanity && ScoutAmuletItemIds.Contains(itemId)) continue;

                        bool allowed = !_itemSanityEnabled || unlockedItems.Contains(itemId);

                        if (isLootSanityPool)
                        {
                            // LootSanity: (re)distribute trackable items into this pool based on biome assignments
                            bool belongsInPool = false;
                            if (_lootBiomeAssignments.Count > 0)
                            {
                                // Find the acquire location for this item ID and check its assigned biome
                                var acquireMap = GetAcquireToApNameMap();
                                foreach (var kvp in acquireMap)
                                {
                                    if (ItemIdMappings.NameToId.TryGetValue(kvp.Value, out ushort mappedId) && mappedId == itemId)
                                    {
                                        if (_lootBiomeAssignments.TryGetValue(kvp.Key, out int biomeLevel))
                                        {
                                            var allowedPools = GetPoolsForBiomeLevel(biomeLevel);
                                            belongsInPool = allowedPools.Contains(pool);
                                        }
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                // No assignments from server - fall back to adding to all affected pools
                                belongsInPool = true;
                            }

                            if (allowed && belongsInPool)
                            {
                                const int equalWeight = 1;
                                if (!LootData.AllSpawnWeightData[pool].ContainsKey(itemId))
                                {
                                    LootData.AllSpawnWeightData[pool].Add(itemId, equalWeight);
                                    restoredCount++;
                                }
                                else if (LootData.AllSpawnWeightData[pool][itemId] != equalWeight)
                                {
                                    LootData.AllSpawnWeightData[pool][itemId] = equalWeight;
                                    restoredCount++;
                                }
                            }
                            else
                            {
                                // Item not allowed or doesn't belong in this pool - remove
                                if (LootData.AllSpawnWeightData[pool].ContainsKey(itemId))
                                {
                                    LootData.AllSpawnWeightData[pool].Remove(itemId);
                                    removedCount++;
                                }
                            }
                        }
                        else
                        {
                            // Normal pool: ItemSanity gates the item's ORIGINAL spawns, independent of LootSanity.
                            int originalWeight = OriginalLootWeights.GetOriginalWeight(pool, itemId);
                            if (originalWeight <= 0) continue; // item does not originally spawn in this pool

                            if (allowed)
                            {
                                // Unlocked (or ItemSanity off) - ensure it spawns at its original weight
                                if (!LootData.AllSpawnWeightData[pool].ContainsKey(itemId))
                                {
                                    LootData.AllSpawnWeightData[pool].Add(itemId, originalWeight);
                                    restoredCount++;
                                }
                                else if (LootData.AllSpawnWeightData[pool][itemId] != originalWeight)
                                {
                                    LootData.AllSpawnWeightData[pool][itemId] = originalWeight;
                                    restoredCount++;
                                }
                            }
                            else
                            {
                                // Locked - remove it from its normal spawn pool
                                if (LootData.AllSpawnWeightData[pool].ContainsKey(itemId))
                                {
                                    LootData.AllSpawnWeightData[pool].Remove(itemId);
                                    removedCount++;
                                }
                            }
                        }
                    }
                }

                _log?.LogInfo($"[PeakPelago] RefreshLootTables complete: {restoredCount} added/updated, {removedCount} removed");
                _needsRefresh = false;

                // Also refresh scout statue if enabled
                if (_logicalScoutStatue)
                {
                    RefreshScoutStatuePool();
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[PeakPelago] Error refreshing loot tables: {ex.Message}");
                _log?.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Refresh the RespawnCoffin (Scout Statue) pool to only contain items
        /// the player hasn't sent an Acquire check for yet, filtered by biome access
        /// and item unlock status.
        /// </summary>
        public static void RefreshScoutStatuePool()
        {
            try
            {
                if (!_logicalScoutStatue) return;
                if (_session == null || LootData.AllSpawnWeightData == null) return;
                if (!LootData.AllSpawnWeightData.ContainsKey(SpawnPool.RespawnCoffin)) return;

                var plugin = PeakArchipelagoPlugin._instance;
                if (plugin == null) return;

                // Build set of acquire location names that have already been reported
                HashSet<string> reportedAcquires = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_stateData != null)
                {
                    foreach (long checkId in _stateData.ReportedChecks)
                    {
                        // Look up location name from cache
                        string locName = plugin.GetLocationNameById(checkId);
                        if (locName != null && locName.StartsWith("Acquire ", StringComparison.OrdinalIgnoreCase))
                        {
                            reportedAcquires.Add(locName);
                        }
                    }
                }

                HashSet<ushort> unlockedItems = _itemSanityEnabled ? GetUnlockedItemIds() : null;

                // Get the mapping of game item IDs to acquire location names
                var itemIdToLocation = plugin.GetItemIdToLocationMapping();

                // Clear the current RespawnCoffin pool and rebuild it
                var coffinPool = LootData.AllSpawnWeightData[SpawnPool.RespawnCoffin];
                coffinPool.Clear();

                int addedCount = 0;
                foreach (var kvp in itemIdToLocation)
                {
                    ushort itemId = kvp.Key;
                    string locationName = kvp.Value;

                    // Skip if already acquired
                    if (reportedAcquires.Contains(locationName)) continue;

                    // Check biome access
                    if (AcquireBiomeRequirements.TryGetValue(locationName, out int requiredMountain))
                    {
                        if (_progressiveMountainCount < requiredMountain) continue;
                    }

                    // Check item unlock (if ItemSanity is enabled)
                    if (_itemSanityEnabled && unlockedItems != null && !unlockedItems.Contains(itemId)) continue;

                    coffinPool[itemId] = 1;
                    addedCount++;
                }

                _log?.LogInfo($"[PeakPelago] Scout Statue pool refreshed: {addedCount} items available, {reportedAcquires.Count} already acquired");
            }
            catch (Exception ex)
            {
                _log?.LogError($"[PeakPelago] Error refreshing Scout Statue pool: {ex.Message}");
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
