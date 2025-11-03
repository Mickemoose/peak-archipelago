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
                    // Use the static AllCharacters list for random selection
                    if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                    {
                        log.LogWarning("[PeakPelago] Cannot apply yeet trap - no characters found");
                        return;
                    }

                    // Filter to only active, alive characters that are holding items
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

                    // Pick a random character
                    var random = new System.Random();
                    targetCharacter = validCharacters[random.Next(validCharacters.Count)];
                }
                else
                {
                    // Default to local player
                    targetCharacter = Character.localCharacter;
                }

                if (targetCharacter == null || targetCharacter.refs.items == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply yeet trap - target character or items not found");
                    return;
                }

                // Check if character is actually holding an item
                if (targetCharacter.data.currentItem == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply yeet trap - target character not holding an item");
                    return;
                }

                string characterName = targetCharacter == Character.localCharacter ? "local player" : targetCharacter.characterName;
                string itemName = targetCharacter.data.currentItem.UIData?.itemName ?? "item";
                log.LogInfo($"[PeakPelago] Applying Yeet Item Trap to {characterName} - throwing {itemName}!");

                // Apply the yeet during the next fixed update to ensure proper timing
                targetCharacter.StartCoroutine(YeetItemNextFrame(targetCharacter, log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying yeet item trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        private static IEnumerator YeetItemNextFrame(Character targetCharacter, ManualLogSource log)
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
                
                // Check if we have a valid selected slot
                if (!characterItems.currentSelectedSlot.IsSome)
                {
                    log.LogWarning("[PeakPelago] No slot selected, cannot yeet item");
                    yield break;
                }

                characterItems.throwChargeLevel = 1.0f;

                Item currentItem = targetCharacter.data.currentItem;
                Vector3 itemPosition = currentItem.transform.position;
                Vector3 itemVelocity = currentItem.rig.linearVelocity;
                Quaternion itemRotation = currentItem.transform.rotation;

                byte currentSlot = characterItems.currentSelectedSlot.Value;
                
                var itemSlot = targetCharacter.player.GetItemSlot(currentSlot);
                if (itemSlot == null)
                {
                    log.LogWarning("[PeakPelago] Item slot not found");
                    yield break;
                }

                characterItems.photonView.RPC(
                    "DropItemRpc", 
                    Photon.Pun.RpcTarget.All, 
                    1.0f,
                    currentSlot, 
                    itemPosition, 
                    itemVelocity, 
                    itemRotation, 
                    itemSlot.data
                );

                log.LogInfo($"[PeakPelago] Successfully yeeted item from {targetCharacter.characterName ?? "player"}!");
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error during yeet execution: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
    }
}