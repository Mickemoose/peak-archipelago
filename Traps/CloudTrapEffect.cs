using System;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class CloudTrapEffect
    {
        private static Material _cloudMaterial;

        public static void ApplyCloudTrap(ManualLogSource log, string trapLabel,
            CharacterAfflictions.STATUSTYPE status, float amount, Color color,
            float knockback = 0f, float verticalKnockback = 0f)
        {
            try
            {
                var targetCharacter = TrapHelpers.GetRandomValidCharacter();
                if (targetCharacter == null)
                {
                    log.LogWarning($"[PeakPelago] Cannot apply {trapLabel} - no valid characters found");
                    return;
                }

                string characterName = targetCharacter == Character.localCharacter
                    ? "local player"
                    : targetCharacter.characterName;
                log.LogInfo($"[PeakPelago] Applying {trapLabel} on {characterName}");

                Vector3 position = targetCharacter.Center;
                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("SpawnCloudTrapRPC", RpcTarget.All,
                        position, (int)status, amount, color.r, color.g, color.b, knockback, verticalKnockback);
                }
                else
                {
                    SpawnCloudLocal(position, status, amount, color, log, knockback, verticalKnockback);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying {trapLabel}: {ex.Message}");
            }
        }

        public static void SpawnCloudLocal(Vector3 position, CharacterAfflictions.STATUSTYPE status,
            float amount, Color color, ManualLogSource log,
            float knockback = 0f, float verticalKnockback = 0f)
        {
            try
            {
                var cloudObj = new GameObject("AP_CloudTrap");
                cloudObj.transform.position = position;

                var aoe = cloudObj.AddComponent<AOE>();
                aoe.auto = false;
                aoe.onEnable = false;
                aoe.mask = HelperFunctions.LayerType.AllPhysical;
                bool hasBlast = knockback > 0f;
                aoe.range = hasBlast ? 5f : 6f;
                aoe.knockback = knockback;
                aoe.additionalVerticalKnockback = verticalKnockback;
                aoe.fallTime = hasBlast ? 0.5f : 0f;
                aoe.minFactor = hasBlast ? 0.2f : 0.4f;
                aoe.statusType = status;
                aoe.statusAmount = amount;
                aoe.Explode();

                SpawnCloudVisual(cloudObj, color);
                UnityEngine.Object.Destroy(cloudObj, 5f);
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error spawning cloud: {ex.Message}");
            }
        }

        private static void SpawnCloudVisual(GameObject host, Color color)
        {
            var ps = host.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = 2.5f;
            main.startSpeed = 1.5f;
            main.startSize = 2.2f;
            main.startColor = new Color(color.r, color.g, color.b, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2.5f;

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            var material = GetCloudMaterial();
            if (material != null)
            {
                renderer.material = material;
            }

            ps.Play();
        }

        private static Material GetCloudMaterial()
        {
            if (_cloudMaterial != null) return _cloudMaterial;

            var renderers = Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>();
            foreach (var r in renderers)
            {
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null)
                {
                    _cloudMaterial = r.sharedMaterial;
                    break;
                }
            }
            return _cloudMaterial;
        }
    }
}
