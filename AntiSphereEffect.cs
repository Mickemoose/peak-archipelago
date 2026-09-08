using System;
using System.Collections;
using UnityEngine;
using BepInEx.Logging;
using Photon.Pun;

namespace Peak.AP
{
    public static class AntiSphereEffect
    {
        public const float DEFAULT_LIFETIME = 60f;
        public const float SHRINK_DURATION = 2f;

        public static void SpawnAntiSphereOnPlayer(ManualLogSource log, PhotonView view, float lifetime = DEFAULT_LIFETIME)
        {
            try
            {
                var targetCharacter = TrapHelpers.GetRandomValidCharacter();
                if (targetCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot spawn anti-sphere - no valid characters found");
                    return;
                }

                string characterName = targetCharacter == Character.localCharacter
                    ? "local player"
                    : targetCharacter.characterName;

                Vector3 spawnPosition = targetCharacter.Center;
                log.LogInfo($"[PeakPelago] Spawning anti-sphere at {characterName}'s position: {spawnPosition}");

                if (view != null && PhotonNetwork.IsConnected)
                {
                    view.RPC("RPC_SpawnAntiSphere", RpcTarget.All, spawnPosition.x, spawnPosition.y, spawnPosition.z, lifetime);
                }
                else
                {
                    SpawnAntiSphereAt(log, spawnPosition, lifetime);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error spawning anti-sphere: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static void SpawnAntiSphereAt(ManualLogSource log, Vector3 position, float lifetime)
        {
            try
            {
                GameObject template = ResolveTemplate();
                if (template == null)
                {
                    log.LogWarning("[PeakPelago] Anti-Sphere - could not find an AntiSphere to clone");
                    return;
                }

                GameObject sphere = UnityEngine.Object.Instantiate(template, position, Quaternion.identity);
                sphere.name = "AntiSphere_PeakPelago";
                sphere.SetActive(true);

                var antiSphere = sphere.GetComponent<global::Peak.AntiSphere>();
                if (antiSphere == null)
                {
                    log.LogWarning("[PeakPelago] Anti-Sphere - clone has no AntiSphere component, falling back to timed destroy");
                    if (lifetime > 0f)
                    {
                        UnityEngine.Object.Destroy(sphere, lifetime);
                    }
                    return;
                }

                if (lifetime > 0f)
                {
                    antiSphere.hasLifetime = false;
                }

                var controller = sphere.AddComponent<AntiSphereLifetime>();
                controller.Begin(antiSphere, lifetime);
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error creating anti-sphere: {ex.Message}");
            }
        }

        private static GameObject ResolveTemplate()
        {
            GameObject prefab = Resources.Load<GameObject>("AntiSphere");
            if (prefab != null) return prefab;

            var existing = UnityEngine.Object.FindAnyObjectByType<global::Peak.AntiSphere>(FindObjectsInactive.Include);
            if (existing != null) return existing.gameObject;

            var all = Resources.FindObjectsOfTypeAll<global::Peak.AntiSphere>();
            if (all.Length > 0) return all[0].gameObject;

            return null;
        }
    }

    public class AntiSphereLifetime : MonoBehaviour
    {
        private global::Peak.AntiSphere _sphere;
        private bool _released;

        public void Begin(global::Peak.AntiSphere sphere, float lifetime)
        {
            _sphere = sphere;
            if (lifetime > 0f)
            {
                StartCoroutine(LifetimeRoutine(lifetime));
            }
        }

        private IEnumerator LifetimeRoutine(float lifetime)
        {
            yield return new WaitForSeconds(lifetime);

            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < AntiSphereEffect.SHRINK_DURATION)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / AntiSphereEffect.SHRINK_DURATION);
                transform.localScale = startScale * (1f - progress * progress * progress);
                yield return null;
            }

            ReleaseCharacters();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            ReleaseCharacters();
        }

        private void ReleaseCharacters()
        {
            if (_released) return;
            _released = true;

            try
            {
                if (ReferenceEquals(_sphere, null)) return;
                var active = _sphere.ACTIVE_CHARACTERS;
                if (active == null || active.Count == 0) return;

                Character local = Character.localCharacter;
                bool localWasInside = false;
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    Character character = active[i];
                    active.RemoveAt(i);
                    if (local != null && character == local)
                    {
                        localWasInside = true;
                    }
                }

                if (localWasInside && GUIManager.instance != null)
                {
                    GUIManager.instance.EndAnti();
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
