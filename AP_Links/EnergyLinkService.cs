using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.DataPackage;
using BepInEx.Logging;
using Newtonsoft.Json.Linq;
using UnityEngine;
using TMPro;
using HarmonyLib;
using Archipelago.MultiClient.Net.Enums;
using Zorro.Core;
using Photon.Pun;
using Archipelago.MultiClient.Net.Models;

namespace Peak.AP
{
    public class EnergyLinkService
    {
        private readonly ManualLogSource _log;
        private ArchipelagoSession _session;
        private bool _isEnabled;
        private string _teamName;
        private IDataStorageHelper _dataStorageHelper;
        private ArchipelagoNotificationManager _notifications;
        private Harmony _harmony;
        private int _currentEnergy = 0;
        private int _maxEnergy = 0;
        private string _energyKey;
        private GameObject _tertiaryPromptObject;
        private TextMeshProUGUI _tertiaryPromptText;
        private float _lastConversionTime = 0f;
        private const float CONVERSION_COOLDOWN = 0.5f;
        private float _conversionProgress = 0f;
        private const float CONVERSION_TIME = 1.5f;
        private Item _convertingItem = null;
        private PeakArchipelagoPlugin _plugin;
        public EnergyLinkService(ManualLogSource log, ArchipelagoNotificationManager notifications)
        {
            _log = log;
            _notifications = notifications;
        }

        /// <summary>
        /// Initialize the EnergyLink service with an Archipelago session
        /// </summary>
        public void Initialize(ArchipelagoSession session, bool enabled, string teamName, PeakArchipelagoPlugin plugin = null)
        {
            _session = session;
            _isEnabled = enabled;
            _teamName = teamName;
            _plugin = plugin;
            
            _energyKey = $"EnergyLink{_teamName}";

            if (_isEnabled)
            {
                try
                {
                    if (_session != null)
                    {
                        _dataStorageHelper = _session.DataStorage;
                        
                        var energyScope = _dataStorageHelper[Scope.Global, _energyKey];
                        energyScope.OnValueChanged += HandleEnergyChanged;
                        energyScope.Initialize(0);
                        
                        _log.LogInfo($"[PeakPelago] EnergyLink service initialized with session for team: {_teamName} (key: {_energyKey})");
                        RefreshEnergyState();
                    }
                    else
                    {
                        _log.LogInfo($"[PeakPelago] EnergyLink enabled for CLIENT - will use RPC to sync");
                    }
                    
                    _harmony = new Harmony("com.mickemoose.peak.ap.energylink");
                    _harmony.PatchAll(typeof(EnergyLinkPatches));
                    EnergyLinkPatches.SetInstance(this);
                }
                catch (Exception ex)
                {
                    _log.LogError($"[PeakPelago] Failed to initialize EnergyLink: {ex.Message}");
                }
            }
        }

        private void HandleEnergyChanged(JToken originalValue, JToken newValue, Dictionary<string, JToken> additionalArguments)
        {
            try
            {
                // EnergyLink is just a number, not an object
                _currentEnergy = newValue.ToObject<int>();
                _log.LogInfo($"[PeakPelago] EnergyLink updated from server: {_currentEnergy}");
                BroadcastEnergyUpdate();
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error handling EnergyLink change: {ex.Message}");
            }
        }

        public bool IsEnabled() => _isEnabled;

        /// <summary>
        /// Enable or disable EnergyLink
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            _log.LogInfo($"[PeakPelago] EnergyLink {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Refresh the current energy state from DataStorage
        /// </summary>
        public void RefreshEnergyState()
        {
            if (_session == null || !_isEnabled) return;

            try
            {
                var energyData = _dataStorageHelper[Scope.Global, _energyKey];
                _currentEnergy = energyData.To<int>();
                _log.LogDebug($"[PeakPelago] EnergyLink state: {_currentEnergy}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to refresh EnergyLink state: {ex.Message}");
            }
        }
        /// <summary>
        /// Contribute energy to the EnergyLink pool
        /// </summary>

        public void ContributeEnergy(int amount)
        {
            if (!_isEnabled || amount <= 0) return;

            _log.LogInfo($"[PeakPelago] ContributeEnergy called: {amount} (HasSession: {_session != null})");

            if (_session == null)
            {
                if (_plugin != null && _plugin.PhotonView != null && PhotonNetwork.IsConnected)
                {
                    _log.LogInfo($"[PeakPelago] CLIENT: Requesting host to contribute {amount} energy");
                    _plugin.PhotonView.RPC("RPC_ContributeEnergy", RpcTarget.MasterClient, amount);
                }
                else
                {
                    _log.LogWarning($"[PeakPelago] CLIENT: Cannot contribute energy - not connected to host");
                }
                return;
            }

            try
            {
                _dataStorageHelper[Scope.Global, _energyKey] += amount;
                _log.LogInfo($"[PeakPelago] HOST: Contributed {amount} energy to EnergyLink");
                _notifications.ShowEnergyLinkNotification($"EnergyLink: Contributed +{amount} energy");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to contribute energy: {ex.Message}");
            }
        }

        public void BroadcastEnergyUpdate()
        {
            if (_plugin != null && _plugin.PhotonView != null && PhotonNetwork.IsConnected)
            {
                _plugin.PhotonView.RPC("RPC_UpdateEnergy", RpcTarget.All, _currentEnergy, 0);
            }
        }


        public void UpdateEnergyCache(int current, int max)
        {
            _currentEnergy = current;
            _log.LogInfo($"[PeakPelago] Updated energy cache: {_currentEnergy}");
        }
        
        private int GetItemEnergyValue(Item item)
        {
            if (item == null) return 0;
            
            string itemName = item.GetName().ToLower();
            if (itemName.Contains("cursed skull") || itemName.Contains("pirate's compass") || itemName.Contains("faerie lantern") || itemName.Contains("pandora")
                || itemName.Contains("scout effigy") || itemName.Contains("cure-all") || itemName.Contains("the book of bones"))
                return 250;
            if (itemName.Contains("ancient idol"))
                return 1000;
            if (itemName.Contains("conch"))
                return 25;
            if (itemName.Contains("bing bong"))
                return 1;
            
            // Default value for misc items
            return 100;
        }

        /// <summary>
        /// Consume energy from the EnergyLink pool
        /// </summary>
        public bool ConsumeEnergy(int amount)
        {
            if (!_isEnabled || amount <= 0) return false;

            _log.LogInfo($"[PeakPelago] ConsumeEnergy called: {amount} (HasSession: {_session != null})");

            if (_session == null)
            {
                _log.LogWarning("[PeakPelago] CLIENT: Cannot consume energy directly - must go through host");
                return false;
            }

            try
            {
                // Check if we have enough (using cached value)
                if (_currentEnergy < amount)
                {
                    _log.LogWarning($"[PeakPelago] Not enough energy to consume {amount} (available: {_currentEnergy})");
                    return false;
                }
                
                var energyOp = _dataStorageHelper[Scope.Global, _energyKey];
                energyOp += -amount;  // Subtract the energy
                energyOp += new OperationSpecification 
                { 
                    OperationType = OperationType.Max, 
                    Value = JToken.FromObject(0) 
                };  // Ensure it doesn't go below 0
                
                // Assign back to trigger the server update
                _dataStorageHelper[Scope.Global, _energyKey] = energyOp;
                
                _log.LogInfo($"[PeakPelago] Consumed {amount} energy from EnergyLink");
                _notifications.ShowEnergyLinkNotification($"EnergyLink: Consumed -{amount} energy");
                
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to consume energy: {ex.Message}");
                return false;
            }
        }
        public int GetCurrentEnergy()
        {
            return _currentEnergy;
        }

        public bool HasEnergy(int amount)
        {
            return _currentEnergy >= amount;
        }
        public int GetMaxEnergy()
        {
            return _maxEnergy;
        }
        /// <summary>
        /// Check if an item can be converted to energy
        /// </summary>
        private bool CanConvertItem(Item item)
        {
            if (item == null) return false;
            
            // Check if it's a special item that shouldn't be converted
            string itemName = item.GetName().ToLower();
            
            // Exclude important progression items
            if (itemName.Contains("passport") || itemName.Contains("rook") || itemName.Contains("basketball") || itemName.Contains("pawn")
                || itemName.Contains("knight") || itemName.Contains("king") || itemName.Contains("queen") || itemName.Contains("bishop"))
            {
                return false;
            }
            
            return true;
        }
        /// <summary>
        /// Create the tertiary prompt UI
        /// </summary>
        private void CreateTertiaryPromptUI(GUIManager guiManager)
        {
            try
            {
                if (_tertiaryPromptObject != null) return;
                
                // Get the parent canvas
                GameObject secondaryPrompt = guiManager.itemPromptSecondary.gameObject;
                if (secondaryPrompt == null)
                {
                    _log.LogError("[PeakPelago] Could not find item prompt secondary to get parent");
                    return;
                }
                
                InputIcon inputIcon = secondaryPrompt.GetComponent<InputIcon>();
                if (inputIcon != null)
                {
                    _log.LogInfo($"[PeakPelago] InputIcon keyboard sprites: {inputIcon.keyboardSprites?.name}");
                }
                
                InputSpriteData inputSpriteData = SingletonAsset<InputSpriteData>.Instance;
                if (inputSpriteData != null)
                {
                    _log.LogInfo($"[PeakPelago] Singleton keyboard sprites: {inputSpriteData.keyboardSprites?.name}");
                }
                
                TextMeshProUGUI secondaryText = secondaryPrompt.GetComponent<TextMeshProUGUI>();
                if (secondaryText != null)
                {
                    _log.LogInfo($"[PeakPelago] Secondary text sprite asset: {secondaryText.spriteAsset?.name}");
                }
                
                // Create a brand new GameObject
                _tertiaryPromptObject = new GameObject("ItemPromptTertiary_EnergyLink");
                _tertiaryPromptObject.transform.SetParent(secondaryPrompt.transform.parent, false);
                
                // Add RectTransform
                RectTransform rectTransform = _tertiaryPromptObject.AddComponent<RectTransform>();
                
                // Copy position from secondary prompt and offset it down
                RectTransform secondaryRect = secondaryPrompt.GetComponent<RectTransform>();
                if (secondaryRect != null)
                {
                    rectTransform.anchorMin = secondaryRect.anchorMin;
                    rectTransform.anchorMax = secondaryRect.anchorMax;
                    rectTransform.pivot = secondaryRect.pivot;
                    rectTransform.sizeDelta = secondaryRect.sizeDelta;
                    Vector2 pos = secondaryRect.anchoredPosition;
                    rectTransform.anchoredPosition = new Vector2(pos.x, pos.y - 40f);
                }
                
                // Add TextMeshProUGUI component
                _tertiaryPromptText = _tertiaryPromptObject.AddComponent<TextMeshProUGUI>();
                
                // Copy styling from secondary prompt
                if (secondaryText != null)
                {
                    _tertiaryPromptText.font = secondaryText.font;
                    _tertiaryPromptText.fontSize = secondaryText.fontSize;
                    _tertiaryPromptText.color = secondaryText.color;
                    _tertiaryPromptText.alignment = secondaryText.alignment;
                    _tertiaryPromptText.fontStyle = secondaryText.fontStyle;
                    
                    // Set the keyboard sprite asset for sprite tags to work
                    if (inputSpriteData != null && inputSpriteData.keyboardSprites != null)
                    {
                        _tertiaryPromptText.spriteAsset = inputSpriteData.keyboardSprites;
                    }
                }
                
                // that sprite is the M keyboard icon sprite
                _tertiaryPromptText.text = "<sprite=22 tint=1> Convert";
                
                _tertiaryPromptObject.SetActive(false);
                _log.LogInfo("[PeakPelago] Created custom tertiary item prompt UI for EnergyLink");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to create tertiary prompt UI: {ex.Message}");
            }
        }
        /// <summary>
        /// Update the tertiary prompt UI
        /// </summary>
        public void UpdateTertiaryPrompt(GUIManager guiManager)
        {
            if (!_isEnabled) return;
            
            try
            {
                // Create UI if it doesn't exist
                if (_tertiaryPromptObject == null)
                {
                    CreateTertiaryPromptUI(guiManager);
                }
                
                // Check if we should show the tertiary prompt
                if (Character.localCharacter != null && Character.localCharacter.data.currentItem != null)
                {
                    Item currentItem = Character.localCharacter.data.currentItem;
                    
                    // Show the tertiary prompt for items that can be converted
                    if (CanConvertItem(currentItem))
                    {
                        _tertiaryPromptObject.SetActive(true);
                        _tertiaryPromptText.text = "Convert <sprite=22 tint=1>";
                    }
                    else
                    {
                        _tertiaryPromptObject.SetActive(false);
                    }
                }
                else
                {
                    if (_tertiaryPromptObject != null)
                    {
                        _tertiaryPromptObject.SetActive(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] UpdateTertiaryPrompt error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle item conversion input
        /// </summary>
        public void HandleItemConversionInput(Item item)
        {
            if (!_isEnabled) return;
            if (item == null) return;
            if (item.itemState != ItemState.Held) return;
            if (item.holderCharacter != Character.localCharacter) return;
            
            var character = item.holderCharacter;
            if (character == null || character.input == null) return;
            
            // Check if this item can be converted
            if (!CanConvertItem(item)) return;
            
            // Check if M key is being held
            if (Input.GetKey(KeyCode.M))
            {
                // Track which item we're converting
                if (_convertingItem != item)
                {
                    _convertingItem = item;
                    _conversionProgress = 0f;
                }
                
                // Increment progress
                _conversionProgress += Time.deltaTime / CONVERSION_TIME;
                
                // Update the item's progress for UI display
                item.overrideProgress = _conversionProgress;
                item.overrideForceProgress = true;
                
                // Once we've held long enough, convert
                if (_conversionProgress >= 1f)
                {
                    _conversionProgress = 0f;
                    _convertingItem = null;
                    
                    string itemName = item.GetName();
                    int energyValue = GetItemEnergyValue(item);
                    
                    _log.LogInfo($"[PeakPelago] Converting {itemName} to {energyValue} energy");
                    
                    ContributeEnergy(energyValue);
                    item.overrideProgress = 0f;
                    item.overrideForceProgress = false;
                    item.StartCoroutine(item.ConsumeDelayed(ignoreActions: true));
                }
            }
            else
            {
                // Reset if they let go of M
                if (_convertingItem == item)
                {
                    item.overrideProgress = 0f;
                    item.overrideForceProgress = false;
                }
                _conversionProgress = 0f;
                _convertingItem = null;
            }
        }

        /// <summary>
        /// Clean up when disconnecting
        /// </summary>
        public void Cleanup()
        {
            if (_tertiaryPromptObject != null)
            {
                UnityEngine.Object.Destroy(_tertiaryPromptObject);
                _tertiaryPromptObject = null;
                _tertiaryPromptText = null;
            }
            
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
            
            EnergyLinkPatches.SetInstance(null);
            
            _session = null;
            _isEnabled = false;
            _currentEnergy = 0;
            _maxEnergy = 0;
        }

        /// <summary>
        /// Harmony patches for EnergyLink functionality
        /// </summary>
        private static class EnergyLinkPatches
        {
            private static EnergyLinkService _instance;

            public static void SetInstance(EnergyLinkService instance)
            {
                _instance = instance;
            }

            [HarmonyPatch(typeof(GUIManager), "UpdateItemPrompts")]
            public static class GUIManagerUpdateItemPromptsPatches
            {
                static void Postfix(GUIManager __instance)
                {
                    try
                    {
                        if (_instance == null) return;
                        _instance.UpdateTertiaryPrompt(__instance);
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError($"[PeakPelago] GUIManager.UpdateItemPrompts patch error: {ex.Message}");
                        }
                    }
                }
            }

            [HarmonyPatch(typeof(Item), "Update")]
            public static class ItemUpdatePatch
            {
                static void Postfix(Item __instance)
                {
                    try
                    {
                        if (_instance == null) return;
                        _instance.HandleItemConversionInput(__instance);
                    }
                    catch (Exception ex)
                    {
                        if (_instance != null)
                        {
                            _instance._log.LogError($"[PeakPelago] Item.Update patch error: {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}