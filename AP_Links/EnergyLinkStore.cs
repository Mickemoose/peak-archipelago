using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using System.Linq;
using System.IO;
using System;
using Photon.Pun;
using System.Collections;

namespace Peak.AP
{
    [HarmonyPatch]
    public class CampfireModelSpawner
    {
        private static GameObject modelPrefab;
        private static AnimationClip openAnimationClip;
        private static ManualLogSource _log;
        private static readonly Dictionary<Campfire, GameObject> spawnedModels = new Dictionary<Campfire, GameObject>();
        private static EnergyLinkService _energyLinkService;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
            LoadModelPrefab();
        }

        public static void SetEnergyLinkService(EnergyLinkService service)
        {
            _energyLinkService = service;
            _log?.LogInfo("[CampfireSpawner] EnergyLinkService reference set");
        }

        private static void LoadModelPrefab()
        {
            string bundlePath = Path.Combine(BepInEx.Paths.PluginPath, "peakpelago", "energylinkstore.peakbundle");
            
            if (File.Exists(bundlePath))
            {
                try
                {
                    AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                    if (bundle != null)
                    {
                        _log?.LogInfo("[CampfireSpawner] All assets in bundle:");
                        foreach (var name in bundle.GetAllAssetNames())
                        {
                            _log?.LogInfo($"[CampfireSpawner]   - {name}");
                        }
                        modelPrefab = bundle.LoadAsset<GameObject>("energy_link_store");
                        openAnimationClip = bundle.LoadAsset<AnimationClip>("armature_anim_open");
                        
                        if (modelPrefab != null)
                        {
                            UnityEngine.Object.DontDestroyOnLoad(modelPrefab);
                            if (openAnimationClip != null)
                            {
                                UnityEngine.Object.DontDestroyOnLoad(openAnimationClip);
                                _log?.LogInfo($"[CampfireSpawner] Loaded model and animation from AssetBundle");
                            }
                            else
                            {
                                _log?.LogWarning("[CampfireSpawner] Animation clip not found in bundle");
                            }
                            _log?.LogInfo($"[CampfireSpawner] Loaded model '{modelPrefab.name}' from AssetBundle");
                            return;
                        }
                        else
                        {
                            _log?.LogError("[CampfireSpawner] Model not found in AssetBundle. Available assets:");
                            foreach (var name in bundle.GetAllAssetNames())
                            {
                                _log?.LogInfo($"[CampfireSpawner]   - {name}");
                            }
                        }
                    }
                    else
                    {
                        _log?.LogError("[CampfireSpawner] Failed to load AssetBundle");
                    }
                }
                catch (Exception ex)
                {
                    _log?.LogError($"[CampfireSpawner] Error loading AssetBundle: {ex.Message}");
                }
            }
            else
            {
                _log?.LogWarning($"[CampfireSpawner] AssetBundle not found at: {bundlePath}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Campfire), nameof(Campfire.Awake))]
        static void OnCampfireAwake(Campfire __instance)
        {
            if (modelPrefab == null)
            {
                _log?.LogWarning("[CampfireSpawner] Model prefab not loaded!");
                return;
            }

            GameObject model = UnityEngine.Object.Instantiate(modelPrefab);
            model.SetActive(true);
            model.name = $"APCampfireModel_{__instance.GetInstanceID()}";
            ApplyPeakShaderToModel(model);

            Vector3 campfirePos = __instance.transform.position;
            
            Vector3 offset = new Vector3(6f, -0.25f, 2f);
            model.transform.position = campfirePos + offset;
            model.transform.LookAt(campfirePos);
            
            Vector3 currentRotation = model.transform.eulerAngles;
            model.transform.eulerAngles = new Vector3(0f, currentRotation.y, 0f);
            model.transform.localScale = Vector3.one * 2f;
            model.transform.SetParent(__instance.transform, worldPositionStays: true);
            var interactable = model.AddComponent<EnergyLinkStoreInteractable>();
            interactable.Initialize(_log, _energyLinkService, openAnimationClip);
            
            spawnedModels[__instance] = model;

            _log?.LogInfo($"[CampfireSpawner] Spawned model at {model.transform.position}");
        }

        public static void CleanupAllModels()
        {
            foreach (var kvp in spawnedModels.ToList())
            {
                if (kvp.Value != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }
            spawnedModels.Clear();
            _log?.LogInfo("[CampfireSpawner] Cleaned up all campfire models");
        }

        public static void CleanupDestroyedCampfires()
        {
            var toRemove = spawnedModels.Where(kvp => kvp.Key == null || kvp.Value == null).Select(kvp => kvp.Key).ToList();

            foreach (var campfire in toRemove)
            {
                spawnedModels.Remove(campfire);
            }

            if (toRemove.Count > 0)
            {
                _log?.LogDebug($"[CampfireSpawner] Cleaned up {toRemove.Count} destroyed campfire entries");
            }
        }
        
        private static void ApplyPeakShaderToModel(GameObject model)
        {
            try
            {
                var peakShader = Shader.Find("W/Peak_Standard");
                if (peakShader == null)
                {
                    _log?.LogWarning("[CampfireSpawner] W/Peak_Standard shader not found!");
                    return;
                }

                var renderers = model.GetComponentsInChildren<Renderer>(true);
                _log?.LogInfo($"[CampfireSpawner] Found {renderers.Length} renderers in model");

                int fixedCount = 0;
                foreach (var renderer in renderers)
                {
                    renderer.enabled = true;
                    
                    var materials = renderer.materials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var oldMaterial = materials[i];
                        
                        // Store original shit
                        Color originalColor = oldMaterial.color;
                        Texture originalMainTex = oldMaterial.mainTexture;
                        
                        _log?.LogInfo($"[CampfireSpawner] Material '{oldMaterial.name}': Color={originalColor}, HasTexture={originalMainTex != null}");
                        
                        // Change shader
                        oldMaterial.shader = peakShader;
                        
                        // Set colors
                        if (oldMaterial.HasProperty("_BaseColor"))
                        {
                            oldMaterial.SetColor("_BaseColor", originalColor);
                        }
                        if (oldMaterial.HasProperty("_Tint"))
                        {
                            oldMaterial.SetColor("_Tint", originalColor);
                        }
                        
                        // Set textures
                        if (originalMainTex != null)
                        {
                            if (oldMaterial.HasProperty("_BaseTexture"))
                            {
                                oldMaterial.SetTexture("_BaseTexture", originalMainTex);
                                _log?.LogInfo($"[CampfireSpawner]   Set _BaseTexture");
                            }
                            if (oldMaterial.HasProperty("_MainTex"))
                            {
                                oldMaterial.SetTexture("_MainTex", originalMainTex);
                                _log?.LogInfo($"[CampfireSpawner]   Set _MainTex");
                            }
                        }
                        
                        fixedCount++;
                    }
                }
                
                _log?.LogInfo($"[CampfireSpawner] Applied Peak shader to {fixedCount} materials across {renderers.Length} renderers");
            }
            catch (System.Exception ex)
            {
                _log?.LogError($"[CampfireSpawner] Error applying Peak shader: {ex.Message}");
                _log?.LogError($"[CampfireSpawner] Stack trace: {ex.StackTrace}");
            }
        }

        public static int GetSpawnedModelCount()
        {
            return spawnedModels.Count;
        }
    }

    public class EnergyLinkStoreInteractable : MonoBehaviour, IInteractible, IInteractibleConstant
    {
        private ManualLogSource _log;
        private EnergyLinkService _energyLinkService;
        private BoxCollider _collider;
        private Animation _animation;
        private int _cachedEnergy = 0;
        private int _cachedMaxEnergy = 0;
        private string _selectedBundleName;
        private Action _selectedBundleAction;
        private const int BUNDLE_COST = 300;
        private const float PURCHASE_TIME = 3f;
        private static readonly Dictionary<string, BundleDefinition> BundleDefinitions = new Dictionary<string, BundleDefinition>
        {
            { "Bundle: Glizzy Gobbler", new BundleDefinition("Glizzy", 3) },
            { "Bundle: Marshmallow Muncher", new BundleDefinition("Marshmallow", 3) },
            { "Bundle: Trailblazer Snacks", new BundleDefinition(new[] {
                ("Granola Bar", 2),
                ("TrailMix", 2)
            }) },
            { "Bundle: Lovely Bunch", new BundleDefinition("Item_Coconut", 3) },
            { "Bundle: Bear Favorite", new BundleDefinition("Item_Honeycomb", 6) },
            { "Bundle: Rainy Day", new BundleDefinition("Parasol", 4) },
            { "Bundle: Turkey Day", new BundleDefinition("EggTurkey", 3) },
        };
        
        private class BundleDefinition
        {
            public (string itemName, int count)[] Items { get; }
            public BundleDefinition(string itemName, int count)
            {
                Items = new[] { (itemName, count) };
            }
            public BundleDefinition((string itemName, int count)[] items)
            {
                Items = items;
            }
        }

        public void Initialize(ManualLogSource log, EnergyLinkService energyLinkService, AnimationClip openClip)
        {
            _log = log;
            _energyLinkService = energyLinkService;
            
            var physicsCollider = gameObject.AddComponent<BoxCollider>();
            physicsCollider.center = new Vector3(-0.16f, 0f, 0.1f);
            physicsCollider.size = new Vector3(1.5f, 2.7f, 1.05f);
            physicsCollider.isTrigger = false;
            
            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.center = new Vector3(-0.16f, 0f, 0.1f);
            _collider.size = new Vector3(1.5f, 2.7f, 1.05f);
            _collider.isTrigger = true;

            if (openClip != null)
            {
                _animation = gameObject.AddComponent<Animation>();
                _animation.AddClip(openClip, "open_animation");
                _log?.LogInfo("[EnergyLinkStore] Added animation component");
            }
                    
            if (_energyLinkService?.IsEnabled() == true)
            {
                _cachedEnergy = _energyLinkService.GetCurrentEnergy();
                _cachedMaxEnergy = _energyLinkService.GetMaxEnergy();
                _log?.LogInfo($"[EnergyLinkStore] Initial energy: {_cachedEnergy}/{_cachedMaxEnergy}");
            }
            
            SelectRandomBundle();
        }

        private void SelectRandomBundle()
        {
            var bundleList = BundleDefinitions.ToList();
            var randomIndex = UnityEngine.Random.Range(0, bundleList.Count);
            var selectedBundle = bundleList[randomIndex];

            _selectedBundleName = selectedBundle.Key;
            var bundleDef = selectedBundle.Value;
            _selectedBundleAction = () => DispenseBundle(bundleDef);

            _log?.LogInfo($"[EnergyLinkStore] Selected bundle: {_selectedBundleName}");
        }

        private void DispenseBundle(BundleDefinition bundle)
        {
            if (_animation != null)
            {
                _log?.LogInfo("[EnergyLinkStore] Playing open animation");
                _animation.Play("open_animation");
            }

            StartCoroutine(DispenseAfterDelay(bundle, 1.2f));
        }
        
        private IEnumerator DispenseAfterDelay(BundleDefinition bundle, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            Vector3 dispenserOffset = new Vector3(0f, 1.5f, 1f);
            Vector3 dispenserPosition = transform.position + transform.TransformDirection(dispenserOffset);

            _log?.LogInfo($"[EnergyLinkStore] Dispensing from position: {dispenserPosition}");
            foreach (var (itemName, count) in bundle.Items)
            {
                SpawnPhysicalItems(itemName, count, dispenserPosition);
            }
        }
        
        private void SpawnPhysicalItems(string itemName, int count, Vector3 basePosition)
        {
            try
            {
                // Find the item in the database
                Item itemToSpawn = null;
                for (ushort itemID = 1; itemID < 1000; itemID++)
                {
                    if (ItemDatabase.TryGetItem(itemID, out Item item))
                    {
                        if (item.name.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                        {
                            itemToSpawn = item;
                            break;
                        }
                    }
                }

                if (itemToSpawn == null)
                {
                    _log?.LogWarning($"[EnergyLinkStore] Could not find item in database: {itemName}");
                    return;
                }
                for (int i = 0; i < count; i++)
                {
                    // Add some randomness so items don't spawn in exact same spot
                    Vector3 randomOffset = new Vector3(
                        UnityEngine.Random.Range(-0.3f, 0.3f),
                        UnityEngine.Random.Range(0f, 0.2f),
                        UnityEngine.Random.Range(-0.3f, 0.3f)
                    );
                    
                    Vector3 spawnPosition = basePosition + randomOffset;
                    
                    // Add slight outward velocity
                    Vector3 dispenserForward = transform.forward;
                    Quaternion rotation = Quaternion.LookRotation(dispenserForward);
                    
                    GameObject spawnedItem = PhotonNetwork.Instantiate(
                        "0_Items/" + itemToSpawn.name, 
                        spawnPosition, 
                        rotation, 
                        0
                    );
                    
                    var rb = spawnedItem.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 force = dispenserForward * UnityEngine.Random.Range(2f, 3f) + Vector3.up * 2f;
                        rb.AddForce(force, ForceMode.Impulse);
                    }
                    
                    _log?.LogInfo($"[EnergyLinkStore] Spawned {itemName} at {spawnPosition}");
                    
                    // Small delay between spawns to prevent physics explosions
                    System.Threading.Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[EnergyLinkStore] Error spawning items: {ex.Message}");
            }
        }

        public Vector3 Center()
        {
            if (_collider != null)
                return _collider.bounds.center;
            return transform.position;
        }

        public string GetInteractionText()
        {
            return $"{_selectedBundleName}\n{BUNDLE_COST}J";
        }

        public string GetName()
        {
            return "Energy Link Store";
        }

        public Transform GetTransform()
        {
            return transform;
        }

        public void HoverEnter()
        {
            if (_energyLinkService?.IsEnabled() == true)
            {
                _cachedEnergy = _energyLinkService.GetCurrentEnergy();
                _cachedMaxEnergy = _energyLinkService.GetMaxEnergy();
                _log?.LogDebug($"[EnergyLinkStore] Refreshed energy on hover: {_cachedEnergy}/{_cachedMaxEnergy}");
            }
        }

        public void HoverExit()
        {
        }

        public void Interact(Character interactor)
        {
            _log?.LogInfo($"[EnergyLinkStore] Player {interactor.photonView.Owner.NickName} interacted with Energy Link Store");
        }

        public void Interact_CastFinished(Character interactor)
        {
            _log?.LogInfo($"[EnergyLinkStore] Interact_CastFinished called!");
            
            if (_energyLinkService?.IsEnabled() != true)
            {
                _log?.LogWarning("[EnergyLinkStore] EnergyLink not enabled");
                return;
            }
            if (_cachedEnergy < BUNDLE_COST)
            {
                _log?.LogInfo($"[EnergyLinkStore] Not enough energy! Need {BUNDLE_COST}J, have {_cachedEnergy}J");
                return;
            }

            if (_energyLinkService.ConsumeEnergy(BUNDLE_COST))
            {
                _log?.LogInfo($"[EnergyLinkStore] Purchase successful! Dispensing {_selectedBundleName}");
                _selectedBundleAction?.Invoke();
                _cachedEnergy = _energyLinkService.GetCurrentEnergy();
                _cachedMaxEnergy = _energyLinkService.GetMaxEnergy();
            }
            else
            {
                _log?.LogWarning("[EnergyLinkStore] Failed to consume energy");
            }
        }

        public void CancelCast(Character interactor)
        {
        }

        public void ReleaseInteract(Character interactor)
        {
            // Called when interaction is released
        }

        public bool IsInteractible(Character interactor)
        {
            // Only allow interaction if EnergyLink is enabled and we have enough energy
            if (_energyLinkService?.IsEnabled() != true)
                return false;
                
            return _cachedEnergy >= BUNDLE_COST;
        }

        public bool IsConstantlyInteractable(Character interactor)
        {
            return IsInteractible(interactor);
        }

        public float GetInteractTime(Character interactor)
        {
            return PURCHASE_TIME;
        }

        public bool holdOnFinish => false;
    }
}