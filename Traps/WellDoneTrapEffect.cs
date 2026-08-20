using System;
using BepInEx.Logging;
using Peak.Network;
using Photon.Pun;
using Zorro.Core.Serizalization;

namespace Peak.AP
{
    public static class WellDoneTrapEffect
    {
        public static void ApplyWellDoneTrap(ManualLogSource log)
        {
            try
            {
                log.LogInfo("[PeakPelago] Applying Well Done Trap - cooking everyone's inventories");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("StartWellDoneTrapRPC", RpcTarget.All);
                }
                else
                {
                    CookLocalInventory(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Well Done Trap: {ex.Message}");
            }
        }

        public static void CookLocalInventory(ManualLogSource log)
        {
            try
            {
                var character = Character.localCharacter;
                if (character == null || character.player == null) return;
                var player = character.player;

                int cooked = 0;
                foreach (var slot in player.itemSlots)
                {
                    cooked += CookSlot(slot);
                }
                cooked += CookSlot(player.tempFullSlot);
                cooked += CookSlot(player.backpackSlot);

                if (player.backpackSlot != null && !player.backpackSlot.IsEmpty() && player.backpackSlot.data != null &&
                    player.backpackSlot.data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out var backpackData))
                {
                    foreach (var slot in backpackData.itemSlots)
                    {
                        cooked += CookSlot(slot);
                    }
                }

                var heldItem = character.data.currentItem;
                if (heldItem != null)
                {
                    var cooking = heldItem.GetComponent<ItemCooking>();
                    if (cooking != null && cooking.canBeCooked)
                    {
                        cooking.FinishCooking();
                        cooked++;
                    }
                }

                if (cooked > 0)
                {
                    var syncData = IBinarySerializable.ToManagedArray(
                        new InventorySyncData(player.itemSlots, player.backpackSlot, player.tempFullSlot));
                    player.view.RPC("SyncInventoryRPC", RpcTarget.Others, syncData, false);
                }

                log.LogInfo($"[PeakPelago] Well Done Trap cooked {cooked} item(s)");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error cooking inventory: {ex.Message}");
            }
        }

        private static int CookSlot(ItemSlot slot)
        {
            if (slot == null || slot.IsEmpty() || slot.prefab == null || slot.data == null) return 0;

            var cooking = slot.prefab.GetComponent<ItemCooking>();
            if (cooking == null || !cooking.canBeCooked) return 0;

            IntItemData cookedData;
            if (!slot.data.TryGetDataEntry<IntItemData>(DataEntryKey.CookedAmount, out cookedData))
            {
                cookedData = slot.data.RegisterNewEntry<IntItemData>(DataEntryKey.CookedAmount);
            }

            if (cooking.wreckWhenCooked)
            {
                cookedData.Value = 5;
            }
            else if (cookedData.Value < 12)
            {
                cookedData.Value++;
            }
            return 1;
        }
    }
}
