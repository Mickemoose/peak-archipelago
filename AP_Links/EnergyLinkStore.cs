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
        private static ManualLogSource _log;
        private static readonly Dictionary<Campfire, GameObject> spawnedModels = new Dictionary<Campfire, GameObject>();
        private static EnergyLinkService _energyLinkService;
        public static Texture2D redEmissiveTexture;
        public static Texture2D greenEmissiveTexture;

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
                        modelPrefab = bundle.LoadAsset<GameObject>("EnergyLinkStore");
                        redEmissiveTexture = bundle.LoadAsset<Texture2D>("RED");
                        greenEmissiveTexture = bundle.LoadAsset<Texture2D>("GREEN");
                        
                        if (modelPrefab != null)
                        {
                            UnityEngine.Object.DontDestroyOnLoad(modelPrefab);
                            _log?.LogInfo($"[CampfireSpawner] Loaded prefab from AssetBundle");
                        }
                        
                        if (redEmissiveTexture != null)
                        {
                            UnityEngine.Object.DontDestroyOnLoad(redEmissiveTexture);
                            _log?.LogInfo($"[CampfireSpawner] Loaded RED emissive texture");
                        }
                        
                        if (greenEmissiveTexture != null)
                        {
                            UnityEngine.Object.DontDestroyOnLoad(greenEmissiveTexture);
                            _log?.LogInfo($"[CampfireSpawner] Loaded GREEN emissive texture");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log?.LogError($"[CampfireSpawner] Error loading AssetBundle: {ex.Message}");
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Campfire), nameof(Campfire.Awake))]
        static void OnCampfireAwake(Campfire __instance)
        {
            if (_energyLinkService?.IsEnabled() != true) return;
            if (modelPrefab == null) return;

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
            interactable.Initialize(_log, _energyLinkService, null); // Pass null - animation is on prefab
            
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
        private Animator _animator;
        private int _cachedEnergy = 0;
        private int _cachedMaxEnergy = 0;
        private string _selectedBundleName;
        private Action _selectedBundleAction;
        private const int BUNDLE_COST = 300;
        private const float PURCHASE_TIME = 2f;
        private const float ENERGY_UPDATE_INTERVAL = 0.5f;
        private float _lastEnergyUpdateTime = 0f;
        private bool _isAvailable = true;
        private static readonly Dictionary<string, BundleDefinition> BundleDefinitions = new()
        {
            { "Bundle: Trailblazer Snacks", new BundleDefinition([
                ("Granola Bar", 2),
                ("TrailMix", 2)
            ]) },
            { "Bundle: Lovely Bunch", new BundleDefinition("Item_Coconut", 3) },
            { "Bundle: Bear Favorite", new BundleDefinition("Item_Honeycomb", 6) },
            { "Bundle: Rainy Day", new BundleDefinition("Parasol", 4) },
            { "Bundle: Turkey Day", new BundleDefinition("EggTurkey", 3) },
            { "Bundle: For Your Health", new BundleDefinition([
                ("FirstAidKit", 4),
                ("Bandages", 4)
            ]) },
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

            _animator = GetComponentInChildren<Animator>();
            
            if (_animator != null)
            {
                _log?.LogInfo($"[EnergyLinkStore] Found Animator component");
                _animator.enabled = false; // Don't auto-play
            }
            else
            {
                _log?.LogWarning("[EnergyLinkStore] No Animator found!");
            }
                    
            if (_energyLinkService?.IsEnabled() == true)
            {
                _cachedEnergy = _energyLinkService.GetCurrentEnergy();
                _cachedMaxEnergy = _energyLinkService.GetMaxEnergy();
                _log?.LogInfo($"[EnergyLinkStore] Initial energy: {_cachedEnergy}/{_cachedMaxEnergy}");
            }
            UpdateCachedEnergy();
            SelectRandomBundle();
            SetEmissiveTexture(CampfireModelSpawner.redEmissiveTexture);
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
            if (_animator != null)
            {
                _log?.LogInfo($"[EnergyLinkStore] Playing animation");
                _animator.enabled = true;
                _animator.Play("armature|anim_open", 0, 0f);
                
                StartCoroutine(DispenseAfterDelay(bundle, 3f));
            }
            else
            {
                _log?.LogWarning("[EnergyLinkStore] No animator, dispensing immediately");
                StartCoroutine(DispenseAfterDelay(bundle, 0.1f));
            }
        }

        private IEnumerator DispenseAfterDelay(BundleDefinition bundle, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (_animator != null)
            {
                _animator.enabled = false;
            }
            
            Vector3 dispenserOffset = new Vector3(-0.453f, 0.523f, 0.512f);
            Vector3 dispenserPosition = transform.position + transform.TransformDirection(dispenserOffset);
            _log?.LogInfo($"[EnergyLinkStore] Dispensing from position: {dispenserPosition}");

            foreach (var (itemName, count) in bundle.Items)
            {
                SpawnPhysicalItems(itemName, count, dispenserPosition);
            }
        }
        private void Update()
        {
            if (Time.time - _lastEnergyUpdateTime >= ENERGY_UPDATE_INTERVAL)
            {
                UpdateCachedEnergy();
                _lastEnergyUpdateTime = Time.time;
            }
        }

        private void UpdateCachedEnergy()
        {
            if (_energyLinkService?.IsEnabled() == true)
            {
                _cachedEnergy = _energyLinkService.GetCurrentEnergy();
                _cachedMaxEnergy = _energyLinkService.GetMaxEnergy();
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
                        Vector3 force = dispenserForward * UnityEngine.Random.Range(1f, 1.5f) + Vector3.up * 1f;
                        rb.AddForce(force, ForceMode.Impulse);
                    }
                    
                    _log?.LogInfo($"[EnergyLinkStore] Spawned {itemName} at {spawnPosition}");
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
            UpdateCachedEnergy();
            
            if (_cachedEnergy >= BUNDLE_COST)
            {
                return $"{_selectedBundleName}\n{BUNDLE_COST}J";
            }
            else
            {
                return $"NOT ENOUGH ENERGY\n({_cachedEnergy}/{BUNDLE_COST}J)";
            }
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
            // Force immediate update when hovering
            UpdateCachedEnergy();
        }

        public void HoverExit()
        {
        }

        public void Interact(Character interactor)
        {
            _log?.LogInfo($"[EnergyLinkStore] Player {interactor.photonView.Owner.NickName} interacted with Energy Link Store");
        }

        private void SetEmissiveTexture(Texture2D texture)
        {
            if (texture == null) return;
            
            try
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                
                foreach (var renderer in renderers)
                {
                    foreach (var material in renderer.materials)
                    {
                        bool found = false;
                        
                        // Try all common emission texture property names
                        string[] possibleNames = { "_EmissionMap", "_EmissiveMap", "_Emission", "_EmissiveTex", "_Emissive" };
                        
                        foreach (string propName in possibleNames)
                        {
                            if (material.HasProperty(propName))
                            {
                                material.SetTexture(propName, texture);
                                _log?.LogInfo($"[EnergyLinkStore] Set texture on property: {propName}");
                                found = true;
                            }
                        }
                        
                        if (!found)
                        {
                            _log?.LogWarning($"[EnergyLinkStore] No emission property found on material {material.name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[EnergyLinkStore] Error changing emissive texture: {ex.Message}");
            }
        }

        public void Interact_CastFinished(Character interactor)
        {
            _log?.LogInfo($"[EnergyLinkStore] Interact_CastFinished called!");

            if (_energyLinkService?.IsEnabled() != true)
            {
                _log?.LogWarning("[EnergyLinkStore] EnergyLink not enabled");
                return;
            }
            UpdateCachedEnergy();
            if (_cachedEnergy < BUNDLE_COST)
            {
                _log?.LogInfo($"[EnergyLinkStore] Not enough energy! Need {BUNDLE_COST}J, have {_cachedEnergy}J");
                return;
            }

            if (_energyLinkService.ConsumeEnergy(BUNDLE_COST))
            {
                _log?.LogInfo($"[EnergyLinkStore] Purchase successful! Dispensing {_selectedBundleName}");
                
                _isAvailable = false;
                SetEmissiveTexture(CampfireModelSpawner.greenEmissiveTexture);
                
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
        }

        public bool IsInteractible(Character interactor)
        {
            return _isAvailable && _energyLinkService?.IsEnabled() == true;
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