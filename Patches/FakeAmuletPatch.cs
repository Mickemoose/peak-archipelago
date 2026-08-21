using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;

namespace Peak.AP
{
    [HarmonyPatch]
    public static class FakeAmuletGatePatch
    {
        public static ManualLogSource Log;

        private static ushort ResolveItemId(Item realItemPrefab)
        {
            if (realItemPrefab == null) return 0;
            if (ItemIdMappings.NameToId.TryGetValue(realItemPrefab.name, out ushort id)) return id;
            return realItemPrefab.itemID;
        }

        [HarmonyPatch(typeof(FakeItemManager), nameof(FakeItemManager.RefreshList))]
        [HarmonyPostfix]
        static void RefreshListPostfix(FakeItemManager __instance)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                ApplyGates(__instance);
            }
        }


        private static readonly HashSet<int> _lootReplacedFakes = new HashSet<int>();

        public static void ResetForScene()
        {
            _lootReplacedFakes.Clear();
        }

        public static void ApplyGates(FakeItemManager manager)
        {
            if (manager == null) manager = FakeItemManager.Instance;
            if (manager == null) return;

            Log?.LogInfo($"[PeakPelago] Gating {manager.allFakeItems.Count} fake items (master={PhotonNetwork.IsMasterClient})");

            bool freelyPlaced = UnlockedItemsManager.ScoutAmuletsFreelyPlaced;
            var hide = new List<int>();
            var show = new List<int>();
            foreach (var fake in manager.allFakeItems)
            {
                if (fake == null) continue;
                ushort itemId = ResolveItemId(fake.realItemPrefab);
                if (itemId == 0 || !UnlockedItemsManager.IsAmuletChainItem(itemId)) continue;

                if (freelyPlaced)
                {
                    hide.Add(fake.index);
                    if (PhotonNetwork.IsMasterClient && _lootReplacedFakes.Add(fake.GetInstanceID()))
                    {
                        AmuletSpawnerGatePatch.SpawnLootAt(AmuletSpawnerGatePatch.ResolveBiomePool(fake),
                            fake.transform.position + UnityEngine.Vector3.up * 0.1f, fake.transform.rotation,
                            true, null, $"Amulet statue '{fake.itemName}'");
                    }
                    continue;
                }

                bool allowed = UnlockedItemsManager.CanSpawnScoutAmulet(itemId);
                Log?.LogInfo($"[PeakPelago] Fake amulet [{fake.index}] '{fake.itemName}' prefab='{fake.realItemPrefab?.name}' (id {itemId}): {(allowed ? "shown" : "hidden")}");
                if (allowed) show.Add(fake.index);
                else hide.Add(fake.index);
            }

            if (hide.Count > 0 || show.Count > 0)
            {
                PeakArchipelagoPlugin._instance?.BroadcastFakeAmuletStates(hide.ToArray(), show.ToArray());
            }
        }

        public static void ApplyLocalStates(int[] hide, int[] show)
        {
            var manager = FakeItemManager.Instance;
            if (manager == null) return;

            foreach (var fake in manager.allFakeItems)
            {
                if (fake == null) continue;
                if (hide != null && hide.Contains(fake.index))
                {
                    fake.gameObject.SetActive(false);
                }
                else if (show != null && show.Contains(fake.index) && !fake.pickedUp)
                {
                    fake.gameObject.SetActive(true);
                }
            }
        }

        [HarmonyPatch(typeof(FakeItemManager), nameof(FakeItemManager.RPC_RequestFakeItemPickup))]
        [HarmonyPrefix]
        static bool BlockLockedPickup(FakeItemManager __instance, PhotonView characterView, int fakeItemIndex)
        {
            if (!IsIndexLocked(__instance, fakeItemIndex)) return true;

            var character = characterView != null ? characterView.GetComponent<Character>() : null;
            if (character != null && character.player?.photonView?.Owner != null)
            {
                __instance.photonView.RPC("RPC_DenyFakeItemPickup", character.player.photonView.Owner, fakeItemIndex);
            }
            return false;
        }

        [HarmonyPatch(typeof(FakeItemManager), nameof(FakeItemManager.RPC_RequestStickFakeItemToPlayer))]
        [HarmonyPrefix]
        static bool BlockLockedStick(FakeItemManager __instance, int fakeItemIndex)
        {
            if (!IsIndexLocked(__instance, fakeItemIndex)) return true;

            __instance.photonView.RPC("RPC_DenyFakeItemPickup", RpcTarget.All, fakeItemIndex);
            return false;
        }

        private static bool IsIndexLocked(FakeItemManager manager, int fakeItemIndex)
        {
            if (manager == null) return false;
            foreach (var fake in manager.allFakeItems)
            {
                if (fake == null || fake.index != fakeItemIndex) continue;
                ushort itemId = ResolveItemId(fake.realItemPrefab);
                if (itemId == 0) return false;
                bool locked = (UnlockedItemsManager.IsAmuletChainItem(itemId) && UnlockedItemsManager.ScoutAmuletsFreelyPlaced)
                    || !UnlockedItemsManager.CanSpawnScoutAmulet(itemId);
                if (locked)
                {
                    Log?.LogInfo($"[PeakPelago] Blocked fake amulet pickup '{fake.itemName}' - unlock not yet received");
                }
                return locked;
            }
            return false;
        }
    }
}
