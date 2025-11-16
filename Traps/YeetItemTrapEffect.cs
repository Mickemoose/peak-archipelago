using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace Peak.AP
{
    public static class YeetItemTrapEffect
    {
        public enum TargetMode
        {
            LocalPlayer,
            RandomPlayer
        }

        public static void ApplyYeetTrap(ManualLogSource log, TargetMode targetMode = TargetMode.RandomPlayer)
        {
            try
            {
                Character targetCharacter;

                if (targetMode == TargetMode.RandomPlayer)
                {
                    if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply yeet trap - no characters found");
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
                        log.LogWarning("[PeakPelago] Cannot apply yeet trap - no valid characters holding items");
                        return;
                    }

                    var random = new System.Random();
                    targetCharacter = validCharacters[random.Next(validCharacters.Count)];
                }
                else
                {
                    targetCharacter = Character.localCharacter;
                }

                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply yeet trap - target character not found");
                    return;
                }

                if (targetCharacter.data.currentItem == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply yeet trap - target character not holding an item");
                    return;
                }

                string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
                string itemName = targetCharacter.data.currentItem.UIData?.itemName ?? "item";
                log.LogInfo($"[PeakPelago] Applying Yeet Item Trap to {characterName} - throwing {itemName}!");

                targetCharacter.StartCoroutine(YeetItemNextFrame(targetCharacter, characterName, log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying yeet item trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        private static IEnumerator YeetItemNextFrame(Character targetCharacter, string characterName, ManualLogSource log)
        {
            yield return new WaitForFixedUpdate();

            try
            {
                if (targetCharacter == null || targetCharacter.refs.items == null)
                {
                    log.LogWarning("[PeakPelago] Character or items became null during yeet");
                    yield break;
                }

                if (targetCharacter.data.currentItem == null)
                {
                    log.LogWarning("[PeakPelago] Item became null during yeet");
                    yield break;
                }

                var characterItems = targetCharacter.refs.items;
                Item currentItem = targetCharacter.data.currentItem;
                
                if (!characterItems.currentSelectedSlot.IsSome)
                {
                    log.LogWarning("[PeakPelago] No slot selected, cannot yeet item");
                    yield break;
                }

                byte currentSlot = characterItems.currentSelectedSlot.Value;
                
                var itemSlot = targetCharacter.player.GetItemSlot(currentSlot);
                if (itemSlot == null)
                {
                    log.LogWarning("[PeakPelago] Item slot not found");
                    yield break;
                }

                // Set max throw charge
                characterItems.throwChargeLevel = 1.0f;
                characterItems.photonView.RPC(
                    "DropItemRpc", 
                    Photon.Pun.RpcTarget.All, 
                    1.0f,                           // float throwCharge
                    currentSlot,                    // byte slotID
                    currentItem.transform.position, // Vector3 spawnPos
                    currentItem.rig.linearVelocity, // Vector3 velocity
                    currentItem.transform.rotation, // Quaternion rotation
                    itemSlot.data,                  // ItemInstanceData itemInstanceData
                    false                           // bool cacheToDroppedItems
                );

                log.LogInfo($"[PeakPelago] Successfully yeeted item from {characterName}!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error during yeet execution: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}