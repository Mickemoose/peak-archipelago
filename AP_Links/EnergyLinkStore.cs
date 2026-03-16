using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using System.Linq;
using System.IO;
using System;
using Photon.Pun;
using System.Collections;
using Photon.Realtime;
using ExitGames.Client.Photon;

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
            PhotonNetwork.NetworkingClient.EventReceived += OnPhotonEvent;
        }
        private static void OnPhotonEvent(ExitGames.Client.Photon.EventData photonEvent)
        {
            if (photonEvent.Code == 117)
            {
                object[] data = (object[])photonEvent.CustomData;
                if (data != null && data.Length > 0 && data[0] is int campfireId)
                {
                    HandleStorePurchase(campfireId);
                }
            }
        }

        public static void HandleStorePurchase(int campfireInstanceId)
        {
            _log?.LogInfo($"[CampfireSpawner] Handling store purchase for campfire {campfireInstanceId}");
            
            foreach (var kvp in spawnedModels)
            {
                if (kvp.Key != null && kvp.Key.GetInstanceID() == campfireInstanceId)
                {
                    var store = kvp.Value?.GetComponent<EnergyLinkStoreInteractable>();
                    if (store != null)
                    {
                        store.MarkAsUsed();
                        _log?.LogInfo($"[CampfireSpawner] Store marked as used");
                    }
                    break;
                }
            }
        }

        public static void SetEnergyLinkService(EnergyLinkService service)
        {
            _energyLinkService = service;
            _log?.LogInfo("[CampfireSpawner] EnergyLinkService reference set");
        }

        private static void LoadModelPrefab()
        {
            // Get the directory where this DLL is located
            string pluginDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string bundlePath = Path.Combine(pluginDirectory, "energylinkstore.peakbundle");

            _log?.LogInfo($"[CampfireSpawner] Looking for AssetBundle at: {bundlePath}");

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
            else
            {
                _log?.LogError($"[CampfireSpawner] AssetBundle not found at: {bundlePath}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Campfire), nameof(Campfire.Awake))]
        static void OnCampfireAwake(Campfire __instance)
        {
            if (_energyLinkService?.IsEnabled() != true)
            {
                _log?.LogDebug("[CampfireSpawner] EnergyLink not enabled, skipping store spawn");
                return;
            }

            SpawnStoreOnCampfire(__instance);
        }

        private static void SpawnStoreOnCampfire(Campfire campfire)
        {
            if (modelPrefab == null)
            {
                _log?.LogWarning("[CampfireSpawner] Model prefab not loaded!");
                return;
            }

            if (spawnedModels.ContainsKey(campfire))
                return;

            GameObject model = UnityEngine.Object.Instantiate(modelPrefab);
            model.SetActive(true);
            model.name = $"APCampfireModel_{campfire.GetInstanceID()}";
            ApplyPeakShaderToModel(model);

            Vector3 campfirePos = campfire.transform.position;
            Vector3 offset = new Vector3(6f, -0.25f, 2f);
            model.transform.position = campfirePos + offset;
            model.transform.LookAt(campfirePos);
            Vector3 currentRotation = model.transform.eulerAngles;
            model.transform.eulerAngles = new Vector3(0f, currentRotation.y, 0f);
            model.transform.localScale = Vector3.one * 2f;
            model.transform.SetParent(campfire.transform, worldPositionStays: true);

            var interactable = model.AddComponent<EnergyLinkStoreInteractable>();
            interactable.Initialize(_log, _energyLinkService, null);

            spawnedModels[campfire] = model;

            _log?.LogInfo($"[CampfireSpawner] Spawned model at {model.transform.position}");
        }

        public static void SpawnOnExistingCampfires()
        {
            if (_energyLinkService?.IsEnabled() != true) return;

            var campfires = UnityEngine.Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None);
            foreach (var campfire in campfires)
            {
                if (campfire != null && campfire.gameObject.activeInHierarchy)
                {
                    SpawnStoreOnCampfire(campfire);
                }
            }
            _log?.LogInfo($"[CampfireSpawner] Checked {campfires.Length} existing campfires for store spawning");
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
            catch (Exception ex)
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
        private string _selectedBundleName;
        private Action _selectedBundleAction;
        private const int BUNDLE_COST = 400000000; // 400 MJ
        private const float PURCHASE_TIME = 2f;
        private const float ENERGY_UPDATE_INTERVAL = 0.5f;
        private float _lastEnergyUpdateTime = 0f;
        private bool _isAvailable = true;
        private Campfire _parentCampfire;
        private static readonly Dictionary<string, BundleDefinition> BundleDefinitions = new()
        {
            /*
            " Alt" at the end of bundle names are hidden from players and logs. The total item cost of bundles cannot exceed half the bundle cost.
            */
            { "Trailblazer Snacks", new BundleDefinition("Granola Bar", 4) },
            { "Trailblazer Snacks Alt", new BundleDefinition([
                ("Granola Bar", 2),
                ("TrailMix", 6)
            ]) },
            { "Lovely Bunch", new BundleDefinition("Item_Coconut", 4) },
            { "Lovely Bunch Alt", new BundleDefinition("Item_Coconut_half", 8) },
            { "Bear Favorite", new BundleDefinition("Item_Honeycomb", 4) },
            { "Bear Favorite Alt", new BundleDefinition("Item_Honeycomb", 6) },
            { "Rainy Day", new BundleDefinition("Parasol", 3) },
            { "Rainy Day Alt", new BundleDefinition("Parasol", 4) },
            { "Turkey Day", new BundleDefinition("EggTurkey", 4) },
            { "Turkey Day Alt", new BundleDefinition([
                ("EggTurkey", 2),
                ("NestEgg", 3)
            ]) },
            { "Special Delivery", new BundleDefinition("Lantern_Faerie", 2) },
            { "Special Delivery Alt", new BundleDefinition("Dynamite", 5) },
            { "For Your Health", new BundleDefinition([
                ("FirstAidKit", 1),
                ("Bandages", 4)
            ]) },
            { "For Your Health Alt", new BundleDefinition("Bandages", 8) },
            { "Rooty Tooty", new BundleDefinition("MedicinalRoot", 4) },
            { "Rooty Tooty Alt", new BundleDefinition([
                ("MedicinalRoot", 3),
                ("Mandrake", 1)
            ]) },
            { "Banana Bonanza", new BundleDefinition([
                ("Berrynana Yellow", 4),
                ("Berrynana Pink", 4),
            ]) },
            { "Banana Bonanza Alt", new BundleDefinition([
                ("Berrynana Blue", 4),
                ("Berrynana Brown", 4),
            ]) },
            { "Cluster Of Berries" , new BundleDefinition([
                ("Apple Berry Green", 1),
                ("Apple Berry Red", 1),
                ("Clusterberry Red", 1),
                ("Clusterberry Yellow", 1),
                ("Kingberry Green", 1),
                ("Kingberry Yellow", 1),
                ("Prickleberry_Red", 1),
                ("Winterberry Orange", 1),
            ]) },
            { "Cluster Of Berries Alt" , new BundleDefinition([
                ("Apple Berry Red", 1),
                ("Apple Berry Yellow", 1),
                ("Clusterberry Black", 1),
                ("Clusterberry Yellow", 1),
                ("Kingberry Green", 1),
                ("Kingberry Purple", 1),
                ("Prickleberry_Gold", 1),
                ("Winterberry Yellow", 1),
            ]) },
            { "Grapple Pack", new BundleDefinition("RopeShooter", 3) },
            { "Grapple Pack Alt", new BundleDefinition([
                ("ChainShooter", 1),
                ("RescueHook", 1)
            ]) },
            { "Mass Production", new BundleDefinition("BingBong", 4) },
            { "Mass Production Alt", new BundleDefinition("BingBong", 8) },
            { "Night Night", new BundleDefinition("HealingDart Variant", 2) },
            { "Night Night Alt", new BundleDefinition("Napberry", 2) },
            { "Sugar Rush", new BundleDefinition([
                ("Lollipop", 1),
                ("Energy Drink", 4)
            ]) },
            { "Sugar Rush Alt", new BundleDefinition([
                ("Lollipop", 2),
                ("Energy Drink", 4)
            ]) }
        };
        
        private class BundleDefinition
        {
            public (string itemName, int count)[] Items { get; }
            public BundleDefinition(string itemName, int count)
            {
                Items = [(itemName, count)];
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
            _parentCampfire = GetComponentInParent<Campfire>();
            if (_parentCampfire == null)
            {
                _log?.LogError("[EnergyLinkStore] No parent Campfire found!");
            }
            
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
                _animator.enabled = false;
            }
            else
            {
                _log?.LogWarning("[EnergyLinkStore] No Animator found!");
            }
                        
            if (_energyLinkService?.IsEnabled() == true)
            {
                _cachedEnergy = _energyLinkService.GetCurrentEnergy();
                _log?.LogInfo($"[EnergyLinkStore] Initial energy: {_cachedEnergy}"); // REMOVED max energy
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

            var bundleDef = selectedBundle.Value;
            _selectedBundleAction = () => DispenseBundle(bundleDef);

            _selectedBundleName = selectedBundle.Key;
            string altBundleName = _selectedBundleName;
            if (_selectedBundleName.EndsWith(" Alt"))
            {
                altBundleName = _selectedBundleName.Substring(
                    0, _selectedBundleName.Length - 4
                );
            }
            _log?.LogInfo($"[EnergyLinkStore] Selected bundle: {altBundleName}");
        }
        private void DispenseBundle(BundleDefinition bundle)
        {
            _log?.LogInfo(
                $"[EnergyLinkStore] Dispensing bundle: {_selectedBundleName}"
            );
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
            
            string altBundleName = _selectedBundleName;
            if (_selectedBundleName.EndsWith(" Alt"))
            {
                altBundleName = _selectedBundleName.Substring(0, _selectedBundleName.Length - 4);
            }
            double cachedMegajoules = ((double) _cachedEnergy) / 1000000f;
            double bundleMegajoules = ((double) BUNDLE_COST) / 1000000f;
            if (_cachedEnergy >= BUNDLE_COST)
            {
                return $"Bundle: {altBundleName}\n{cachedMegajoules:F2} / {bundleMegajoules:F0} MJ";
            }
            else
            {
                return $"Bundle: {altBundleName}\nNOT ENOUGH ENERGY\n({cachedMegajoules:F2} / {bundleMegajoules:F0} MJ)";
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
            
            if (!_isAvailable)
            {
                _log?.LogInfo("[EnergyLinkStore] Store already used");
                return;
            }
            
            UpdateCachedEnergy();
            if (_cachedEnergy < BUNDLE_COST)
            {
                _log?.LogInfo($"[EnergyLinkStore] Not enough energy! Need {BUNDLE_COST}J, have {_cachedEnergy}J");
                return;
            }

            // Send purchase request to host (or process locally if we are host)
            if (PhotonNetwork.IsMasterClient)
            {
                // Host processes directly
                _log?.LogInfo($"[EnergyLinkStore] HOST: Processing purchase");
                ProcessPurchase();
            }
            else
            {
                // Client sends RPC to host
                if (_parentCampfire != null)
                {
                    int campfireId = _parentCampfire.GetInstanceID();
                    _log?.LogInfo($"[EnergyLinkStore] CLIENT: Sending purchase request to host for campfire {campfireId}");
                    
                    // Send to host to process
                    var photonView = PhotonNetwork.LocalPlayer.TagObject as PhotonView;
                    if (photonView == null)
                    {
                        // Try to find the plugin's photon view
                        var plugin = UnityEngine.Object.FindFirstObjectByType<PeakArchipelagoPlugin>();
                        if (plugin != null && plugin.PhotonView != null)
                        {
                            plugin.PhotonView.RPC("RPC_PurchaseEnergyLinkStore", RpcTarget.MasterClient, campfireId);
                        }
                    }
                }
            }
        }
        private void ProcessPurchase()
        {
            if (_energyLinkService.ConsumeEnergy(BUNDLE_COST))
            {
                _log?.LogInfo($"[EnergyLinkStore] Purchase successful!");
                
                // Broadcast to all clients that this store was purchased
                int campfireId = _parentCampfire.GetInstanceID();
                object[] data = [campfireId];
                
                RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                PhotonNetwork.RaiseEvent(117, data, raiseEventOptions, SendOptions.SendReliable);
                
                // Update local cache
                _cachedEnergy = _energyLinkService.GetCurrentEnergy();
            }
            else
            {
                _log?.LogWarning("[EnergyLinkStore] Failed to consume energy for purchase");
            }
        }
        public void MarkAsUsed()
        {
            _isAvailable = false;
            SetEmissiveTexture(CampfireModelSpawner.greenEmissiveTexture);
            _selectedBundleAction?.Invoke();
        }
        
        [PunRPC]
        private void RPC_PurchaseStore()
        {
            _log?.LogInfo("[EnergyLinkStore] RPC received - executing purchase");
            _isAvailable = false;
            SetEmissiveTexture(CampfireModelSpawner.greenEmissiveTexture);
            _selectedBundleAction?.Invoke();
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