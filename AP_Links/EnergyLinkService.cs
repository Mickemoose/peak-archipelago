using System;
using System.Collections.Generic;
using System.Linq;
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

        public void ContributeEnergy(string itemName, int amount)
        {
            if (!_isEnabled || amount <= 0) return;

            _log.LogInfo($"[PeakPelago] ContributeEnergy called: {itemName}, {amount} (HasSession: {_session != null})");

            if (_session == null)
            {
                if (_plugin != null && _plugin.PhotonView != null && PhotonNetwork.IsConnected)
                {
                    _log.LogInfo($"[PeakPelago] CLIENT: Requesting host to contribute {amount} J from {itemName}");
                    _plugin.PhotonView.RPC("RPC_ContributeEnergy", RpcTarget.MasterClient, itemName, amount);
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
                _log.LogInfo($"[PeakPelago] HOST: Contributed {amount} J from {itemName} to EnergyLink");
                double megajoules = ((double) amount) / 1000000f;
                if (megajoules >= 1) {
                    if (megajoules % 1 == 0) {
                        _notifications.ShowEnergyLinkNotification(
                            $"EnergyLink: Contributed {megajoules:F0} MJ from {itemName}"
                        );
                    }
                    else
                    {
                        _notifications.ShowEnergyLinkNotification(
                            $"EnergyLink: Contributed {megajoules:F2} MJ from {itemName}"
                        );
                    }
                }
                else
                {
                    double kilojoules = ((double) amount) / 1000f;
                    _notifications.ShowEnergyLinkNotification(
                        $"EnergyLink: Contributed {kilojoules:F0} kJ from {itemName}"
                    );
                }
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
            /*
            The below energy values were chosen in an attempt to match the expected energy earned from playing Plants vs. Zombies: Game of the Year Edition in the same multiworld.
            */
            float megajoules = 30f;
            switch (itemName)
            {
                // Packaged foods
                case "pandora's lunchbox":
                    megajoules = 66.6f;
                    break;
                case "airline food":
                    megajoules = 40;
                    break;
                case "big lollipop":
                case "scout cookies":
                    megajoules = 39.95f;
                    break;
                case "granola bar":
                    megajoules = 29.95f;
                    break;
                case string s when s.Contains("drink"):
                case "trail mix":
                    megajoules = 19.95f;
                    break;
                // Berries and mushrooms
                case "napberry":
                    megajoules = 55;
                    break;
                case string s when s.Contains("berrynana peel"):
                    megajoules = 1;
                    break;
                case string s when s.Contains("berry"):
                    megajoules = 9;
                    break;
                case string s when s.Contains("shroom"):
                    megajoules = 10;
                    break;
                // Other naturally-found foods
                case "beehive":
                    megajoules = 60;
                    break;
                case "cooked bird":
                case string s when s.Contains("egg"):
                    megajoules = 40;
                    break;
                case "honeycomb":
                    megajoules = 30;
                    break;
                case "hot dog":
                case "marshmallow":
                    megajoules = 29.95f;
                    break;
                case "half-coconut":
                    megajoules = 3.75f;
                    break;
                case "coconut":
                    megajoules = 7.5f;
                    break;
                // Living food
                case "tick":
                    megajoules = 7.5f;
                    break;
                // Balloon and Balloon Bunch
                case "balloon bunch":
                    megajoules = 15;
                    break;
                case "balloon":
                    megajoules = 5;
                    break;
                // Rope and Scout launchers
                case "anti-rope cannon":
                    megajoules = 77.5f;
                    break;
                case "chain launcher":
                case "rescue claw":
                    megajoules = 75;
                    break;
                case "rope cannon":
                case "scout cannon":
                    megajoules = 60;
                    break;
                // First aid and treatment
                case "faerie lantern":
                case "scout effigy":
                    megajoules = 99.9f;
                    break;
                case "cursed skull":
                case "the book of bones":
                    megajoules = 96;
                    break;
                case "cure-all":
                case "remedy fungus":
                    megajoules = 80;
                    break;
                case "first aid kit":
                    megajoules = 79.95f;
                    break;
                case "antidote":
                case "medicinal root":
                    megajoules = 50;
                    break;
                case "fortified milk":
                    megajoules = 49.95f;
                    break;
                case "blowgun":
                    megajoules = 45;
                    break;
                case "portable stove":
                    megajoules = 44.95f;
                    break;
                case "heat pack":
                    megajoules = 42;
                    break;
                case "aloe vera":
                    megajoules = 30;
                    break;
                case "sunscreen":
                    megajoules = 29.95f;
                    break;
                case "bandages":
                    megajoules = 7.5f;
                    break;
                // Magical fungus (non-remedy)
                case string s when s.Contains("fungus"):
                case "magic bean":
                    megajoules = 40;
                    break;
                // Mystical valuables
                case "ancient idol":
                    megajoules = 300;
                    break;
                case "bugle of friendship":
                case "scoutmaster's bugle":
                    megajoules = 60;
                    break;
                case "strange gem":
                    megajoules = 19;
                    break;
                case "torn page":
                    megajoules = 10;
                    break;
                // Miscellaneous
                case "anti-rope spool":
                case "checkpoint flag":
                    megajoules = 35;
                    break;
                case "parasol":
                case "pirate's compass":
                    megajoules = 33;
                    break;
                case "piton":
                case "rope spool":
                case "torch":
                    megajoules = 25;
                    break;
                case "scroll":
                    megajoules = 8;
                    break;
                case "cactus":
                    megajoules = 1;
                    break;
                case "snowball":
                    megajoules = 0.5f;
                    break;
                // Items found on the Shore, worth less to discourage farming
                case "bugle":
                case "guidebook":
                    megajoules = 2;
                    break;
                case "compass":
                case "flare":
                    megajoules = 1.95f;
                    break;
                case "bing bong":
                    megajoules = 1.88f;
                    break;
                case "binoculars":
                case "lantern":
                    megajoules = 1.5f;
                    break;
                case "flying disc":
                    megajoules = 1;
                    break;
                case "conch":
                    megajoules = 0.2f;
                    break;
            }
            // Convert megajoules to joules (max 2 decimal places)
            return ((int) Mathf.RoundToInt(megajoules * 100.0f)) * 10000;
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
                double megajoules = ((double) amount) / 1000000f;
                List<string> messages = new List<string>
                {
                    "And through the square window is...",
                    "*anxiety-inducing whirring noises*",
                    "Are you feeling lucky?",
                    "Bing Bong approves!",
                    "Door opening, please stand clear.",
                    "For your health!",
                    "Generator online.",
                    "Get a load of this!",
                    "Gotta spend 'em all!",
                    "Here, catch!",
                    "Here's something to toot your bugle about.",
                    "Initiating surprise in 3... 2... 1.",
                    "It ain't Burger King, but it'll do.",
                    "Mhhh, that's the good stuff.",
                    "Now THIS is Pandora's Box.",
                    "Please don't be cursed...",
                    "Prepare to get bonked.",
                    "Some items for your troubles.",
                    "*slurping noises*",
                    "Surprise! We're doing it now!",
                    "This could be quite explosive...",
                    "What could possibly go wrong?",
                    "What's behind door #1?",
                    "WHAT'S IN THE BOX!?!?",
                    "What's the deal with airline food?",
                    "Where are you getting all these batteries?",
                    "You might just get a new check!",
                };
                // Add messages per active, alive character count
                List<Character> validCharacters = Character.AllCharacters.Where(c =>
                    c != null &&
                    c.gameObject.activeInHierarchy &&
                    !c.data.dead &&
                    !c.data.fullyPassedOut
                ).ToList();
                if (validCharacters.Count >= 2)
                {
                    messages.Add("Let's get you all patched up.");
                    messages.Add("Someone's gonna steal it all, I guarantee it.");
                    messages.Add($"Watch out, {validCharacters.Count} Scouts are about!");
                    messages.Add("What are y'all gonna get?");
                    messages.Add("You all planning on sharing?");
                }
                else
                {
                    messages.Add("Been farming, have you?");
                    messages.Add("If it's just you, who put these Stores here?");
                    messages.Add("It's dangerous to go alone! Take this.");
                    messages.Add("Let's get you patched up.");
                    messages.Add("What are you gonna get?");
                }
                _notifications.ShowEnergyLinkNotification($"EnergyLink: Consumed -{megajoules:F0} MJ. {messages[UnityEngine.Random.Range(0, messages.Count)]}");
                
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
            
            // Prevent conversion when in Airport
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene.Contains("Airport")) return false;
            // Check if it's a special item that shouldn't be converted
            string itemName = item.GetName().ToLower();
            
            // Exclude Airport and highly dangerous items
            switch (itemName)
            {
                case "passport":
                case "rook":
                case "basketball":
                case "pawn":
                case "knight":
                case "king":
                case "queen":
                case "bishop":
                case "dynamite":
                case "mandrake":
                case "scorpion":
                    return false;
                default:
                    return true;
            }
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
                    
                    ContributeEnergy(itemName, energyValue);
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