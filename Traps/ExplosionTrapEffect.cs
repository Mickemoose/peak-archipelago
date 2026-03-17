using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace Peak.AP
{
    public static class ExplosionTrapEffect
    {
        public static void ApplyExplosionTrap(ManualLogSource log)
        {
            try
            {
                List<Character> players = PlayerHandler.GetAllPlayerCharacters();
                if (players == null || players.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Explosion Trap - no players found");
                    return;
                }

                Character target = players[UnityEngine.Random.Range(0, players.Count)];
                int actorNumber = target.photonView.Owner.ActorNumber;

                log.LogInfo($"[PeakPelago] Applying Explosion Trap on {target.photonView.Owner.NickName}!");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("StartExplosionTrapRPC", Photon.Pun.RpcTarget.All, actorNumber);
                }
                else
                {
                    SpawnExplosionOnLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Explosion Trap: {ex.Message}");
            }
        }

        public static void SpawnExplosionOnLocal(ManualLogSource log)
        {
            if (Character.localCharacter == null) return;
            SpawnExplosionAt(Character.localCharacter.Center, log);
        }

        public static void SpawnExplosionAt(Vector3 position, ManualLogSource log)
        {
            Dynamite template = UnityEngine.Object.FindObjectOfType<Dynamite>(true);
            if (template == null)
            {
                var allTemplates = Resources.FindObjectsOfTypeAll<Dynamite>();
                if (allTemplates.Length > 0)
                    template = allTemplates[0];
            }

            if (template == null || template.explosionPrefab == null)
            {
                log.LogWarning("[PeakPelago] Explosion Trap - could not find Dynamite explosionPrefab");
                return;
            }

            UnityEngine.Object.Instantiate(template.explosionPrefab, position, Quaternion.identity);
        }
    }
}
