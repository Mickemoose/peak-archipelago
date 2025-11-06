using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace Peak.AP
{
    public static class ItemToWhateverTrapEffect
    {
        public static void ApplyItemToWhateverTrap(ManualLogSource log, string itemName)
        {
            try
            {
                // Get all valid characters
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Item to Whatever trap - no characters found");
                    return;
                }

                var validCharacters = Character.AllCharacters.Where(c => 
                    c != null && 
                    c.gameObject.activeInHierarchy && 
                    !c.data.dead &&
                    c.data.currentItem != null
                ).ToList();

                if (validCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Item to WHATEVA trap - no characters holding items");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Item to Whatver Trap to {validCharacters.Count} character(s)!");

                // Apply to all characters holding items
                foreach (var character in validCharacters)
                {
                    character.StartCoroutine(ReplaceItemWithWhatever(character, log, itemName));
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Item to Whateverrr trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        private static IEnumerator ReplaceItemWithWhatever(Character targetCharacter, ManualLogSource log, string itemNameToGive)
        {
            if (targetCharacter == null || targetCharacter.refs.items == null)
            {
                log.LogWarning("[PeakPelago] Character or items became null during item replacement");
                yield break;
            }

            if (targetCharacter.data.currentItem == null)
            {
                log.LogWarning("[PeakPelago] Item became null during replacement");
                yield break;
            }

            var characterItems = targetCharacter.refs.items;
            
            // Check if we have a valid selected slot
            if (!characterItems.currentSelectedSlot.IsSome)
            {
                log.LogWarning("[PeakPelago] No slot selected, cannot replace item");
                yield break;
            }

            string itemName = targetCharacter.data.currentItem.UIData?.itemName ?? "item";
            string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
            
            byte currentSlot = characterItems.currentSelectedSlot.Value;
            
            // Get the item slot
            var itemSlot = targetCharacter.player.GetItemSlot(currentSlot);
            if (itemSlot == null)
            {
                log.LogWarning("[PeakPelago] Item slot not found");
                yield break;
            }

            // Destroy the held item
            characterItems.photonView.RPC("DestroyHeldItemRpc", RpcTarget.All);
            
            yield return new WaitForSeconds(0.1f);

            // Empty the slot
            targetCharacter.player.EmptySlot(Optionable<byte>.Some(currentSlot));
            
            yield return new WaitForSeconds(0.1f);

            // Call the RPC directly to spawn watevr in hand
            characterItems.photonView.RPC("RPC_SpawnItemInHandMaster", RpcTarget.MasterClient, itemNameToGive);
            
            log.LogInfo($"[PeakPelago] Replaced {itemName} with {itemNameToGive} in slot {currentSlot} for {characterName}!");
        }
    }
}