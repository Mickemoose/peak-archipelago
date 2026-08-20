// Archipelago
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static MountainProgressHandler;
using Newtonsoft.Json.Linq;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Zorro.Core;
using static CharacterAfflictions;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine.UI;

namespace Peak.AP
{
    [BepInPlugin("com.mickemoose.peak.ap", "Peak Archipelago", "0.6.1")]
    public class PeakArchipelagoPlugin : BaseUnityPlugin, IInRoomCallbacks
    {
        // ===== BepInEx / logging =====
        public ManualLogSource _log;
        private Harmony _harmony;

        // ===== Config =====
        private ConfigEntry<string> cfgServer;
        private ConfigEntry<int> cfgPort;
        private ConfigEntry<string> cfgSlot;
        private ConfigEntry<string> cfgPassword;
        private ConfigEntry<string> cfgCustomTriviaFolder;
        private ConfigEntry<bool> cfgIncludeStandardTrivia;
        public ConfigEntry<bool> cfgDeathLinkEnabled;



        // ===== Session =====
        public ArchipelagoSession _session;
        public ArchipelagoSession Session => _session;
        private StateManager _stateManager;
        public StateData _stateData;
        public StateData State => _stateData;
        public volatile bool _intentionalDisconnect = false;
        public class MountainHint
        {
            public string Location { get; set; }
            public string Player { get; set; }
            public string Game { get; set; }
            public long LocationId { get; set; }
            public int PlayerSlot { get; set; }
        }
        public string _status = "Disconnected";
        public bool _isConnecting;
        private bool _wantReconnect;
        private string _currentPort = "";
        // ===== Reconnection =====
        private float _reconnectAttemptTime = 0f;
        private const float RECONNECT_DELAY = 5f;
        public int _reconnectAttempts = 0;
        public const int MAX_RECONNECT_ATTEMPTS = 10;
        private readonly HashSet<long> OfflineChecks = [];
        public bool IsReconnecting => _wantReconnect;
        public int OfflineCheckCount => _stateData.OfflineChecks.Count;
        private bool _wasConnected = false;

        // ===== Archipelago Item Receiving Debug =====
        private int _itemsReceivedFromAP = 0;
        private string _lastReceivedItemName = "None";
        private DateTime _lastReceivedItemTime = DateTime.MinValue;
        private Dictionary<string, long> _locationCache = new Dictionary<string, long>();

        // ===== Debug counter for luggage opens =====
        private int _luggageOpenedCount = 0;
        private int _luggageOpenedThisRun = 0;
        private bool _hasOpenedLuggageThisSession = false;
        private ProgressiveStaminaManager _staminaManager;
        public ArchipelagoNotificationManager _notifications;
        private PhotonView _photonView;
        public PhotonView PhotonView => _photonView;
        private const string CHECK_RPC_NAME = "ReceiveCheckFromClient";

        // ===== Badge Management =====
        private HashSet<ACHIEVEMENTTYPE> _originalUnlockedBadges = new HashSet<ACHIEVEMENTTYPE>();
        private bool _badgesHidden = false;
        private bool _hasHiddenBadges = false;
        private bool _hasRebuiltBadges = false;
        private bool _wasInAirport = false;
        private bool _wasInLevel = false;

        // ===== Ascent Management =====
        private int _originalMaxAscent = 0;
        public static bool RunEnded;
        private bool _goalNeedsAscent = true;
        private bool _goalNeedsBadges = false;
        private bool _goalNeedsIdol = false;
        private bool _goalNeedsSoul = false;
        private DamageLinkService _damageLinkService;
        private bool _damageLinkEnabled = false;
        private string _damageLinkGroup = "";
        private KnockbackLinkService _knockbackLinkService;
        private bool _knockbackLinkEnabled = false;
        private string _deathLinkGroup = "";
        private double _lastGroupedDeathLinkSent;
        private int _slotRequiredAscent = 0;
        private int _slotRequiredBadges = 20;
        public bool _itemSanityEnabled = false;
        private int _lootSanityMode = 0; // 0=None, 1=Luggage, 2=Trees/Bushes, 3=All
        public Dictionary<string, int> _lootBiomeAssignments = new Dictionary<string, int>();
        private bool _logicalScoutStatue = false;
        private bool _scoutAmuletSanity = false;
        public int _progressiveMountainCount = 0;
        public static int TrackerVersion = 0;

        public static void BumpTrackerVersion()
        {
            TrackerVersion++;
        }

        // ===== AP Link Management =====
        public RingLinkService _ringLinkService;
        public HardRingLinkService _hardRingLinkService;
        public TrapLinkService _trapLinkService;
        public DeathLinkService _deathLinkService;
        public BreathLinkService _breathLinkService;
        public EnergyLinkService _energyLinkService;
        public bool _ringLinkEnabled = false;
        public bool _hardRingLinkEnabled = false;
        public bool _trapLinkEnabled = false;
        public bool energyLinkEnabled = false;
        public bool _breathLinkEnabled = false;
        public bool _deathLinkEnabled = false;
        private HashSet<string> _enabledTraps = new HashSet<string>();
        private string _energyLinkTeamName = "0";
        private const int BUNDLE_COST = 400000000; // Same as AP_Links/EnergyLinkStore.cs
        private int _deathLinkBehavior = 0;
        private bool _deathLinkReceivedThisSession = false;
        private int _deathLinkSendBehavior = 0;
        private DateTime _lastDeathLinkSent = DateTime.MinValue;
        private DateTime _lastDeathLinkReceived = DateTime.MinValue;
        private string _lastDeathLinkSource = "None";
        private string _lastDeathLinkCause = "None";
        public bool _isDyingFromDeathLink = false;
        public static PeakArchipelagoPlugin _instance { get; private set; }
        public string Status => _status;
        private ArchipelagoUI _ui;
        private ConcurrentQueue<(string itemName, bool isTrap, int itemIndex)> _itemQueue = new();
        private float _lastItemProcessed = 0f;
        private const float ITEM_PROCESSING_COOLDOWN = 1f;

        private float _lastNotificationTime = 0f;
        private const float NOTIFICATION_COOLDOWN = 0.1f;
        private ConcurrentQueue<Action> _mainThreadActions = new();
        private List<MountainHint> _mountainHints = [];

        public Dictionary<string, string> _activeHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public event Action OnHintsChanged;

        public void EnqueueMainThread(Action action)
        {
            if (action != null) _mainThreadActions.Enqueue(action);
        }

        private void Awake()
        {
            try
            {
                _log = Logger;
                _instance = this;
                _stateData = new StateData();
                _stateManager = new StateManager(_log, this);
                _log.LogInfo("[PeakPelago] Plugin is initializing...");
                cfgServer = Config.Bind("Connection", "Server", "archipelago.gg", "Host, or host:port");
                cfgPort = Config.Bind("Connection", "Port", 38281, "Port (ignored if Server already contains :port)");
                cfgSlot = Config.Bind("Connection", "Slot", "Player", "Your AP slot name");
                cfgPassword = Config.Bind("Connection", "Password", "", "Room password (optional)");
                cfgCustomTriviaFolder = Config.Bind("Custom Trivia", "CustomTriviaFolder", "plugins/PeakArchipelago-PEAKPELAGO/CustomTrivia", "Folder path for custom trivia questions (relative to BepInEx folder)");
                cfgIncludeStandardTrivia = Config.Bind("Custom Trivia", "IncludeStandardTrivia", true, "Whether to include the standard trivia questions along with custom ones");
                cfgDeathLinkEnabled = Config.Bind("Archipelago Links", "DeathLinkEnabled", true, "Enable DeathLink (if slot has it enabled)");


                _notifications = new ArchipelagoNotificationManager(_log, cfgSlot.Value);
                _staminaManager = new ProgressiveStaminaManager(_log);
                CharacterGetMaxStaminaPatch.SetStaminaManager(_staminaManager);
                CharacterClampStaminaPatch.SetStaminaManager(_staminaManager);
                CharacterHandleLifePatch.SetStaminaManager(_staminaManager);
                StaminaBarUpdatePatch.SetStaminaManager(_staminaManager);
                CharacterHandlePassedOutPatch.SetStaminaManager(_staminaManager);
                BarAfflictionUpdateAfflictionPatch.SetStaminaManager(_staminaManager);
                CharacterAfflictionsGetStatusCapPatch.SetStaminaManager(_staminaManager);
                CharacterAfflictionsShouldPassOutPatch.SetStaminaManager(_staminaManager);
                CharacterAfflictionsOverflowCapPatch.SetStaminaManager(_staminaManager);
                _ringLinkService = new RingLinkService(_log, _notifications);
                _hardRingLinkService = new HardRingLinkService(_log, _notifications);
                _breathLinkService = new BreathLinkService(_log, _notifications);
                _trapLinkService = new TrapLinkService(_log, _notifications);
                _damageLinkService = new DamageLinkService(_log, _notifications);
                _knockbackLinkService = new KnockbackLinkService(_log, _notifications);
                _energyLinkService = new EnergyLinkService(_log, _notifications);
                CampfireModelSpawner.SetEnergyLinkService(_energyLinkService);
                SwapTrapEffect.Initialize(_log, this);
                AfflictionTrapEffect.Initialize(_log);
                PokemonTriviaTrapEffect.Initialize(_log, this);
                PokemonCountTrapEffect.Initialize(_log, this);
                TriviaUIHelper.Initialize(_log);
                CustomTriviaTrapEffect.Initialize(_log, this);
                DropEverythingTrapEffect.Initialize(_log);
                ZoomTrapEffect.Initialize(_log, this);
                PixelTrapEffect.Initialize(_log, this);
                ScreenFlipTrapEffect.Initialize(_log, this);
                InvertedMouseTrapEffect.Initialize(_log, this);
                StaminaDrainTrapEffect.Initialize(_log);
                ChaosControlTrapEffect.Initialize(_log, this);
                BlackoutTrapEffect.Initialize(_log, this);
                CampfireModelSpawner.Initialize(_log);
                ScoutStatueHonorGatePatch.Log = _log;
                AmuletSpawnerGatePatch.Log = _log;
                FakeAmuletGatePatch.Log = _log;
                FearTrapEffect.Initialize(_log, this);
                EruptionTrapEffect.Initialize(_log);
                _ui = gameObject.AddComponent<ArchipelagoUI>();
                _ui.Initialize(this);
                InitializeItemMapping();
                InitializeItemEffectHandlers();
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedApplyGates;
                GlobalEvents.OnAchievementThrown += OnAchievementThrown;
                GlobalEvents.OnItemRequested += OnItemRequested;
                GlobalEvents.OnSoulFreed += OnSoulFreed;
                _harmony = new Harmony("com.mickemoose.peak.ap");
                _harmony.PatchAll();
                _log.LogInfo("[PeakPelago] Harmony patches applied successfully");
                SetupPhotonView();
                _notifications.SetPhotonView(_photonView);
                // Hide existing badges after a short delay to let the game initialize
                Invoke(nameof(HideExistingBadges), 1f);
                // Store original ascent level
                Invoke(nameof(StoreOriginalAscent), 1f);
                _status = "Ready";
                PhotonNetwork.AddCallbackTarget(this);
                _log.LogInfo("[PeakPelago] Plugin ready.");
                Invoke(nameof(CountExistingBadges), 1.5f);

            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] CRITICAL ERROR during plugin initialization: " + ex.Message);
                _log.LogError("[PeakPelago] Stack trace: " + ex.StackTrace);
            }
        }

        public void ApplyAfflictionViaRPC(int actorNumber, int statusType, float amount)
        {
            if (_photonView != null && PhotonNetwork.IsConnected)
            {
                var targetPlayer = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == actorNumber);

                if (targetPlayer != null)
                {
                    _photonView.RPC("ApplyAfflictionToPlayer", targetPlayer, actorNumber, statusType, amount);
                }
                else
                {
                    _log.LogWarning($"[PeakPelago] Could not find player with actor number {actorNumber}");
                }
            }
        }

        public void IncrementLuggageCount()
        {
            _hasOpenedLuggageThisSession = true;
            _luggageOpenedCount++;
            _luggageOpenedThisRun++;
            _stateData.TotalLuggageOpened++;
            
            CheckLuggageAchievements();
            SaveState();
        }
        private void OnDestroy()
        {
            GlobalEvents.OnAchievementThrown -= OnAchievementThrown;
            GlobalEvents.OnItemRequested -= OnItemRequested;
            GlobalEvents.OnSoulFreed -= OnSoulFreed;
            _ringLinkService?.Cleanup();
            _hardRingLinkService?.Cleanup();
            _breathLinkService?.Cleanup();
            _trapLinkService?.Cleanup();
            _energyLinkService?.Cleanup();
            _harmony?.UnpatchSelf();
            TryCloseSession();
            SaveState();
        }
        private void SetupPhotonView()
        {
            try
            {
                _photonView = GetComponent<PhotonView>();
                if (_photonView == null)
                {
                    _photonView = gameObject.AddComponent<PhotonView>();
                }
                if (_photonView.ViewID == 0)
                {
                    _photonView.ViewID = 999001;
                }

            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to setup PhotonView: " + ex.Message);
                _log.LogWarning("[PeakPelago] Network synchronization may not work properly");
            }
        }

        private void PublishSessionSeedToRoom()
        {
            try
            {
                string seed = CampfireModelSpawner.SessionSeed;
                if (string.IsNullOrEmpty(seed)) return;
                if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

                var room = PhotonNetwork.CurrentRoom;
                if (room.CustomProperties != null
                    && room.CustomProperties.TryGetValue(CampfireModelSpawner.SeedRoomProperty, out object existing)
                    && existing is string existingSeed && existingSeed == seed)
                {
                    return;
                }

                Hashtable props = new Hashtable();
                props[CampfireModelSpawner.SeedRoomProperty] = seed;
                room.SetCustomProperties(props);
                _log.LogInfo("[PeakPelago] Published session seed to room properties");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error publishing session seed: {ex.Message}");
            }
        }

        public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            try
            {
                _log.LogInfo($"[PeakPelago] Player {newPlayer.NickName} joined the room");

                PublishSessionSeedToRoom();
                if (PhotonNetwork.IsMasterClient)
                {
                    FakeAmuletGatePatch.ApplyGates(null);
                }

                // Only the host syncs to new players
                if (PhotonNetwork.IsMasterClient && _photonView != null)
                {
                    // Get the current stamina configuration
                    bool progressiveEnabled = _staminaManager?.IsProgressiveStaminaEnabled() ?? false;
                    int totalUpgrades = _staminaManager?.GetStaminaUpgradesReceived() ?? 0;

                    // Send stamina config
                    _photonView.RPC("SyncStaminaConfiguration", newPlayer, progressiveEnabled, totalUpgrades);

                    _photonView.RPC("SyncProgressiveMountain", newPlayer, _progressiveMountainCount);
                    
                    // Send AP Links config
                    bool energyLinkEnabled = _energyLinkService?.IsEnabled() ?? false;
                    
                    _photonView.RPC("SyncAPLinksConfiguration", newPlayer, 
                        _ringLinkEnabled, 
                        _hardRingLinkEnabled, 
                        _trapLinkEnabled, 
                        energyLinkEnabled, 
                        _energyLinkTeamName,
                        string.Join(",", _enabledTraps));
                    if (_energyLinkService != null && _energyLinkService.IsEnabled())
                    {
                        long currentEnergy = _energyLinkService.GetCurrentEnergy();
                        int maxEnergy = _energyLinkService.GetMaxEnergy();
                        _photonView.RPC("RPC_UpdateEnergy", newPlayer, currentEnergy, maxEnergy);
                    }

                    if (_session != null)
                    {
                        int enduranceCount = _session.Items.AllItemsReceived.Count(item =>
                        {
                            string itemName = _session.Items.GetItemName(item.ItemId, item.ItemGame);
                            return itemName?.Equals("Progressive Endurance", StringComparison.OrdinalIgnoreCase) ?? false;
                        });
                        
                        enduranceCount = Mathf.Min(enduranceCount, 4);
                        
                        if (enduranceCount > 0)
                        {
                            float multiplier = 1.0f - (enduranceCount * 0.1f);
                            _photonView.RPC("SyncEnduranceUpgrade", newPlayer, enduranceCount, multiplier);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in OnPlayerEnteredRoom: {ex.Message}");
            }
        }
        [PunRPC]
        private void SyncProgressiveMountain(int mountainCount)
        {
            try
            {
                _progressiveMountainCount = mountainCount;
                BumpTrackerVersion();
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in SyncProgressiveMountain: {ex.Message}");
            }
        }
        [PunRPC]
        public void StartDropEverythingTrapRPC()
        {
            DropEverythingTrapEffect.ApplyDropEverythingTrapLocal(_log);
        }
        [PunRPC]
        private void StartChaosControlTrapRPC()
        {
            try
            {
                ChaosControlTrapEffect.ApplyChaosControlTrapLocal(_log);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartChaosControlTrapRPC: {ex.Message}");
            }
        }
        [PunRPC]
        private void StartInvertedMouseTrapRPC()
        {
            try
            {
                InvertedMouseTrapEffect.ApplyInvertedMouseTrapLocal(_log);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartInvertedMouseTrapRPC: {ex.Message}");
            }
        }
        [PunRPC]
        private void StartScreenFlipTrapRPC()
        {
            try
            {
                ScreenFlipTrapEffect.ApplyScreenFlipTrapLocal(_log);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartScreenFlipTrapRPC: {ex.Message}");
            }
        }
        [PunRPC]
        private void StartZoomTrapRPC()
        {
            try
            {
                ZoomTrapEffect.ApplyZoomTrapLocal(_log);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartZoomTrapRPC: {ex.Message}");
            }
        }
        [PunRPC]
        private void RPC_PurchaseEnergyLinkStore(int campfireId)
        {
            try
            {
                
                if (_energyLinkService == null || !_energyLinkService.IsEnabled())
                {
                    _log.LogWarning("[PeakPelago] HOST: EnergyLink not enabled");
                    return;
                }
                
                // Check if we have enough energy
                if (_energyLinkService.GetCurrentEnergy() < BUNDLE_COST)
                {
                    _log.LogWarning($"[PeakPelago] HOST: Not enough energy for purchase (have {_energyLinkService.GetCurrentEnergy()}, need {BUNDLE_COST})");
                    return;
                }
                
                // Consume the energy
                if (_energyLinkService.ConsumeEnergy(BUNDLE_COST))
                {
                    
                    // Broadcast to all clients that this store was purchased
                    object[] data = [campfireId];
                    RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                    PhotonNetwork.RaiseEvent(117, data, raiseEventOptions, SendOptions.SendReliable);
                }
                else
                {
                    _log.LogError("[PeakPelago] HOST: Failed to consume energy");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in RPC_PurchaseEnergyLinkStore: {ex.Message}");
                _log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
        [PunRPC]
        private void SyncAPLinksConfiguration(bool ringLink, bool hardRingLink, bool trapLink, bool energyLink, string energyTeam, string enabledTrapsStr)
        {
            try
            {
                
                // Initialize RingLink
                if (ringLink && _ringLinkService != null)
                {
                    _ringLinkService.Initialize(null, true);
                }
                
                // Initialize HardRingLink
                if (hardRingLink && _hardRingLinkService != null)
                {
                    _hardRingLinkService.Initialize(null, true);
                }
                
                // Initialize TrapLink
                if (trapLink && _trapLinkService != null)
                {
                    var enabledTraps = new HashSet<string>(
                        enabledTrapsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                    );
                    
                    _trapLinkService.Initialize(null, true, cfgSlot.Value, enabledTraps, ApplyItemEffect);
                }
                
                // Initialize EnergyLink
                if (energyLink && _energyLinkService != null)
                {
                    _energyLinkService.Initialize(null, true, energyTeam, this);
                    CampfireModelSpawner.SetEnergyLinkService(_energyLinkService);
                    CampfireModelSpawner.SpawnOnExistingCampfires();
                }
                
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] CLIENT: Error in SyncAPLinksConfiguration: {ex.Message}");
                _log.LogError($"[PeakPelago] CLIENT: Stack trace: {ex.StackTrace}");
            }
        }
        [PunRPC]
        private void StartFearTrapRPC()
        {
            try
            {
                
                FearTrapEffect.ApplyFearTrapLocal(_log);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartFearTrapRPC: {ex.Message}");
            }
        }
        [PunRPC]
        private void SyncStaminaUpgrade(int totalUpgrades)
        {
            try
            {
                

                if (_staminaManager == null)
                {
                    _log.LogError("[PeakPelago] StaminaManager is null!");
                    return;
                }
                int currentUpgrades = _staminaManager.GetStaminaUpgradesReceived();
                while (currentUpgrades < totalUpgrades)
                {
                    _staminaManager.ApplyStaminaUpgrade();
                    currentUpgrades++;
                    
                }

                StartCoroutine(ForceStaminaUIUpdate());
                
                
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in SyncStaminaUpgrade: {ex.Message}");
                _log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
        [PunRPC]
        private void SyncSegmentAdvancement(int targetSegment)
        {
            try
            {
                
                
                var mapHandler = Singleton<MapHandler>.Instance;
                if (mapHandler == null)
                {
                    _log.LogError("[PeakPelago] CLIENT: MapHandler is null!");
                    return;
                }
                
                Segment segment = (Segment)targetSegment;
                mapHandler.GoToSegment(segment);
                
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] CLIENT: Error in SyncSegmentAdvancement: {ex.Message}");
                _log.LogError($"[PeakPelago] CLIENT: Stack trace: {ex.StackTrace}");
            }
        }
        [PunRPC]
        private void SyncStaminaConfiguration(bool progressiveEnabled, int totalUpgrades)
        {
            try
            {
                
                
                if (_staminaManager == null)
                {
                    _log.LogError("[PeakPelago] CLIENT: StaminaManager is null!");
                    return;
                }
                
                // Reset stamina manager completely before syncing
                _staminaManager.Initialize(progressiveEnabled, true);
                _staminaManager._localStaminaUpgrades = 0; // Reset upgrades counter
                _staminaManager._localBaseMaxStamina = 0.25f; // Reset to base value
                
                
                
                // Now apply the correct number of upgrades
                for (int i = 0; i < totalUpgrades; i++)
                {
                    _staminaManager.ApplyStaminaUpgrade();
                    
                }
                
                
                StartCoroutine(ForceStaminaUIUpdate());
                
                
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] CLIENT: Error in SyncStaminaConfiguration: {ex.Message}");
                _log.LogError($"[PeakPelago] CLIENT: Stack trace: {ex.StackTrace}");
            }
        }

        [PunRPC]
        private void ShowItemNotificationRPC(string fromName, string toName, string itemName, int classificationInt)
        {
            _notifications?.ReceiveItemNotificationRPC(fromName, toName, itemName, classificationInt);
        }

        [PunRPC]
        private void ShowSimpleMessageRPC(string message)
        {
            _notifications?.ReceiveSimpleMessageRPC(message);
        }

        [PunRPC]
        private void ShowWarningMessageRPC(string message)
        {
            _notifications?.ReceiveWarningMessageRPC(message);
        }

        [PunRPC]
        private void ShowColoredMessageRPC(string message, float r, float g, float b)
        {
            _notifications?.ReceiveColoredMessageRPC(message, r, g, b);
        }

        [PunRPC]
        private void ShowHeroTitleRPC(string message)
        {
            _notifications?.ReceiveHeroTitleRPC(message);
        }

        [PunRPC]
        private void ShowDeathLinkRPC(string cause, string source)
        {
            _notifications?.ReceiveDeathLinkRPC(cause, source);
        }

        [PunRPC]
        private void RPC_SpawnAntiSphere(float x, float y, float z, float lifetime)
        {
            AntiSphereEffect.SpawnAntiSphereAt(_log, new Vector3(x, y, z), lifetime);
        }

        [PunRPC]
        private void RPC_SendRingLink(int amount)
        {
            if (PhotonNetwork.IsMasterClient && _ringLinkService != null)
            {
                _ringLinkService.SendRingLink(amount);
            }
        }

        [PunRPC]
        private void RPC_SendHardRingLink(int amount)
        {
            if (PhotonNetwork.IsMasterClient && _hardRingLinkService != null)
            {
                _hardRingLinkService.SendHardRingLink(amount);
            }
        }

        [PunRPC]
        private void ReceiveCheckFromClient(string locationName, int senderId)
        {
            try
            {
                

                // Only the host processes and reports to Archipelago
                if (PhotonNetwork.IsMasterClient)
                {
                    ReportCheckByName(locationName);

                    // Track badge for goal completion if this check is a badge
                    var badgeMapping = GetBadgeToLocationMapping();
                    foreach (var kvp in badgeMapping)
                    {
                        if (kvp.Value == locationName)
                        {
                            _collectedBadges.Add(kvp.Key);
                            

                            if (_collectedBadges.Count >= _slotRequiredBadges)
                            {
                                
                                ReportCheckByName("All Badges Collected");
                            }
                            if (kvp.Key == ACHIEVEMENTTYPE.TwentyFourKaratBadge)
                            {
                                
                                ReportCheckByName("Idol Dunked");
                            }

                            CheckForGoalCompletion(kvp.Key);
                            break;
                        }
                    }
                }
                else
                {
                    _log.LogWarning("[PeakPelago] Non-host received RPC - this shouldn't happen");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in ReceiveCheckFromClient RPC: {ex.Message}");
            }
        }

        // ===== Death Link Implementation =====

        /// <summary>Send a death link packet to Archipelago when local player dies</summary>
        public void SendDeathLink(string cause = "Unknown")
        {
            if (_deathLinkService == null)
            {
                _log.LogDebug("[PeakPelago] Cannot send death link - not connected or disabled");
                return;
            }

            try
            {
                // Prevent spam - only send one death link per 5 seconds
                if (DateTime.Now - _lastDeathLinkSent < TimeSpan.FromSeconds(5))
                {
                    _log.LogDebug("[PeakPelago] Death link throttled - too recent");
                    return;
                }

                if (string.IsNullOrEmpty(_deathLinkGroup))
                {
                    var deathLink = new DeathLink(cfgSlot.Value, cause);
                    _deathLinkService.SendDeathLink(deathLink);
                }
                else
                {
                    if (_session == null) return;

                    _lastGroupedDeathLinkSent = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                    var data = new Dictionary<string, JToken>
                    {
                        { "time", JToken.FromObject(_lastGroupedDeathLinkSent) },
                        { "source", JToken.FromObject(cfgSlot.Value) },
                        { "cause", JToken.FromObject(cause) }
                    };
                    var bouncePacket = new BouncePacket
                    {
                        Tags = new List<string> { "DeathLink" + _deathLinkGroup },
                        Data = data
                    };
                    _session.Socket.SendPacket(bouncePacket);
                }
                _lastDeathLinkSent = DateTime.Now;
                _notifications.ShowDeathLinkSent("DeathLink Sent!");

            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to send death link: {ex.Message}");
            }
        }

        private void OnGroupedDeathLinkPacket(ArchipelagoPacketBase packet)
        {
            try
            {
                if (!_deathLinkEnabled || string.IsNullOrEmpty(_deathLinkGroup)) return;
                if (!(packet is BouncePacket bounce)) return;
                if (bounce.Tags == null || !bounce.Tags.Contains("DeathLink" + _deathLinkGroup)) return;

                var data = bounce.Data;
                if (data == null) return;

                string source = data.ContainsKey("source") ? data["source"].ToObject<string>() : "Unknown";
                if (source == cfgSlot.Value)
                {
                    _log.LogDebug("[PeakPelago] Ignoring own grouped death link");
                    return;
                }

                if (data.ContainsKey("time"))
                {
                    double time = data["time"].ToObject<double>();
                    if (System.Math.Abs(time - _lastGroupedDeathLinkSent) < 0.0001) return;
                }

                string cause = data.ContainsKey("cause") ? data["cause"].ToObject<string>() : "Death Link";
                _mainThreadActions.Enqueue(() => HandleDeathLinkReceived(cause, source));
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error handling grouped death link: {ex.Message}");
            }
        }

        /// <summary>Handle incoming death link from Archipelago</summary>
        private void HandleDeathLinkReceived(string cause, string source)
        {
            if (_deathLinkService == null || !_deathLinkEnabled || !cfgDeathLinkEnabled.Value)
            {
                _log.LogDebug("[PeakPelago] Death link received but disabled");
                return;
            }

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            if (!currentScene.StartsWith("Level_"))
            {
                _log.LogDebug($"[PeakPelago] Death link blocked - not in Level scene (current: {currentScene})");
                return;
            }
            
            if (Character.localCharacter != null && Character.localCharacter.data.passedOutOnTheBeach > 0f)
            {
                _log.LogDebug("[PeakPelago] Death link blocked - player is passed out on beach");
                return;
            }


            try
            {
                // Prevent spam - only process one death link per 5 seconds
                if (DateTime.Now - _lastDeathLinkReceived < TimeSpan.FromSeconds(5))
                {
                    _log.LogDebug("[PeakPelago] Death link throttled - too recent");
                    return;
                }

                _lastDeathLinkReceived = DateTime.Now;
                _lastDeathLinkSource = source;
                _lastDeathLinkCause = cause;
                _deathLinkReceivedThisSession = true;

                

                // Kill a random player
                KillLocalPlayerFromDeathLink(cause, source);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to handle death link: {ex.Message}");
            }
        }

        /// <summary>Kill a random player (or local player) from death link</summary>
        private void KillLocalPlayerFromDeathLink(string cause, string source)
        {
            try
            {
                Character targetCharacter;

                // Get all valid characters
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    _log.LogWarning("[PeakPelago] Cannot apply death link - no characters found");
                    return;
                }

                // Filter to only active, alive characters
                var validCharacters = Character.AllCharacters.Where(c => 
                    c != null && 
                    c.gameObject.activeInHierarchy && 
                    !c.data.dead &&
                    !c.data.fullyPassedOut
                ).ToList();

                if (validCharacters.Count == 0)
                {
                    _log.LogWarning("[PeakPelago] Cannot apply death link - no valid characters found");
                    return;
                }

                var random = new System.Random();
                targetCharacter = validCharacters[random.Next(validCharacters.Count)];

                if (targetCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Cannot apply death link - target character not found");
                    return;
                }

                string characterName;
                if (targetCharacter == Character.localCharacter)
                {
                    characterName = cfgSlot.Value;
                }
                else
                {
                    characterName = targetCharacter.characterName ?? "Player";
                }

                

                _notifications.ShowDeathLink(cause, source);
                _notifications.ShowHeroTitle("RIP " + characterName.ToUpper());

                if (_deathLinkBehavior == 1)
                {
                    // Use checkpoint flag position if one exists, otherwise fall back to spawn point
                    Vector3 checkpointPos;
                    var localFlags = Character.localCharacter?.data.checkpointFlags;
                    if (localFlags != null && localFlags.Count > 0)
                    {
                        var lastFlag = localFlags[localFlags.Count - 1];
                        checkpointPos = lastFlag.transform.position;
                    }
                    else
                    {
                        checkpointPos = GetLastCheckpointPosition();
                    }
                    
                    foreach (var character in validCharacters)
                    {
                        try
                        {
                            character.WarpPlayerRPC(checkpointPos, true);
                            
                        }
                        catch (Exception ex)
                        {
                            _log.LogError($"[PeakPelago] Failed to warp {character.characterName ?? "player"}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    _isDyingFromDeathLink = true;
                    StartCoroutine(KillCharacterCoroutine(targetCharacter, characterName));
                }

                
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to apply death link: " + ex.Message);
                _log.LogError("[PeakPelago] Stack trace: " + ex.StackTrace);
            }
        }

        private System.Collections.IEnumerator KillCharacterCoroutine(Character targetCharacter, string characterName)
        {
            yield return null;

            try
            {
                
                var dieInstantlyMethod = targetCharacter.GetType().GetMethod("DieInstantly", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (dieInstantlyMethod != null)
                {
                    dieInstantlyMethod.Invoke(targetCharacter, null);
                }
                else
                {
                    _log.LogError("[PeakPelago] Could not find DieInstantly method");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Coroutine death failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _log.LogError($"[PeakPelago] Inner: {ex.InnerException.Message}");
                    _log.LogError($"[PeakPelago] Inner stack: {ex.InnerException.StackTrace}");
                }
            }
            
            // Reset the flag after a short delay to ensure the death event has been processed
            yield return new WaitForSeconds(1f);
            _isDyingFromDeathLink = false;
        }
        /// <summary>Get the position of the last checkpoint/campfire the player visited</summary>
        private Vector3 GetLastCheckpointPosition()
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    return Vector3.zero;
                }

                // Method 1: Use the character's stored spawn point
                if (Character.localCharacter.data.spawnPoint != null)
                {
                    Vector3 spawnPos = Character.localCharacter.data.spawnPoint.position;
                    _log.LogDebug("[PeakPelago] Using character spawn point: " + spawnPos);
                    return spawnPos;
                }

                var mapHandler = Singleton<MapHandler>.Instance;
                if (mapHandler != null && mapHandler.segments != null)
                {
                    int currentSegmentIndex = (int)mapHandler.GetCurrentSegment();
                    if (currentSegmentIndex >= 0 && currentSegmentIndex < mapHandler.segments.Length)
                    {
                        var segment = mapHandler.segments[currentSegmentIndex];
                        if (segment != null)
                        {
                            if (segment.segmentCampfire != null)
                            {
                                Vector3 campfirePos = segment.segmentCampfire.transform.position;
                                _log.LogDebug("[PeakPelago] Using current segment campfire: " + campfirePos);
                                return campfirePos;
                            }
                            if (segment.reconnectSpawnPos != null)
                            {
                                Vector3 reconnectPos = segment.reconnectSpawnPos.transform.position;
                                _log.LogDebug("[PeakPelago] Using reconnect spawn position: " + reconnectPos);
                                return reconnectPos;
                            }
                        }
                    }
                }

                // Fallback: Use the first available spawn point
                if (SpawnPoint.allSpawnPoints != null && SpawnPoint.allSpawnPoints.Count > 0)
                {
                    Vector3 fallbackPos = SpawnPoint.allSpawnPoints[0].transform.position;
                    _log.LogDebug("[PeakPelago] Using fallback spawn point: " + fallbackPos);
                    return fallbackPos;
                }

                _log.LogWarning("[PeakPelago] No checkpoint found, using zero position");
                return Vector3.zero;
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error getting checkpoint position: " + ex.Message);
                return Vector3.zero;
            }
        }

        // ===== Badge Hiding Methods =====

        private void HideExistingBadges()
        {
            try
            {
                if (_hasHiddenBadges)
                {
                    return;
                }

                // Store original badge states
                foreach (ACHIEVEMENTTYPE badgeType in Enum.GetValues(typeof(ACHIEVEMENTTYPE)))
                {
                    if (badgeType != ACHIEVEMENTTYPE.NONE)
                    {
                        // Use reflection to check if badge is unlocked
                        var achievementManager = GetAchievementManager();
                        if (achievementManager != null)
                        {
                            var isUnlockedMethod = achievementManager.GetType().GetMethod("IsAchievementUnlocked");
                            if (isUnlockedMethod != null)
                            {
                                bool isUnlocked = (bool)isUnlockedMethod.Invoke(achievementManager, [badgeType]);
                                if (isUnlocked)
                                {
                                    _originalUnlockedBadges.Add(badgeType);
                                }
                            }
                        }
                    }
                }

                _badgesHidden = true;
                _hasHiddenBadges = true;
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to hide existing badges: " + ex.Message);
            }
        }

        private object GetAchievementManager()
        {
            try
            {
                
                var assembly = typeof(Character).Assembly;
                var singletonType = assembly.GetType("Zorro.Core.Singleton`1");
                
                if (singletonType != null)
                {
                    var achievementManagerType = assembly.GetType("AchievementManager");
                    
                    if (achievementManagerType != null)
                    {
                        var genericType = singletonType.MakeGenericType(achievementManagerType);
                        
                        var instanceProperty = genericType.GetProperty("Instance");
                        
                        if (instanceProperty != null)
                        {
                            var instance = instanceProperty.GetValue(null);
                            return instance;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to get AchievementManager: " + ex.Message);
                _log.LogError("[PeakPelago] Stack trace: " + ex.StackTrace);
            }
            return null;
        }

        // ===== Ascent Management Methods =====

        private void StoreOriginalAscent()
        {
            try
            {
                var achievementManager = GetAchievementManager();
                if (achievementManager != null)
                {
                    var getMaxAscentMethod = achievementManager.GetType().GetMethod("GetMaxAscent");
                    if (getMaxAscentMethod != null)
                    {
                        _originalMaxAscent = (int)getMaxAscentMethod.Invoke(achievementManager, null);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to store original ascent: " + ex.Message);
            }
        }


        // ===== Badge Achievement Mapping =====

        private static Dictionary<ACHIEVEMENTTYPE, string> _badgeToLocationMapping;

        private Dictionary<ACHIEVEMENTTYPE, string> GetBadgeToLocationMapping()
        {
            if (_badgeToLocationMapping != null)
            {
                return _badgeToLocationMapping;
            }

            _badgeToLocationMapping = new Dictionary<ACHIEVEMENTTYPE, string>
            {
                { ACHIEVEMENTTYPE.BeachcomberBadge, "Beachcomber Badge" },
                { ACHIEVEMENTTYPE.TrailblazerBadge, "Trailblazer Badge" },
                { ACHIEVEMENTTYPE.AlpinistBadge, "Alpinist Badge" },
                { ACHIEVEMENTTYPE.VolcanologyBadge, "Volcanology Badge" },
                { ACHIEVEMENTTYPE.CookingBadge, "Cooking Badge" },
                { ACHIEVEMENTTYPE.HappyCamperBadge, "Happy Camper Badge" },
                { ACHIEVEMENTTYPE.BoulderingBadge, "Bouldering Badge" },
                { ACHIEVEMENTTYPE.ToxicologyBadge, "Toxicology Badge" },
                { ACHIEVEMENTTYPE.ForagingBadge, "Foraging Badge" },
                { ACHIEVEMENTTYPE.EsotericaBadge, "Esoterica Badge" },
                { ACHIEVEMENTTYPE.PeakBadge, "Peak Badge" },
                { ACHIEVEMENTTYPE.LoneWolfBadge, "Lone Wolf Badge" },
                { ACHIEVEMENTTYPE.ClutchBadge, "Clutch Badge" },
                { ACHIEVEMENTTYPE.BalloonBadge, "Balloon Badge" },
                { ACHIEVEMENTTYPE.LeaveNoTraceBadge, "Leave No Trace Badge" },
                { ACHIEVEMENTTYPE.SpeedClimberBadge, "Speed Climber Badge" },
                { ACHIEVEMENTTYPE.TriedYourBestBadge, "Participation Badge" },
                { ACHIEVEMENTTYPE.AscenderBadge, "High Altitude Badge" },
                { ACHIEVEMENTTYPE.BingBongBadge, "Bing Bong Badge" },
                { ACHIEVEMENTTYPE.NaturalistBadge, "Naturalist Badge" },
                { ACHIEVEMENTTYPE.GourmandBadge, "Gourmand Badge" },
                { ACHIEVEMENTTYPE.MycologyBadge, "Mycology Badge" },
                { ACHIEVEMENTTYPE.FirstAidBadge, "First Aid Badge" },
                { ACHIEVEMENTTYPE.SurvivalistBadge, "Survivalist Badge" },
                { ACHIEVEMENTTYPE.AnimalSerenadingBadge, "Animal Serenading Badge" },
                { ACHIEVEMENTTYPE.ArboristBadge, "Arborist Badge" },
                { ACHIEVEMENTTYPE.MentorshipBadge, "Mentorship Badge" },
                { ACHIEVEMENTTYPE.KnotTyingBadge, "Knot Tying Badge" },
                { ACHIEVEMENTTYPE.EmergencyPreparednessBadge, "Emergency Preparedness Badge" },
                { ACHIEVEMENTTYPE.PlundererBadge, "Plunderer Badge" },
                { ACHIEVEMENTTYPE.BookwormBadge, "Bookworm Badge" },
                { ACHIEVEMENTTYPE.EnduranceBadge, "Endurance Badge" },
                { ACHIEVEMENTTYPE.ResourcefulnessBadge, "Resourcefulness Badge" },
                { ACHIEVEMENTTYPE.NomadBadge, "Nomad Badge" },
                { ACHIEVEMENTTYPE.UltimateBadge, "Ultimate Badge" },
                { ACHIEVEMENTTYPE.CoolCucumberBadge, "Cool Cucumber Badge" },
                { ACHIEVEMENTTYPE.NeedlepointBadge, "Needlepoint Badge" },
                { ACHIEVEMENTTYPE.AeronauticsBadge, "Aeronautics Badge" },
                { ACHIEVEMENTTYPE.TwentyFourKaratBadge, "24 Karat Badge" },
                { ACHIEVEMENTTYPE.DaredevilBadge, "Daredevil Badge" },
                { ACHIEVEMENTTYPE.MegaentomologyBadge, "Megaentomology Badge" },
                { ACHIEVEMENTTYPE.AstronomyBadge, "Astronomy Badge" },
                { ACHIEVEMENTTYPE.BundledUpBadge, "Bundled Up Badge" },
                { ACHIEVEMENTTYPE.ForestryBadge, "Forestry Badge" },
                { ACHIEVEMENTTYPE.TreadLightlyBadge, "Tread Lightly Badge" },
                { ACHIEVEMENTTYPE.WebSecurityBadge, "Web Security Badge" },
                { ACHIEVEMENTTYPE.UndeadEncounterBadge, "Undead Encounter Badge" },
                { ACHIEVEMENTTYPE.AdvancedMycologyBadge, "Advanced Mycology Badge" },
                { ACHIEVEMENTTYPE.DisasterResponseBadge, "Disaster Response Badge" },
                { ACHIEVEMENTTYPE.CalciumIntakeBadge, "Calcium Intake Badge" },
                { ACHIEVEMENTTYPE.CompetitiveEatingBadge, "Competitive Eating Badge" },
                { ACHIEVEMENTTYPE.AppliedEsotericaBadge, "Applied Esoterica Badge" },
                { ACHIEVEMENTTYPE.MycoacrobaticsBadge, "Mycoacrobatics Badge" },
                { ACHIEVEMENTTYPE.CryptogastronomyBadge, "Cryptogastronomy Badge" },
                { ACHIEVEMENTTYPE.WandererBadge, "Wanderer Badge" },
            };

            return _badgeToLocationMapping;
        }

        // ===== Luggage Achievement Checking =====

        private void CheckLuggageAchievements()
        {
            if (_session == null || !_hasOpenedLuggageThisSession) return;

            try
            {
                int[] totalThresholds = { 1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100 };
                int[] singleRunThresholds = { 2, 5, 10, 15, 20 };

                foreach (int threshold in totalThresholds)
                    CheckAndReportAchievement($"Open {threshold} luggage", threshold, _stateData.TotalLuggageOpened);

                foreach (int threshold in singleRunThresholds)
                    CheckAndReportAchievement($"Open {threshold} luggage in a single run", threshold, _luggageOpenedThisRun);
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] CheckLuggageAchievements error: " + ex.Message);
            }
        }

        private void CheckAndReportAchievement(string achievementName, int requiredCount, int currentCount)
        {
            if (currentCount >= requiredCount)
            {
                ReportCheckByName(achievementName);
            }
        }

        public void OnBasketballScored(int totalBaskets)
        {
            if (_session == null) return;

            try
            {
                _stateData.TotalBasketsScored = totalBaskets;
                
                CheckAndReportAchievement("Score 1 basket", 1, _stateData.TotalBasketsScored);
                
                int[] thresholds = { 3, 5, 7, 10 };
                foreach (int threshold in thresholds)
                {
                    CheckAndReportAchievement($"Score {threshold} baskets", threshold, _stateData.TotalBasketsScored);
                }
                
                SaveState();
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] OnBasketballScored error: " + ex.Message);
            }
        }

        // ===== Public helpers you can call from game hooks =====

        /// <summary>Report a check by its AP location name (defined in the apworld).</summary>
        public void ReportCheckByName(string locationName)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    if (_photonView != null && PhotonNetwork.IsConnected)
                    {
                        _photonView.RPC(CHECK_RPC_NAME, RpcTarget.MasterClient, locationName, PhotonNetwork.LocalPlayer.ActorNumber);
                    }
                    else
                    {
                        _log.LogWarning($"[PeakPelago] Cannot send check '{locationName}' - not connected to Photon");
                    }
                    return;
                }

                // Host processing
                try
                {
                    long id = -1;
                    
                    // Try to get location ID
                    if (_session != null && _session.Locations != null)
                    {
                        try
                        {
                            id = _session.Locations.GetLocationIdFromName("PEAK", locationName);
                            // Cache it for offline use
                            _locationCache[locationName] = id;
                        }
                        catch (Exception ex)
                        {
                            _log.LogWarning($"[PeakPelago] Could not get location ID for '{locationName}': {ex.Message}");
                        }
                    }
                    
                    // If we couldn't get it from session, try cache
                    if (id <= 0 && _locationCache.ContainsKey(locationName))
                    {
                        id = _locationCache[locationName];
                    }

                    if (id <= 0)
                    {
                        _log.LogWarning($"[PeakPelago] Location '{locationName}' not found in session or cache (id={id})");
                        return;
                    }

                    // Check if already reported
                    if (_stateData.ReportedChecks.Contains(id))
                    {
                        _log.LogDebug($"[PeakPelago] Check already reported: {locationName} (ID: {id})");
                        return;
                    }

                    // If connected, report immediately
                    if (_session != null && _session.Socket != null && _session.Socket.Connected)
                    {
                        try
                        {
                            _session.Locations.CompleteLocationChecks([id]);
                            _stateData.ReportedChecks.Add(id);
                            
                            BroadcastCheckCompleted(locationName, id);
                            _stateManager.StateDirty = true;
                        }
                        catch (Exception ex)
                        {
                            _log.LogWarning($"[PeakPelago] Failed to report check '{locationName}', queueing for reconnect: {ex.Message}");
                            _stateData.OfflineChecks.Add(id);
                            SaveOfflineChecks();
                        }
                    }
                    else
                    {
                        // Not connected - queue for later
                        _stateData.OfflineChecks.Add(id);
                        SaveOfflineChecks();
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError("[PeakPelago] ReportCheckByName failed: " + ex.Message);
                    _log.LogError("[PeakPelago] Stack trace: " + ex.StackTrace);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in ReportCheckByName wrapper: {ex.Message}");
                _log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
        /// <summary>Broadcast to all clients that a check was completed</summary>
        private void BroadcastCheckCompleted(string locationName, long locationId)
        {
            try
            {
                if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("OnCheckCompletedRPC", RpcTarget.All, locationName, locationId);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to broadcast check completion: {ex.Message}");
            }
        }
        [PunRPC]
        private void OnCheckCompletedRPC(string locationName, long locationId)
        {
            try
            {
                // All clients add this to their reported checks to avoid duplicate reports
                _stateData.ReportedChecks.Add(locationId);
                BumpTrackerVersion();
                
                // Update local state
                SaveState();
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in OnCheckCompletedRPC: {ex.Message}");
            }
        }
        /// <summary>
        /// Check if the "Reach Peak" goal has been met based on current ascent level.
        /// </summary>
        /// <param name="currentAscent"></param>
        private void CheckReachPeakGoal(int currentAscent)
        {
            if (_session == null) return;

            if (currentAscent >= _slotRequiredAscent)
            {
                if (!_stateData.GoalAscentReached)
                {
                    _stateData.GoalAscentReached = true;
                    SaveState();
                }
                ReportCheckByName($"Ascent {currentAscent} Completed");
            }

            EvaluateGoalCompletion();
        }

        private bool GoalNeedsAscent() => _goalNeedsAscent;
        private bool GoalNeedsBadges() => _goalNeedsBadges;

        private void EvaluateGoalCompletion()
        {
            if (_session == null) return;

            bool needIdol = _goalNeedsIdol;
            bool needSoul = _goalNeedsSoul;
            bool needAscent = GoalNeedsAscent();
            bool needBadges = GoalNeedsBadges();

            if (!needIdol && !needSoul && !needAscent && !needBadges) return;

            bool ascentDone = _stateData.GoalAscentReached ||
                _awardedAscentBadges.Contains($"Ascent {_slotRequiredAscent} Completed_{_slotRequiredAscent}");

            if (needAscent && !ascentDone) return;
            if (needBadges && _collectedBadges.Count < _slotRequiredBadges) return;
            if (needIdol && !_stateData.GoalIdolDunked) return;
            if (needSoul && !_stateData.GoalSoulFreed) return;

            _log.LogInfo("[PeakPelago] All goal requirements met - sending goal completion");
            SendGoalComplete();
        }

        /// <summary>
        /// Check for goal completion based on the badges.
        /// </summary>
        /// <param name="achievementType"></param>
        private void CheckForGoalCompletion(ACHIEVEMENTTYPE achievementType)
        {
            if (_session == null) return;

            if (achievementType == ACHIEVEMENTTYPE.TwentyFourKaratBadge && !_stateData.GoalIdolDunked)
            {
                _stateData.GoalIdolDunked = true;
                SaveState();
            }

            EvaluateGoalCompletion();
        }
        
        /// <summary>
        /// Count existing badges.
        /// </summary>
        private void CountExistingBadges()
        {
            try
            {
                var badgeMapping = GetBadgeToLocationMapping();
                var achievementManager = GetAchievementManager();
                
                if (achievementManager != null)
                {
                    var isUnlockedMethod = achievementManager.GetType().GetMethod("IsAchievementUnlocked");
                    if (isUnlockedMethod != null)
                    {
                        foreach (var kvp in badgeMapping)
                        {
                            bool isUnlocked = (bool)isUnlockedMethod.Invoke(achievementManager, new object[] { kvp.Key });
                            if (isUnlocked)
                            {
                                _collectedBadges.Add(kvp.Key);
                            }
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error counting existing badges: " + ex.Message);
            }
        }


        /// <summary>Mark goal complete (use when the PEAK victory condition is reached).</summary>
        public void SendGoalComplete()
        {
            if (_session == null)
            {
                _log.LogWarning("[PeakPelago] Not connected; cannot send goal.");
                return;
            }
            try
            {
                _session.SetGoalAchieved();
                
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Goal send failed: " + ex.Message);
            }
        }

        // ===== Item Acquisition Tracking =====

        //private Dictionary<string, int> _itemAcquisitionCounts = new Dictionary<string, int>();
        //private Dictionary<string, int> _itemAcquisitionCountsThisRun = new Dictionary<string, int>();

        // Track most recently acquired item
        private string _lastAcquiredItemName = "None";
        private ushort _lastAcquiredItemId = 0;
        private float _lastAcquiredItemTime = 0f;

        // Mapping from in-game item names to Archipelago location names
        private Dictionary<ushort, string> _itemIdToLocationMapping = [];

        // Track which ascent badges have been awarded to avoid duplicates
        private HashSet<string> _awardedAscentBadges = [];
        // Track which badges have been collected to avoid duplicates
        private HashSet<ACHIEVEMENTTYPE> _collectedBadges = [];

        // Item effect handlers for Archipelago items
        private Dictionary<string, Action> _itemEffectHandlers = [];

        private void InitializeItemMapping()
        {
            // First, let's log all item IDs so we can map them correctly
            //
            //for (ushort itemID = 0; itemID < 300; itemID++)
            //{
             //   if (ItemDatabase.TryGetItem(itemID, out Item item))
            //    {
             //       _log.LogInfo($"[PeakPelago] Item ID {itemID}: {item.name}");
             //   }
            //}
            
            _itemIdToLocationMapping = new Dictionary<ushort, string>
            {
                // Rope items
                { 65, "Acquire Rope Spool" },
                { 63, "Acquire Rope Cannon" },
                { 1, "Acquire Anti-Rope Spool" },
                { 64, "Acquire Anti-Rope Cannon" },
                { 17, "Acquire Chain Launcher" },
                { 18, "Acquire Piton" },
                { 100, "Acquire Rescue Claw" },
                
                { 45, "Acquire Magic Bean" },
                { 98, "Acquire Parasol" },
                { 164, "Acquire Parasol" },
                { 105, "Acquire Balloon" },
                { 37, "Acquire Balloon Bunch" },
                { 107, "Acquire Scout Cannon" },
                { 99, "Acquire Flying Disc" },
                { 34, "Acquire Guidebook" },
                
                { 62, "Acquire Portable Stove" },
                { 28, "Acquire FireWood" },
                { 42, "Acquire Lantern" },
                { 32, "Acquire Flare" },
                { 109, "Acquire Torch" },
                { 43, "Acquire Faerie Lantern" },
                { 70, "Acquire Blowgun" },
                
                { 48, "Acquire Cactus" },
                { 23, "Acquire Compass" },
                { 61, "Acquire Pirate's Compass" },
                { 14, "Acquire Binoculars" },
                { 7, "Acquire Bandages" },
                { 29, "Acquire First-Aid Kit" },
                { 2, "Acquire Antidote" },
                { 35, "Acquire Heat Pack" },
                { 24, "Acquire Cure-All" },
                { 90, "Acquire Remedy Fungus" },
                { 81, "Acquire Medicinal Root" },
                { 101, "Acquire Aloe Vera" },
                { 104, "Acquire Sunscreen" },
                { 46, "Acquire Marshmallow" },
                { 154, "Acquire Glizzy" },
                { 152, "Acquire Fortified Milk" },
                
                // Special objects
                { 67, "Acquire Scout Effigy" },
                { 25, "Acquire Cursed Skull" },
                { 58, "Acquire Pandora's Lunchbox" },
                { 47, "Acquire Ancient Idol" },
                { 112, "Acquire Strange Gem" },
                { 115, "Acquire Book of Bones" },
                { 30, "Acquire Checkpoint Flag" },
                { 117, "Acquire Cloud Fungus" },
                
                // Musical items
                { 16, "Acquire Bugle of Friendship" },
                { 15, "Acquire Bugle" },
                
                // Mushrooms
                { 68, "Acquire Shelf Shroom" },
                { 79, "Acquire Bounce Shroom" },
                { 102, "Acquire Button Shroom" },
                { 93, "Acquire Bugle Shroom" },
                { 88, "Acquire Cluster Shroom" },
                { 83, "Acquire Chubby Shroom" },
                
                // Food items
                { 73, "Acquire Trail Mix" },
                { 33, "Acquire Granola Bar" },
                { 66, "Acquire Scout Cookies" },
                { 0, "Acquire Airline Food" },
                { 27, "Acquire Energy Drink" },
                { 71, "Acquire Sports Drink" },
                { 44, "Acquire Big Lollipop" },
                { 57, "Acquire Big Egg" },
                { 26, "Acquire Egg" },
                { 114, "Acquire Cooked Bird" },
                { 38, "Acquire Honeycomb" },
                { 8, "Acquire Beehive" },
                { 111, "Acquire Scorpion" },
                { 95, "Acquire Tick" },
                
                // Miscellaneous items
                { 69, "Acquire Conch" },
                { 39, "Acquire Pink Berrynana Peel" },
                { 91, "Acquire Blue Berrynana Peel" },
                { 92, "Acquire Brown Berrynana Peel" },
                { 94, "Acquire Yellow Berrynana Peel" },
                { 106, "Acquire Dynamite" },
                { 13, "Acquire Bing Bong" },
                { 155, "Acquire Mandrake" },
                
                // Berries
                { 4, "Acquire Red Crispberry" },
                { 3, "Acquire Green Crispberry" },
                { 5, "Acquire Yellow Crispberry" },
                { 36, "Acquire Coconut" },
                { 55, "Acquire Coconut Half" },
                { 10, "Acquire Brown Berrynana" },
                { 9, "Acquire Blue Berrynana" },
                { 11, "Acquire Pink Berrynana" },
                { 12, "Acquire Yellow Berrynana" },
                { 75, "Acquire Orange Winterberry" },
                { 76, "Acquire Yellow Winterberry" },
                { 108, "Acquire Red Prickleberry" },
                { 103, "Acquire Gold Prickleberry" },

                { 161, "Acquire Red Shroomberry" },
                { 158, "Acquire Blue Shroomberry" },
                { 159, "Acquire Green Shroomberry" },
                { 162, "Acquire Yellow Shroomberry" },
                { 160, "Acquire Purple Shroomberry" },

                { 40, "Acquire Purple Kingberry" },
                { 41, "Acquire Yellow Kingberry" },
                { 56, "Acquire Green Kingberry" },

                { 110, "Acquire Napberry" },

                { 19, "Acquire Black Clusterberry"},
                { 20, "Acquire Red Clusterberry" },
                { 21, "Acquire Yellow Clusterberry" },

                { 77, "Acquire Scoutmaster's Bugle"},
                { 60, "Acquire Scorchberry"},


                //GLOOM & CITADEL UPDATE
                { 192, "Acquire Anti-Zooka"},
                { 184, "Acquire The Early Worm"},
                { 181, "Acquire Warp Fungus"},
                { 174, "Acquire Glider"},
                { 173, "Acquire Ritual Dagger"},
                { 171, "Acquire Candlestick"},
                { 172, "Acquire Frog"},
                { 169, "Acquire Rocketpack"},
                { 168, "Acquire Jetpack"},
                { 166, "Acquire Fanny Pack"},
                { 185, "Acquire Frog Legs"},

                { 182, "Acquire Scout's Tenacity"},
                { 180, "Acquire Scout's Generosity"},
                { 179, "Acquire Scout's Ambition"},
                { 170, "Acquire Scout's Initiative"},

                { 183, "Acquire Scout's Honor"},
                


            };
        }

        public Dictionary<ushort, string> GetItemIdToLocationMapping()
        {
            return _itemIdToLocationMapping;
        }

        public string GetLocationNameById(long locationId)
        {
            foreach (var kvp in _locationCache)
            {
                if (kvp.Value == locationId)
                    return kvp.Key;
            }
            return null;
        }

        private void InitializeItemEffectHandlers()
        {
            _itemEffectHandlers = new Dictionary<string, Action>
            {
                { "Rope Spool", () => SpawnPhysicalItem("RopeSpool") },
                { "Rope Cannon", () => SpawnPhysicalItem("RopeShooter") },
                { "Anti-Rope Spool", () => SpawnPhysicalItem("Anti-Rope Spool") },
                { "Anti-Rope Cannon", () => SpawnPhysicalItem("RopeShooterAnti") },
                { "Chain Launcher", () => SpawnPhysicalItem("ChainShooter") },
                { "Piton", () => SpawnPhysicalItem("ClimbingSpike") },
                { "Magic Bean", () => SpawnPhysicalItem("MagicBean") },
                { "Parasol", () => SpawnPhysicalItem("Parasol") },
                { "Balloon", () => SpawnPhysicalItem("Balloon") },
                { "Balloon Bunch", () => SpawnPhysicalItem("BalloonBunch") },
                { "Scout Cannon", () => SpawnPhysicalItem("ScoutCannonItem") },
                { "Portable Stove", () => SpawnPhysicalItem("PortableStovetopItem") },
                { "Checkpoint Flag", () => SpawnPhysicalItem("Flag_Plantable_Checkpoint") },
                { "Lantern", () => SpawnPhysicalItem("Lantern") },
                { "Flare", () => SpawnPhysicalItem("Flare") },
                { "Torch", () => SpawnPhysicalItem("Torch") },
                { "Cactus", () => SpawnPhysicalItem("CactusBall") },
                { "Compass", () => SpawnPhysicalItem("Compass") },
                { "Mandrake", () => SpawnPhysicalItem("Mandrake") },
                { "Pirate's Compass", () => SpawnPhysicalItem("Pirate Compass") },
                { "Pirates Compass", () => SpawnPhysicalItem("Pirate Compass") },
                { "Pirate Compass", () => SpawnPhysicalItem("Pirate Compass") },
                { "Tick", () => SpawnPhysicalItem("Bugfix") },
                { "Hidden Mandrake", () => SpawnPhysicalItem("Mandrake_Hidden") },
                { "Binoculars", () => SpawnPhysicalItem("Binoculars") },
                { "Flying Disc", () => SpawnPhysicalItem("Frisbee") },
                { "Bandages", () => SpawnPhysicalItem("Bandages") },
                { "First-Aid Kit", () => SpawnPhysicalItem("FirstAidKit") },
                { "Antidote", () => SpawnPhysicalItem("Antidote") },
                { "Heat Pack", () => SpawnPhysicalItem("Heat Pack") },
                { "Cure-All", () => SpawnPhysicalItem("Cure-All") },
                { "Faerie Lantern", () => SpawnPhysicalItem("Lantern_Faerie") },
                { "Medicinal Root", () => SpawnPhysicalItem("MedicinalRoot") },
                { "Guidebook", () => SpawnPhysicalItem("Guidebook") },
                { "Aloe Vera", () => SpawnPhysicalItem("AloeVera") },
                { "Blowgun", () => SpawnPhysicalItem("HealingDart Variant") },
                { "Marshmallow", () => SpawnPhysicalItem("Marshmallow") },
                { "Glizzy", () => SpawnPhysicalItem("Glizzy") },
                { "Fortified Milk", () => SpawnPhysicalItem("FortifiedMilk") },
                { "Rescue Claw", () => SpawnPhysicalItem("RescueHook") },
                { "Book of Bones", () => SpawnPhysicalItem("BookOfBones") },
                { "Sunscreen", () => SpawnPhysicalItem("Sunscreen") },
                { "Scout Effigy", () => SpawnPhysicalItem("ScoutEffigy") },
                { "Cursed Skull", () => SpawnPhysicalItem("Cursed Skull") },
                { "Pandora's Lunchbox", () => SpawnPhysicalItem("PandorasBox") },
                { "Ancient Idol", () => SpawnPhysicalItem("AncientIdol") },
                { "Strange Gem", () => SpawnPhysicalItem("Strange Gem") },
                { "Scorpion", () => SpawnPhysicalItem("Scorpion") },
                { "Beehive", () => SpawnPhysicalItem("Beehive") },
                { "Honeycomb", () => SpawnPhysicalItem("Item_Honeycomb") },
                { "Big Egg", () => SpawnPhysicalItem("NestEgg") },
                { "Egg", () => SpawnPhysicalItem("Egg") },
                { "Cooked Bird", () => SpawnPhysicalItem("EggTurkey") },
                { "Bugle of Friendship", () => SpawnPhysicalItem("Bugle_Magic") },
                { "Bugle", () => SpawnPhysicalItem("Bugle") },
                { "Remedy Fungus", () => SpawnPhysicalItem("HealingPuffShroom") },
                { "Shelf Shroom", () => SpawnPhysicalItem("ShelfShroom") },
                { "Bounce Shroom", () => SpawnPhysicalItem("BounceShroom") },
                { "Trail Mix", () => SpawnPhysicalItem("TrailMix") },
                { "Granola Bar", () => SpawnPhysicalItem("Granola Bar") },
                { "Scout Cookies", () => SpawnPhysicalItem("ScoutCookies") },
                { "Airline Food", () => SpawnPhysicalItem("Airplane Food") },
                { "Energy Drink", () => SpawnPhysicalItem("Energy Drink") },
                { "Sports Drink", () => SpawnPhysicalItem("Sports Drink") },
                { "Big Lollipop", () => SpawnPhysicalItem("Lollipop") },
                { "Button Shroom", () => SpawnPhysicalItem("Mushroom Normie") },
                { "Bugle Shroom", () => SpawnPhysicalItem("Mushroom Lace") },
                { "Cluster Shroom", () => SpawnPhysicalItem("Mushroom Cluster") },
                { "Chubby Shroom", () => SpawnPhysicalItem("Mushroom Chubby") },
                { "Conch", () => SpawnPhysicalItem("Shell Big") },
                { "Banana Peel", () => SpawnPhysicalItem("Berrynana Peel Yellow") },
                { "Dynamite", () => SpawnPhysicalItem("Dynamite") },
                { "Bing Bong", () => SpawnPhysicalItem("BingBong") },
                { "Red Crispberry", () => SpawnPhysicalItem("Apple Berry Red") },
                { "Green Crispberry", () => SpawnPhysicalItem("Apple Berry Green") },
                { "Yellow Crispberry", () => SpawnPhysicalItem("Apple Berry Yellow") },
                { "Coconut", () => SpawnPhysicalItem("Item_Coconut") },
                { "Coconut Half", () => SpawnPhysicalItem("Item_Coconut_half") },
                { "Brown Berrynana", () => SpawnPhysicalItem("Berrynana Brown") },
                { "Blue Berrynana", () => SpawnPhysicalItem("Berrynana Blue") },
                { "Pink Berrynana", () => SpawnPhysicalItem("Berrynana Pink") },
                { "Yellow Berrynana", () => SpawnPhysicalItem("Berrynana Yellow") },
                { "Orange Winterberry", () => SpawnPhysicalItem("Winterberry Orange") },
                { "Yellow Winterberry", () => SpawnPhysicalItem("Winterberry Yellow") },
                { "Red Prickleberry", () => SpawnPhysicalItem("Prickleberry_Red") },
                { "Gold Prickleberry", () => SpawnPhysicalItem("Prickleberry_Gold") },
                { "Red Shroomberry", () => SpawnPhysicalItem("Shroomberry_Red") },
                { "Green Shroomberry", () => SpawnPhysicalItem("Shroomberry_Green") },
                { "Blue Shroomberry", () => SpawnPhysicalItem("Shroomberry_Blue") },
                { "Yellow Shroomberry", () => SpawnPhysicalItem("Shroomberry_Yellow") },
                { "Purple Shroomberry", () => SpawnPhysicalItem("Shroomberry_Purple") },
                { "Purple Kingberry", () => SpawnPhysicalItem("Kingberry Purple") },
                { "Yellow Kingberry", () => SpawnPhysicalItem("Kingberry Yellow") },
                { "Green Kingberry", () => SpawnPhysicalItem("Kingberry Green") },
                { "Napberry", () => SpawnPhysicalItem("Napberry") },
                { "Black Clusterberry", () => SpawnPhysicalItem("Clusterberry Black") },
                { "Red Clusterberry", () => SpawnPhysicalItem("Clusterberry Red") },
                { "Yellow Clusterberry", () => SpawnPhysicalItem("Clusterberry Yellow") },
                { "Scoutmaster's Bugle", () => SpawnPhysicalItem("Bugle_Scoutmaster Variant") },

                // Progression Items (76019-76025) - Unlock ascents
                { "Progressive Ascent", () => UnlockAscent() },
                { "Progressive Endurance", () => ApplyProgressiveEndurance() },
                { "Progressive Mountain", () => ApplyProgressiveMountain() },
                //{ "Ascent 2 Unlock", () => UnlockAscent(2) },
                //{ "Ascent 3 Unlock", () => UnlockAscent(3) },
                //{ "Ascent 4 Unlock", () => UnlockAscent(4) },
                //{ "Ascent 5 Unlock", () => UnlockAscent(5) },
                //{ "Ascent 6 Unlock", () => UnlockAscent(6) },
                //{ "Ascent 7 Unlock", () => UnlockAscent(7) },
                { "Progressive Stamina Bar", () => ApplyProgressiveStamina() },

                // Filler Items with effects
                { "Morale Boost", () => MoraleBoost.SpawnMoraleBoost(
                    Character.localCharacter != null ? Character.localCharacter.Center : Vector3.zero,
                    -1f, 0.50f, 0f, sendToAll: true) },
                { "Anti-Sphere", () => AntiSphereEffect.SpawnAntiSphereOnPlayer(_log, _photonView) },

                // Trap Items
                { "Spawn Bee Swarm", () => BeeSwarmTrapEffect.ApplyBeeSwarmTrap(_log) },
                { "Destroy Held Item", () => DestroyHeldItem() },
                { "Blue Berrynana Peel", () => SpawnPhysicalItem("Berrynana Peel Blue Variant") },
                { "Banana Peel Trap", () => SpawnPhysicalItem("Berrynana Peel Yellow") },
                { "Minor Poison Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.25f, STATUSTYPE.Poison) },
                { "Poison Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.53f, STATUSTYPE.Poison) },
                { "Deadly Poison Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.95f, STATUSTYPE.Poison) },
                { "Tornado Trap", () => TornadoTrapEffect.SpawnTornadoOnPlayer(_log) },
                { "Pokemon Trivia Trap", () => PokemonTriviaTrapEffect.ApplyPokemonTriviaTrap(_log) },
                { "Custom Trivia Trap", () => CustomTriviaTrapEffect.ApplyCustomTriviaTrap(_log) },
                { "Pokemon Count Trap", () => PokemonCountTrapEffect.ApplyPokemonCountTrap(_log) },
                { "Drop Everything Trap", () => DropEverythingTrapEffect.ApplyDropEverythingTrap(_log) },
                { "Swap Trap", () => SwapTrapEffect.ApplyPositionSwapTrap(_log) },
                { "Nap Time Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 1.0f, STATUSTYPE.Drowsy) },
                { "Hungry Hungry Camper Trap", () => HungryHungryCamperTrapEffect.ApplyHungerTrap(_log) },
                { "Balloon Trap", () => BalloonTrapEffect.ApplyBalloonTrap(_log) },
                { "Slip Trap", () => SlipTrapEffect.ApplySlipTrap(_log) },
                { "Clear All Effects", () => ClearAllEffects() },
                { "Speed Upgrade", () => ApplySpeedUpgrade() },
                { "Cactus Ball Trap", () =>ItemToWhateverTrapEffect.ApplyItemToWhateverTrap(_log, "CactusBall") },
                { "Freeze Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 1.0f, STATUSTYPE.Cold) },
                { "Cold Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.5f, STATUSTYPE.Cold) },
                { "Hot Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.5f, STATUSTYPE.Hot) },
                { "Injury Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.5f, STATUSTYPE.Injury) },
                { "Bounce Fungus", () => SpawnPhysicalItem("BounceShroom") },
                { "Cloud Fungus", () => SpawnPhysicalItem("CloudFungus") },
                { "Instant Death Trap", () => InstantDeathTrapEffect.ApplyInstantDeathTrap(_log) },
                { "Yeet Trap", () => YeetItemTrapEffect.ApplyYeetTrap(_log)},
                { "Tumbleweed Trap", () => TumbleweedTrapEffect.ApplyTumbleweedTrap(_log) },
                { "Items to Bombs", () => ItemToBombTrapEffect.ApplyItemToBombTrap(_log) },
                { "Zombie Horde Trap", () => ZombieHordeTrapEffect.ApplyZombieHordeTrap(_log) },
                { "Beetle Horde Trap", () => BeetleHordeTrapEffect.ApplyBeetleHordeTrap(_log) },
                { "Frog Trap", () => FrogTrapEffect.ApplyFrogTrap(_log) },
                { "Zoom Trap", () => ZoomTrapEffect.ApplyZoomTrap(_log) },
                { "Pixel Trap", () => PixelTrapEffect.ApplyPixelTrap(_log) },
                { "Screen Flip Trap", () => ScreenFlipTrapEffect.ApplyScreenFlipTrap(_log) },
                { "Gust Trap", () => GustTrapEffect.ApplyGustTrap(_log) },
                { "Mandrake Trap", () => ItemToWhateverTrapEffect.ApplyItemToWhateverTrap(_log, "Mandrake") },
                { "Blackout Trap", () => BlackoutTrapEffect.ApplyBlackoutTrap(_log) },
                { "Eruption Trap", () => EruptionTrapEffect.ApplyEruptionTrap(_log) },
                { "Fungal Infection Trap", () => StatusOverTimeTrapEffect.ApplyStatusOverTime(_log, StatusOverTimeTrapEffect.TargetMode.RandomPlayer,
                STATUSTYPE.Spores,
                amountPerTick: 0.1f,
                tickInterval: 1.0f,
                duration: 5.0f
                ) },
                { "Turn To Stone Trap", () => StatusOverTimeTrapEffect.ApplyStatusOverTime(_log, StatusOverTimeTrapEffect.TargetMode.RandomPlayer,
                STATUSTYPE.Petrify,
                amountPerTick: 0.1f,
                tickInterval: 1.0f,
                duration: 10.0f
                ) },
                { "Fear Trap", () => FearTrapEffect.ApplyFearTrap(_log) },
                { "Scoutmaster Trap", () => ScoutmasterTrapEffect.TriggerScoutmasterTrap(_log)},
                { "Inverted Mouse Trap", () => InvertedMouseTrapEffect.ApplyInvertedMouseTrap(_log)},
                { "Stamina Drain Trap", () => StaminaDrainTrapEffect.ApplyStaminaDrainTrap(_log)},
                { "Chaos Control Trap", () => ChaosControlTrapEffect.ApplyChaosControlTrap(_log)},
                { "Emergency Rescue Trap", () => EmergencyRescueTrapEffect.ApplyEmergencyRescueTrap(_log)},
                { "Explosion Trap", () => ExplosionTrapEffect.ApplyExplosionTrap(_log)}

            };

        }
        public string GetCustomTriviaFolder()
        {
            return cfgCustomTriviaFolder.Value;
        }

        public bool GetIncludeStandardTrivia()
        {
            return cfgIncludeStandardTrivia.Value;
        }
        [PunRPC]
        public void SpawnEruptionTrapRPC(Vector3 position)
        {
            
            EruptionTrapEffect.SpawnEruptionLocal(position, _log);
        }
        [PunRPC]
        private void RPC_ContributeEnergy(string itemName, int amount)
        {
            try
            {
                
                
                if (_energyLinkService != null && _energyLinkService.IsEnabled())
                {
                    _energyLinkService.ContributeEnergy(itemName, amount);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in RPC_ContributeEnergy: {ex.Message}");
            }
        }

        [PunRPC]
        private void RPC_UpdateEnergy(long current, int max)
        {
            try
            {
                
                
                if (_energyLinkService != null)
                {
                    _energyLinkService.UpdateEnergyCache(current, max);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in RPC_UpdateEnergy: {ex.Message}");
            }
        }
        [PunRPC]
        private void StartBlackoutTrapRPC()
        {
            try
            {
                
                BlackoutTrapEffect.ApplyBlackoutTrapLocal(_log);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartBlackoutTrapRPC: {ex.Message}");
            }
        }

        [PunRPC]
        private void BreathLinkDepleteStaminaRPC()
        {
            _breathLinkService?.DepleteLocalStamina();
        }

        [PunRPC]
        private void StartPokemonTriviaRPC()
        {
            PokemonTriviaTrapEffect.ApplyPokemonTriviaTrapLocal(_log);
        }

        [PunRPC]
        private void StartPokemonCountRPC()
        {
            PokemonCountTrapEffect.ApplyPokemonCountTrapLocal(_log);
        }

        [PunRPC]
        private void StartCustomTriviaRPC()
        {
            CustomTriviaTrapEffect.ApplyCustomTriviaTrapLocal(_log);
        }

        [PunRPC]
        public void StartGustTrapRPC()
        {

            GustTrapEffect.ActivateGustLocal(_log);
        }

        [PunRPC]
        public void StartEmergencyRescueRPC(Vector3 position)
        {
            try
            {
                if (Zorro.Core.Singleton<PeakHandler>.Instance != null && !Zorro.Core.Singleton<PeakHandler>.Instance.summonedHelicopter)
                {
                    _log.LogInfo("[PeakPelago] Emergency Rescue RPC - summoning helicopter!");
                    EmergencyRescueTrapEffect.RepositionAndSummon(position);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartEmergencyRescueRPC: {ex.Message}");
            }
        }

        [PunRPC]
        public void StartExplosionTrapRPC(int targetActorNumber)
        {
            try
            {
                var players = PlayerHandler.GetAllPlayerCharacters();
                Character target = null;
                foreach (var p in players)
                {
                    if (p.photonView.Owner.ActorNumber == targetActorNumber)
                    {
                        target = p;
                        break;
                    }
                }

                if (target == null)
                {
                    _log.LogWarning($"[PeakPelago] Explosion Trap RPC - could not find player with actor number {targetActorNumber}");
                    return;
                }

                _log.LogInfo($"[PeakPelago] Explosion Trap RPC - spawning explosion on {target.photonView.Owner.NickName}!");
                ExplosionTrapEffect.SpawnExplosionAt(target.Center, _log);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartExplosionTrapRPC: {ex.Message}");
            }
        }

        [PunRPC]
        private void StartStaminaDrainTrapRPC(int targetActorNumber)
        {
            try
            {
                

                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Local character is null!");
                    return;
                }

                if (Character.localCharacter.photonView.Owner.ActorNumber != targetActorNumber)
                {
                    _log.LogDebug($"[PeakPelago] Stamina Drain RPC not for us (we are {Character.localCharacter.photonView.Owner.ActorNumber}, target is {targetActorNumber})");
                    return;
                }

                
                StartCoroutine(StaminaDrainTrapEffect.StaminaDrainCoroutine(Character.localCharacter, _log));
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartStaminaDrainTrapRPC: {ex.Message}");
                _log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        [PunRPC]
        private void StartDOTTrapRPC(int targetActorNumber, int statusType, float amountPerTick, float tickInterval, float duration)
        {
            try
            {
                

                // Only apply if this is OUR character
                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Local character is null!");
                    return;
                }

                if (Character.localCharacter.photonView.Owner.ActorNumber != targetActorNumber)
                {
                    _log.LogDebug($"[PeakPelago] DOT RPC not for us (we are {Character.localCharacter.photonView.Owner.ActorNumber}, target is {targetActorNumber})");
                    return;
                }

                // Start the DOT coroutine on our local character
                
                StartCoroutine(StatusOverTimeTrapEffect.ApplyStatusOverTimeCoroutine(
                    Character.localCharacter,
                    (CharacterAfflictions.STATUSTYPE)statusType,
                    amountPerTick,
                    tickInterval,
                    duration,
                    _log
                ));
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartDOTTrapRPC: {ex.Message}");
                _log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        [PunRPC]
        private void ApplyHungerTrapRPC(int targetActorNumber, float amount)
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Local character is null!");
                    return;
                }

                if (Character.localCharacter.photonView.Owner.ActorNumber != targetActorNumber)
                {
                    return;
                }

                StartCoroutine(HungryHungryCamperTrapEffect.ApplyStatusNextFrame(
                    Character.localCharacter,
                    CharacterAfflictions.STATUSTYPE.Hunger,
                    amount,
                    _log
                ));
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in ApplyHungerTrapRPC: {ex.Message}");
            }
        }

        [PunRPC]
        private void InstantDeathTrapRPC(int targetActorNumber)
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Local character is null!");
                    return;
                }

                if (Character.localCharacter.photonView.Owner.ActorNumber != targetActorNumber)
                {
                    return;
                }

                StartCoroutine(InstantDeathTrapEffect.KillCharacterNextFrame(Character.localCharacter, "local player", _log));
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in InstantDeathTrapRPC: {ex.Message}");
            }
        }

        [PunRPC]
        private void SwapTrapWarpRPC(int targetActorNumber, float posX, float posY, float posZ)
        {
            try
            {
                

                // Only apply if this is OUR character
                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Local character is null!");
                    return;
                }

                if (Character.localCharacter.photonView.Owner.ActorNumber != targetActorNumber)
                {
                    _log.LogDebug($"[PeakPelago] Swap RPC not for us (we are {Character.localCharacter.photonView.Owner.ActorNumber}, target is {targetActorNumber})");
                    return;
                }

                // Warp our local character to the target position
                Vector3 targetPosition = new Vector3(posX, posY, posZ);
                
                
                Character.localCharacter.WarpPlayerRPC(targetPosition, true);
                
                
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in SwapTrapWarpRPC: {ex.Message}");
                _log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        [PunRPC]
        public void NotifyDamageLinkInjury(float amount, string characterName)
        {
            try
            {
                if (!_damageLinkEnabled) return;

                if (_session != null)
                {
                    _damageLinkService?.ReportLocalDamage(amount, characterName);
                }
                else if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("RPC_DamageLinkTaken", RpcTarget.MasterClient, amount, characterName);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error notifying Damage Link injury: {ex.Message}");
            }
        }

        [PunRPC]
        private void RPC_DamageLinkTaken(float amount, string characterName)
        {
            _damageLinkService?.ReportLocalDamage(amount, characterName);
        }

        public void NotifyKnockbackLink(Vector3 force, string characterName)
        {
            try
            {
                if (!_knockbackLinkEnabled) return;

                if (_session != null)
                {
                    _knockbackLinkService?.ReportLocalKnockback(force, characterName);
                }
                else if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("RPC_KnockbackLinkTaken", RpcTarget.MasterClient, force, characterName);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error notifying Knockback Link: {ex.Message}");
            }
        }

        [PunRPC]
        private void RPC_KnockbackLinkTaken(Vector3 force, string characterName)
        {
            _knockbackLinkService?.ReportLocalKnockback(force, characterName);
        }

        private static bool _eruptionHarvestAttempted;

        private void OnSceneLoadedApplyGates(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            try
            {
                StartCoroutine(ApplyFakeAmuletGatesDelayed());

                if (scene.name == "Title" && !_eruptionHarvestAttempted &&
                    !EruptionTrapEffect.HasCachedPrefab && !PhotonNetwork.InRoom)
                {
                    _eruptionHarvestAttempted = true;
                    StartCoroutine(HarvestEruptionPrefab());
                }

                if (scene.name == "WilIsland" && mode == UnityEngine.SceneManagement.LoadSceneMode.Additive)
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var cam in root.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
                        foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                        foreach (var audio in root.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error scheduling scene-load work: {ex.Message}");
            }
        }

        private System.Collections.IEnumerator HarvestEruptionPrefab()
        {
            _log.LogInfo("[PeakPelago] Harvesting eruption prefab from WilIsland at title screen");
            var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("WilIsland", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (op == null)
            {
                _log.LogError("[PeakPelago] Could not begin loading WilIsland for eruption harvest");
                yield break;
            }
            while (!op.isDone) yield return null;

            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("WilIsland");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                _log.LogError("[PeakPelago] WilIsland did not load for eruption harvest");
                yield break;
            }

            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    var spawners = root.GetComponentsInChildren<EruptionSpawner>(true);
                    foreach (var spawner in spawners)
                    {
                        if (spawner != null && spawner.eruption != null)
                        {
                            EruptionTrapEffect.SetCachedPrefab(spawner.eruption);
                            break;
                        }
                    }
                    if (EruptionTrapEffect.HasCachedPrefab) break;
                }
                _log.LogInfo($"[PeakPelago] Eruption harvest result: {(EruptionTrapEffect.HasCachedPrefab ? "prefab captured" : "no EruptionSpawner found in WilIsland")}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Eruption harvest error: {ex.Message}");
            }

            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            _log.LogInfo("[PeakPelago] Eruption harvest scene unloaded");
        }

        private System.Collections.IEnumerator ApplyFakeAmuletGatesDelayed()
        {
            yield return new WaitForSeconds(2f);
            if (PhotonNetwork.IsMasterClient)
            {
                FakeAmuletGatePatch.ApplyGates(null);
            }
        }

        public void BroadcastFakeAmuletStates(int[] hide, int[] show)
        {
            if (_photonView != null && PhotonNetwork.IsConnected)
            {
                _photonView.RPC("RPC_ApplyFakeAmuletStates", RpcTarget.All, hide, show);
            }
            else
            {
                RPC_ApplyFakeAmuletStates(hide, show);
            }
        }

        [PunRPC]
        private void RPC_ApplyFakeAmuletStates(int[] hide, int[] show)
        {
            try
            {
                FakeAmuletGatePatch.ApplyLocalStates(hide, show);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error applying fake amulet states: {ex.Message}");
            }
        }

        public void OnRunEndedInNadir()
        {
            try
            {
                _log.LogInfo("[PeakPelago] Nadir ending reached - Free The Soul goal step complete");
                if (!_stateData.GoalSoulFreed)
                {
                    _stateData.GoalSoulFreed = true;
                    SaveState();
                }
                EvaluateGoalCompletion();
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error handling Nadir ending: {ex.Message}");
            }
        }

        public void BroadcastKnockbackLink(Vector3 force, string source)
        {
            if (_photonView != null && PhotonNetwork.IsConnected)
            {
                _photonView.RPC("RPC_ApplyKnockbackLink", RpcTarget.All, force, source);
            }
            else
            {
                RPC_ApplyKnockbackLink(force, source);
            }
        }

        [PunRPC]
        private void RPC_ApplyKnockbackLink(Vector3 force, string source)
        {
            try
            {
                var character = Character.localCharacter;
                if (character == null || character.refs?.ragdoll == null) return;
                if (character.data.dead || character.data.fullyPassedOut) return;

                KnockbackLinkPatch.Suppress = true;
                try
                {
                    character.Fall(0.4f);
                    character.AddForce(force, 0.7f, 1.3f);
                }
                finally
                {
                    KnockbackLinkPatch.Suppress = false;
                }

                _log.LogInfo($"[PeakPelago] Applied Knockback Link ({force.magnitude:F1}) from {source}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error applying Knockback Link: {ex.Message}");
            }
        }

        public void BroadcastDamageLinkDamage(float amount, string source)
        {
            if (_photonView != null && PhotonNetwork.IsConnected)
            {
                _photonView.RPC("RPC_ApplyDamageLinkDamage", RpcTarget.All, amount, source);
            }
            else
            {
                RPC_ApplyDamageLinkDamage(amount, source);
            }
        }

        [PunRPC]
        private void RPC_ApplyDamageLinkDamage(float amount, string source)
        {
            try
            {
                var character = Character.localCharacter;
                if (character == null || character.refs?.afflictions == null) return;
                if (character.data.dead) return;

                DamageLinkInjuryPatch.Suppress = true;
                try
                {
                    character.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, amount);
                }
                finally
                {
                    DamageLinkInjuryPatch.Suppress = false;
                }

                _log.LogInfo($"[PeakPelago] Applied Damage Link injury ({amount:F2}) from {source}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error applying Damage Link damage: {ex.Message}");
            }
        }

        private void ApplyAfflictionToPlayer(int targetActorNumber, int statusType, float amount)
        {
            try
            {
                

                // Find our local character
                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Local character is null!");
                    return;
                }

                // Check if this RPC is for US
                int myActorNumber = Character.localCharacter.photonView.Owner.ActorNumber;
                
                if (myActorNumber != targetActorNumber)
                {
                    _log.LogDebug($"[PeakPelago] RPC not for us (we are {myActorNumber}, target is {targetActorNumber})");
                    return;
                }

                // Apply the affliction to our local character
                
                Character.localCharacter.refs.afflictions.AddStatus((CharacterAfflictions.STATUSTYPE)statusType, amount);
                
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in ApplyAfflictionToPlayer RPC: {ex.Message}");
                _log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
                
        private void OnPlayerJoined()
        {
            if (PhotonNetwork.IsMasterClient && _photonView != null && PhotonNetwork.IsConnected)
            {
                int totalUpgrades = _staminaManager?.GetStaminaUpgradesReceived() ?? 0;
                if (totalUpgrades > 0)
                {
                    _photonView.RPC("SyncStaminaUpgrade", RpcTarget.Others, totalUpgrades);
                    
                }
            }
        }
        private void RecoverEnduranceUpgrades()
        {
            try
            {
                if (_session == null) return;
                
                int enduranceCount = _session.Items.AllItemsReceived.Count(item =>
                {
                    string itemName = _session.Items.GetItemName(item.ItemId, item.ItemGame);
                    return itemName?.Equals("Progressive Endurance", StringComparison.OrdinalIgnoreCase) ?? false;
                });
                
                enduranceCount = Mathf.Min(enduranceCount, 4);
                
                if (enduranceCount > 0)
                {
                    float multiplier = 1.0f - (enduranceCount * 0.1f);
                    SetStaminaUsageMultiplier(multiplier);
                    
                    
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error recovering endurance upgrades: {ex.Message}");
            }
        }

        private void ApplyProgressiveMountain()
        {
            try
            {
                if (_session == null) return;
                
                // Count how many Progressive Mountain items we've received
                int mountainCount = _session.Items.AllItemsReceived.Count(item =>
                {
                    string itemName = _session.Items.GetItemName(item.ItemId, item.ItemGame);
                    return itemName?.Equals("Progressive Mountain", StringComparison.OrdinalIgnoreCase) ?? false;
                });
                
                mountainCount = Mathf.Min(mountainCount, 4); // Cap at 4
                _progressiveMountainCount = mountainCount;
                BumpTrackerVersion();
                
                
                
                _notifications.ShowSimpleMessage($"Mountain Progress {mountainCount}/4!");
                UnlockedItemsManager.UpdateMountainCount(_progressiveMountainCount);

                if (_photonView != null && PhotonNetwork.IsMasterClient)
                {
                    _photonView.RPC("SyncProgressiveMountain", RpcTarget.Others, _progressiveMountainCount);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error applying Progressive Mountain: {ex.Message}");
            }
        }

        private void RecoverProgressiveMountain()
        {
            try
            {
                if (_session == null) return;
                
                int mountainCount = _session.Items.AllItemsReceived.Count(item =>
                {
                    string itemName = _session.Items.GetItemName(item.ItemId, item.ItemGame);
                    return itemName?.Equals("Progressive Mountain", StringComparison.OrdinalIgnoreCase) ?? false;
                });
                
                mountainCount = Mathf.Min(mountainCount, 4);
                _progressiveMountainCount = mountainCount;
                BumpTrackerVersion();
                
                
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error recovering Progressive Mountain: {ex.Message}");
            }
        }

        private void ApplyProgressiveEndurance()
        {
            try
            {
                if (_session == null) return;
                
                // Count how many Progressive Endurance items we've received
                int enduranceCount = _session.Items.AllItemsReceived.Count(item =>
                {
                    string itemName = _session.Items.GetItemName(item.ItemId, item.ItemGame);
                    return itemName?.Equals("Progressive Endurance", StringComparison.OrdinalIgnoreCase) ?? false;
                });
                
                // Cap upgrades
                enduranceCount = Mathf.Min(enduranceCount, 8);
                
                
                
                // Calculate stamina usage multiplier
                // member that each upgrade reduces usage by 10%: 100% -> 90% -> 80% -> 70% -> 60%
                float staminaUsageMultiplier = 1.0f - (enduranceCount * 0.1f);
                
                // Broadcast to all clients
                if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("SyncEnduranceUpgrade", RpcTarget.All, enduranceCount, staminaUsageMultiplier);
                }
                else
                {
                    SetStaminaUsageMultiplier(staminaUsageMultiplier);
                }
                
                _notifications.ShowSimpleMessage($"Endurance Upgrade {enduranceCount}! Stamina usage: {staminaUsageMultiplier * 100:F0}%");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error applying Progressive Endurance: {ex.Message}");
            }
        }
        [PunRPC]
        public void StartPixelTrapRPC()
        {
            PixelTrapEffect.ApplyPixelTrapLocal(_log);
        }

        [PunRPC]
        private void SyncEnduranceUpgrade(int totalUpgrades, float multiplier)
        {
            try
            {
                
                SetStaminaUsageMultiplier(multiplier);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in SyncEnduranceUpgrade: {ex.Message}");
            }
        }

        private void SetStaminaUsageMultiplier(float multiplier)
        {
            try
            {
                // Store in Photon custom properties
                if (PhotonNetwork.LocalPlayer != null)
                {
                    Hashtable props = new Hashtable();
                    props["AP_EnduranceMultiplier"] = multiplier;
                    PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                    
                    
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error setting stamina usage multiplier: {ex.Message}");
            }
        }
        private void ApplyProgressiveStamina()
        {
            try
            {
                if (_staminaManager == null)
                {
                    _log.LogError("[PeakPelago] Stamina manager is NULL!");
                    return;
                }

                if (!_staminaManager.IsProgressiveStaminaEnabled())
                {
                    _log.LogWarning("[PeakPelago] Progressive stamina is DISABLED");
                    return;
                }

                int staminaItems = 0;
                if (_session != null && _session.Items != null)
                {
                    staminaItems = _session.Items.AllItemsReceived.Count(item =>
                    {
                        string itemName = _session.Items.GetItemName(item.ItemId, item.ItemGame);
                        return itemName?.Equals("Progressive Stamina Bar", StringComparison.OrdinalIgnoreCase) ?? false;
                    });
                    
                    
                }

                float targetStamina = 0.25f + (staminaItems * 0.25f);
                float currentStamina = _staminaManager.GetBaseMaxStamina();
                
                if (Mathf.Abs(targetStamina - currentStamina) < 0.01f)
                {
                    
                    return;
                }

                

                // UPDATE LOCAL STATE FIRST (so UI reads correct value)
                _staminaManager._localBaseMaxStamina = targetStamina;
                _staminaManager._localStaminaUpgrades = staminaItems;

                // Then set Photon
                if (PhotonNetwork.LocalPlayer != null)
                {
                    Hashtable props = new Hashtable();
                    props["AP_Stamina"] = targetStamina;
                    PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                    
                    
                }

                // Broadcast to other players
                if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("SyncStaminaUpgrade", RpcTarget.Others, staminaItems);
                }

                // Update UI
                if (Character.localCharacter != null)
                {
                    StartCoroutine(ForceStaminaUIUpdate());
                }

                SaveState();
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error in ApplyProgressiveStamina: " + ex.Message);
            }
        }

        public const float TRACKER_SPAWN_COOLDOWN = 5f;
        private float _lastTrackerSpawnTime = -999f;

        public float GetTrackerSpawnCooldownRemaining()
        {
            float remaining = TRACKER_SPAWN_COOLDOWN - (Time.time - _lastTrackerSpawnTime);
            return remaining > 0f ? remaining : 0f;
        }

        public bool TrySpawnTrackerItem(string internalName, out string message)
        {
            message = null;
            try
            {
                float remaining = GetTrackerSpawnCooldownRemaining();
                if (remaining > 0f)
                {
                    message = $"Cooldown {remaining:0.0}s";
                    return false;
                }

                if (Character.localCharacter == null || Character.localCharacter.data.dead)
                {
                    message = "Not in a run";
                    return false;
                }

                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Airport")
                {
                    message = "Can't spawn in the lobby";
                    return false;
                }

                if (!PhotonNetwork.InRoom)
                {
                    message = "Not connected";
                    return false;
                }

                if (string.IsNullOrEmpty(internalName) || !ItemIdMappings.NameToId.TryGetValue(internalName, out ushort itemId))
                {
                    message = "No item mapping";
                    return false;
                }

                if (!ItemDatabase.TryGetItem(itemId, out Item itemToSpawn))
                {
                    message = "Item not in database";
                    return false;
                }

                if (_itemSanityEnabled && !UnlockedItemsManager.IsItemUnlocked(itemId))
                {
                    message = "Not unlocked";
                    return false;
                }

                var local = Character.localCharacter;
                Vector3 forward = MainCamera.instance != null
                    ? MainCamera.instance.transform.forward
                    : local.transform.forward;

                Vector3 spawnPosition = local.Center + forward * 2f + Vector3.up * 0.5f;
                PhotonNetwork.Instantiate("0_Items/" + itemToSpawn.name, spawnPosition, Quaternion.identity, 0);

                _lastTrackerSpawnTime = Time.time;
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error spawning tracker item: " + ex.Message);
                message = "Spawn failed";
                return false;
            }
        }

        private void SpawnPhysicalItem(string itemName)
        {
            try
            {
                if (!ItemIdMappings.TryGetId(itemName, out ushort itemId))
                {
                    _log.LogWarning($"[PeakPelago] No ID mapping for item: {itemName}");
                    return;
                }
                
                if (!ItemDatabase.TryGetItem(itemId, out Item itemToSpawn))
                {
                    _log.LogWarning($"[PeakPelago] Item ID {itemId} not in database: {itemName}");
                    return;
                }

                // Get all valid characters
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    _log.LogWarning("[PeakPelago] Cannot spawn items - no characters found");
                    return;
                }

                var validCharacters = Character.AllCharacters.Where(c =>
                    c != null &&
                    c.gameObject.activeInHierarchy &&
                    !c.data.dead
                ).ToList();

                if (validCharacters.Count == 0)
                {
                    _log.LogWarning("[PeakPelago] Cannot spawn items - no valid characters found");
                    return;
                }

                

                var character = validCharacters[UnityEngine.Random.Range(0, validCharacters.Count)];
                try
                {
                    Vector3 spawnPosition = character.Center + character.transform.forward * 2f + Vector3.up * 0.5f;
                    GameObject spawnedItem = PhotonNetwork.Instantiate("0_Items/" + itemToSpawn.name, spawnPosition, Quaternion.identity, 0);
                }
                catch (Exception ex)
                {
                    _log.LogError($"[PeakPelago] Error spawning item for character: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error spawning physical item {itemName}: {ex.Message}");
            }
        }
        private void RebuildAscentUnlocks()
        {
            if (_session == null) return;

            int progressiveAscentCount = _session.Items.AllItemsReceived.Count(item =>
            {
                string itemName = _session.Items.GetItemName(item.ItemId, item.ItemGame);
                return itemName?.Equals("Progressive Ascent", StringComparison.OrdinalIgnoreCase) ?? false;
            });

            progressiveAscentCount = Mathf.Min(progressiveAscentCount, 8);

            _stateData.UnlockedAscents.Clear();
            for (int i = 1; i <= progressiveAscentCount; i++)
            {
                _stateData.UnlockedAscents.Add(i);
            }
        }

        private void UnlockAscent()
        {
            try
            {
                if (_session == null) return;

                RebuildAscentUnlocks();

                SaveState();
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error unlocking progressive ascent: " + ex.Message);
            }
        }

        private void DestroyHeldItem()
        {
            try
            {
                if (Character.localCharacter == null || Character.localCharacter.refs.items == null)
                {
                    _log.LogWarning("[PeakPelago] Cannot destroy held item - no local character or items");
                    return;
                }

                // Use the existing destroy held item system
                Character.localCharacter.refs.items.photonView.RPC("DestroyHeldItemRpc", RpcTarget.All);
                
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error destroying held item: " + ex.Message);
            }
        }
        private void ClearAllEffects()
        {
            try
            {
                if (Character.localCharacter == null || Character.localCharacter.refs.afflictions == null)
                {
                    _log.LogWarning("[PeakPelago] Cannot clear effects - no local character or afflictions");
                    return;
                }

                // Clear all status effects from the character
                var afflictions = Character.localCharacter.refs.afflictions;
                afflictions.ClearAllStatus(excludeCurse: false);
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error clearing all effects: " + ex.Message);
            }
        }

        private void ApplySpeedUpgrade()
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Cannot apply speed upgrade - no local character");
                    return;
                }

                // Apply a temporary speed boost using movement modifier
                var character = Character.localCharacter;
                var movement = character.refs.movement;
                var originalModifier = movement.movementModifier;

                // Increase speed by 50% for 30 seconds
                movement.movementModifier += 0.5f;

                // Start coroutine to restore original speed after 30 seconds
                StartCoroutine(RestoreSpeedAfterDelay(character, originalModifier, 30f));
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error applying speed upgrade: " + ex.Message);
            }
        }

        private System.Collections.IEnumerator RestoreSpeedAfterDelay(Character character, float originalModifier, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (character != null && character.refs.movement != null)
            {
                character.refs.movement.movementModifier = originalModifier;
            }
        }
        public void ApplyItemEffect(string itemName, bool fromTrapLink = false)
        {
            try
            {
                _itemsReceivedFromAP++;
                _lastReceivedItemName = itemName;
                _lastReceivedItemTime = DateTime.Now;

                // Unlock items just add to the loot pool - no physical spawn needed
                if (itemName.EndsWith(" Unlock"))
                {
                    if (itemName.Equals("Scout's Honor Unlock", StringComparison.OrdinalIgnoreCase))
                    {
                        ScoutStatueHonorGatePatch.RetriggerStatues();
                    }
                    if (itemName.Equals("Strange Gem Unlock", StringComparison.OrdinalIgnoreCase))
                    {
                        ScoutStatueGemGatePatch.RetriggerGemSpawns();
                    }
                    if (itemName.Equals("Progressive Amulet Unlock", StringComparison.OrdinalIgnoreCase))
                    {
                        ScoutStatueGemGatePatch.RetriggerGemSpawns();
                        ScoutStatueHonorGatePatch.RetriggerStatues();
                    }
                    if (itemName.StartsWith("Scout's", StringComparison.OrdinalIgnoreCase) ||
                        itemName.Equals("Progressive Amulet Unlock", StringComparison.OrdinalIgnoreCase) ||
                        itemName.Equals("Strange Gem Unlock", StringComparison.OrdinalIgnoreCase))
                    {
                        AmuletSpawnerGatePatch.RetriggerBlockedSpawners();
                        FakeAmuletGatePatch.ApplyGates(null);
                    }
                    BumpTrackerVersion();
                    return;
                }

                if (_itemEffectHandlers.ContainsKey(itemName))
                {
                    _itemEffectHandlers[itemName].Invoke();

                    if (!fromTrapLink && _trapLinkService != null && IsTrapItem(itemName))
                    {
                        _trapLinkService.SendTrapLink(itemName);
                    }
                    BumpTrackerVersion();
                }
                else
                {
                    _log.LogWarning("[PeakPelago] No effect handler found for item: " + itemName);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error applying item effect for " + itemName + ": " + ex.Message);
            }
        }
        
        private bool IsTrapItem(string itemName)
        {
            return TrapTypeExtensions.IsTrapName(itemName);
        }

        // Method to track when items are received from Archipelago
        public void TrackItemReceivedFromAP(string itemName)
        {
            try
            {
                _itemsReceivedFromAP++;
                _lastReceivedItemName = itemName;
                _lastReceivedItemTime = DateTime.Now;
                ApplyItemEffect(itemName);
            }
            catch (System.Exception ex)
            {
                _log.LogError("[PeakPelago] Error tracking received item: " + ex.Message);
            }
        }
        private void CheckAndReportItemAchievement(string achievementName, int requiredCount, int currentCount)
        {
            if (currentCount >= requiredCount)
            {
                ReportCheckByName(achievementName);
            }
        }

        private void TrackItemAcquisition(string itemName, ushort itemId = 0)
        {
            

            // Update last acquired item info
            _lastAcquiredItemName = itemName;
            _lastAcquiredItemId = itemId;
            _lastAcquiredItemTime = Time.time;

            // Check if this item ID has an Archipelago location to report
            if (_itemIdToLocationMapping.TryGetValue(itemId, out string locationName))
            {

                ReportCheckByName(locationName);
                BumpTrackerVersion();
                if (_logicalScoutStatue && locationName.StartsWith("Acquire "))
                {
                    UnlockedItemsManager.RefreshScoutStatuePool();
                }
            }
            else
            {
                _log.LogDebug($"[PeakPelago] No Archipelago location found for item ID: {itemId} ({itemName})");
            }
            
            SaveState();
        }

        /// <summary>Call this when a new run starts to reset run-specific counters</summary>
        public void ResetRunCounters()
        {
            _luggageOpenedThisRun = 0;
            
        }




        /// <summary>Handle ascent-specific badge awards when reaching peaks</summary>
        public void HandleAscentPeakReached(string peakName)
        {
            if (_session == null) 
            {
                _log.LogDebug("[PeakPelago] HandleAscentPeakReached: No session, skipping");
                return;
            }

            try
            {
                // Get current ascent level using reflection
                int currentAscent = GetCurrentAscentLevel();

                // Award ascent-specific badges based on peak and ascent level
                // Award ALL previous badges for this peak too
                for (int i = 0; i <= currentAscent; i++)
                {
                    string badgeLocation = GetAscentBadgeLocation(peakName, i);
                    if (!string.IsNullOrEmpty(badgeLocation))
                    {
                        string badgeKey = badgeLocation + "_" + i;
                        
                        if (!_awardedAscentBadges.Contains(badgeKey))
                        {
                            ReportCheckByName(badgeLocation);
                            _awardedAscentBadges.Add(badgeKey);
                            
                        }
                    }
                }

                // Award scout sash only when reaching the final peak (PEAK)
                if (peakName.ToUpper() == "PEAK")
                {
                    // Award ALL previous sashes too
                    for (int i = 1; i <= currentAscent; i++)
                    {
                        string sashLocation = GetScoutSashLocation(i);

                        if (!string.IsNullOrEmpty(sashLocation))
                        {
                            string sashKey = sashLocation + "_" + i;

                            if (!_awardedAscentBadges.Contains(sashKey))
                            {
                                ReportCheckByName(sashLocation);
                                _awardedAscentBadges.Add(sashKey);
                                
                            }
                        }
                    }
                    
                    // Award ALL previous ascent completion checks too just in case lol
                    for (int i = 0; i <= currentAscent; i++)
                    {
                        string completionLocation = $"Ascent {i} Completed";
                        string completionKey = completionLocation + "_" + i;
                        
                        if (!_awardedAscentBadges.Contains(completionKey))
                        {
                            ReportCheckByName(completionLocation);
                            _awardedAscentBadges.Add(completionKey);
                            
                        }
                    }
                    
                    CheckReachPeakGoal(currentAscent);
                }
                else
                {
                    
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error handling ascent peak reached: " + ex.Message);
            }
        }

        private int GetCurrentAscentLevel()
        {
            try
            {
                // Try direct access to the static class (if it's in the same namespace or accessible)
                // This might work if the Ascents class is accessible without reflection
                
                // Method 1: Try to access via reflection with better error handling
                var ascentsType = System.Type.GetType("Ascents");
                if (ascentsType == null)
                {
                    // Try with full assembly name
                    ascentsType = System.Type.GetType("Ascents, Assembly-CSharp");
                }
                
                if (ascentsType != null)
                {
                    _log.LogDebug("[PeakPelago] Found Ascents class: " + ascentsType.FullName);
                    
                    // Try the internal field first (like AscentUI does)
                    var field = ascentsType.GetField("_currentAscent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (field != null)
                    {
                        int ascent = (int)field.GetValue(null);
                        _log.LogDebug("[PeakPelago] Got current ascent via _currentAscent field: " + ascent);
                        return ascent;
                    }
                    
                    // Try the public property
                    var property = ascentsType.GetProperty("currentAscent");
                    if (property != null)
                    {
                        int ascent = (int)property.GetValue(null);
                        _log.LogDebug("[PeakPelago] Got current ascent via currentAscent property: " + ascent);
                        return ascent;
                    }
                }
                
                _log.LogWarning("[PeakPelago] Could not find or access Ascents class");
                return -1;
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error getting current ascent level: " + ex.Message);
                return -1;
            }
        }

        private static string GetPeakNameForBiome(Biome.BiomeType biome)
        {
            switch (biome)
            {
                case Biome.BiomeType.Shore:
                    return "SHORE";
                case Biome.BiomeType.Tropics:
                    return "TROPICS";
                case Biome.BiomeType.Mesa:
                    return "MESA";
                case Biome.BiomeType.Alpine:
                    return "ALPINE";
                case Biome.BiomeType.Volcano:
                    return "CALDERA";
                case Biome.BiomeType.Swamp:
                    return "GLOOM";
                case Biome.BiomeType.Roots:
                    return "ROOTS";
                default:
                    return null;
            }
        }

        private static string GetBaseBadgeForPeak(string peakName)
        {
            switch (peakName.ToUpper())
            {
                case "SHORE":
                    return "Beachcomber Badge";
                case "TROPICS":
                    return "Trailblazer Badge";
                case "MESA":
                    return "Nomad Badge";
                case "ALPINE":
                    return "Alpinist Badge";
                case "CALDERA":
                case "THE KILN":
                    return "Volcanology Badge";
                case "ROOTS":
                    return "Forestry Badge";
                case "GLOOM":
                case "CITADEL":
                    return "Wanderer Badge";
                default:
                    return null;
            }
        }

        private string GetAscentBadgeLocation(string peakName, int ascentLevel)
        {
            // Map peak names to their ascent badge locations
            switch (peakName.ToUpper())
            {
                case "SHORE":
                    return "Beachcomber " + GetRomanNumeral(ascentLevel) + " Badge (Ascent " + ascentLevel + ")";
                case "TROPICS":
                    return "Trailblazer " + GetRomanNumeral(ascentLevel) + " Badge (Ascent " + ascentLevel + ")";
                case "MESA":
                    return "Nomad " + GetRomanNumeral(ascentLevel) + " Badge (Ascent " + ascentLevel + ")";
                case "ALPINE":
                    return "Alpinist " + GetRomanNumeral(ascentLevel) + " Badge (Ascent " + ascentLevel + ")";
                case "CALDERA":
                    return "Volcanology " + GetRomanNumeral(ascentLevel) + " Badge (Ascent " + ascentLevel + ")";
                case "THE KILN":
                    return "Volcanology " + GetRomanNumeral(ascentLevel) + " Badge (Ascent " + ascentLevel + ")";
                case "ROOTS":
                    return "Forestry " + GetRomanNumeral(ascentLevel) + " Badge (Ascent " + ascentLevel + ")";
                case "GLOOM":
                    return "Wanderer " + GetRomanNumeral(ascentLevel) + " Badge (Ascent " + ascentLevel + ")";
                case "CITADEL":
                    return "Wanderer " + GetRomanNumeral(ascentLevel) + " Badge (Ascent " + ascentLevel + ")";
                default:
                    _log.LogWarning("[PeakPelago] Unknown peak name for ascent badge: " + peakName);
                    return null;
            }
        }

        private string GetScoutSashLocation(int ascentLevel)
        {
            // Map ascent levels to scout sash locations
            switch (ascentLevel)
            {
                case 1: return "Rabbit Scout sashe (Ascent 1)";
                case 2: return "Raccoon Scout sashe (Ascent 2)";
                case 3: return "Mule Scout sashe (Ascent 3)";
                case 4: return "Kangaroo Scout sashe (Ascent 4)";
                case 5: return "Owl Scout sashe (Ascent 5)";
                case 6: return "Wolf Scout sashe (Ascent 6)";
                case 7: return "Goat Scout sashe (Ascent 7)";
                default: 
                    _log.LogWarning("[PeakPelago] Unknown ascent level for scout sash: " + ascentLevel);
                    return null;
            }
        }

        private string GetRomanNumeral(int number)
        {
            switch (number)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                case 5: return "V";
                case 6: return "VI";
                case 7: return "VII";
                case 8: return "VIII";
                case 9: return "IX";
                default: return number.ToString();
            }
        }

        private System.Collections.IEnumerator ForceStaminaUIUpdate()
        {
            // Wait a bit for the character and UI to be fully initialized
            yield return new WaitForSeconds(0.5f);

            

            // Force update the character's stamina
            if (Character.localCharacter != null && _staminaManager != null)
            {
                // Recalculate and clamp stamina
                float baseMax = _staminaManager.GetBaseMaxStamina();
                float statusSum = Character.localCharacter.refs.afflictions.statusSum;
                float effectiveMax = Mathf.Max(baseMax - statusSum, 0f);
                Character.localCharacter.data.currentStamina = Mathf.Min(Character.localCharacter.data.currentStamina, effectiveMax);

                

                // Force the stamina bar UI to refresh
                if (GUIManager.instance != null && GUIManager.instance.bar != null)
                {
                    GUIManager.instance.bar.ChangeBar();
                    
                }
                else
                {
                    _log.LogWarning("[PeakPelago] CLIENT: GUIManager or bar is null, couldn't refresh UI");
                }
            }
            else
            {
                _log.LogWarning("[PeakPelago] CLIENT: Character or stamina manager is null, couldn't update");
            }
        }
        private MountainHint _cachedNextMountainHint;
        private int _cachedHintMountainCount = -1;
        private int _cachedHintCheckedCount = -1;

        private MountainHint GetNextMountainHint()
        {
            if (_mountainHints == null || _mountainHints.Count == 0) return null;

            int checkedCount = _session != null ? _session.Locations.AllLocationsChecked.Count : -1;
            if (_cachedHintMountainCount == _progressiveMountainCount && _cachedHintCheckedCount == checkedCount)
            {
                return _cachedNextMountainHint;
            }

            MountainHint resolved = null;
            int start = Mathf.Clamp(_progressiveMountainCount, 0, _mountainHints.Count - 1);

            for (int offset = 0; offset < _mountainHints.Count; offset++)
            {
                int i = (start + offset) % _mountainHints.Count;
                if (!IsMountainHintCollected(_mountainHints[i]))
                {
                    resolved = _mountainHints[i];
                    break;
                }
            }

            _cachedNextMountainHint = resolved;
            _cachedHintMountainCount = _progressiveMountainCount;
            _cachedHintCheckedCount = checkedCount;
            return resolved;
        }

        private bool IsMountainHintCollected(MountainHint hint)
        {
            if (hint == null || hint.LocationId <= 0) return true;
            if (_session == null) return false;
            if (hint.PlayerSlot != _session.ConnectionInfo.Slot) return false;

            return _session.Locations.AllLocationsChecked.Contains(hint.LocationId);
        }

        private static class CampfireHelper
        {
            public static int GetRequiredProgressiveMountain(Biome.BiomeType biome, int segmentIndex)
            {
                switch (biome)
                {
                    case Biome.BiomeType.Shore:
                        return 1;
                    case Biome.BiomeType.Tropics:
                    case Biome.BiomeType.Roots:
                        return 2;
                    case Biome.BiomeType.Alpine:
                    case Biome.BiomeType.Mesa:
                        return 3;
                    case Biome.BiomeType.Volcano:
                    case Biome.BiomeType.Swamp:
                        return 4;
                    default:
                        return Mathf.Clamp(segmentIndex + 1, 1, 4);
                }
            }

            public static int GetCurrentSegmentIndex(MapHandler mapHandler)
            {
                if (mapHandler == null || mapHandler.segments == null) return -1;
                if (VoidBiome.VoidBiomeActive) return -1;

                int index = (int)mapHandler.GetCurrentSegment();
                if (index < 0 || index >= mapHandler.segments.Length) return -1;
                return index;
            }
        }

        // ===== Harmony Patches =====
        // Add this field near your other tracking fields

        [HarmonyPatch(typeof(BasketballHoop), "OnTriggerExit")]
        public static class BasketballHoopScorePatch
        {
            private static int _totalBasketsScored = 0;

            public static void ResetRunCount() => _totalBasketsScored = 0;
            public static void SetTotalCount(int count) => _totalBasketsScored = count;
            public static int GetTotalCount() => _totalBasketsScored;

            [HarmonyPrefix]
            static void Prefix(BasketballHoop __instance, Collider other, out float __state)
            {
                __state = __instance.lastScoredTime;
            }

            static void Postfix(BasketballHoop __instance, float __state)
            {
                // If lastScoredTime changed, the original method scored a basket
                if (__instance.lastScoredTime != __state)
                {
                    _totalBasketsScored++;
                    PeakArchipelagoPlugin._instance?.OnBasketballScored(_totalBasketsScored);
                }
            }
        }

        [HarmonyPatch(typeof(Character), "UseStamina")]
        public static class CharacterUseStaminaBreathLinkPatch
        {
            static void Postfix(Character __instance, float usage, bool __result)
            {
                try
                {
                    // UseStamina returns false immediately when usage is zero
                    if (usage <= 0f) return;
                    // UseStamina returns false when stamina is fully depleted
                    if (__result) return;
                    if (__instance.data.currentStamina > 0f || __instance.data.extraStamina > 0f) return;
                    if (!__instance.view.IsMine) return;
                    if (_instance == null) return;
                    if (_instance._breathLinkService == null || !_instance._breathLinkService.IsEnabled()) return;
                    if (_instance._breathLinkService.IsDepleting()) return;

                    _instance._breathLinkService.SendBreathLink($"{__instance.characterName} ran out of stamina");
                }
                catch (Exception ex)
                {
                    _instance?._log?.LogError($"[PeakPelago] Error in UseStamina BreathLink patch: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(CharacterAfflictions), "SubtractStatus")]
        public static class CharacterAfflictionsSubtractStatusPatch
        {
            static void Prefix(CharacterAfflictions __instance, out float __state)
            {
                __state = __instance.GetCurrentStatus(STATUSTYPE.Poison);
            }

            static void Postfix(CharacterAfflictions __instance, STATUSTYPE statusType, float amount, bool fromRPC, bool decreasedNaturally, float __state)
            {
                try
                {
                    if (_instance == null || _instance._session == null) return;
                    if (!__instance.character.IsLocal) return;
                    if (statusType != STATUSTYPE.Poison) return;
                    if (decreasedNaturally) return; // Only count intentional healing

                    float healed = __state - __instance.GetCurrentStatus(STATUSTYPE.Poison);
                    if (healed <= 0f) return;

                    _instance._stateData.PoisonHealed += healed;

                    if (_instance._stateData.PoisonHealed >= 2.0f && !_instance._collectedBadges.Contains(ACHIEVEMENTTYPE.ToxicologyBadge))
                    {
                        _instance.ReportCheckByName("Toxicology Badge");
                        _instance._collectedBadges.Add(ACHIEVEMENTTYPE.ToxicologyBadge);
                    }
                }
                catch (Exception ex)
                {
                    _instance?._log.LogError($"[PeakPelago] SubtractStatus patch error: {ex.Message}");
                }
            }
        }
        [HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.AddStatusBlockedByMilk), MethodType.Normal)]
        public static class AchievementManagerAddStatusBlockedByMilkPatch
        {
            static void Postfix(float amount)
            {
                if (_instance == null) return;
                if (_instance._session == null) return;
                if (!PhotonNetwork.IsMasterClient) return;
                
                _instance._stateData.DamageBlockedByMilk += amount;
                
                if (_instance._stateData.DamageBlockedByMilk >= 0.1f && !_instance._collectedBadges.Contains(ACHIEVEMENTTYPE.CalciumIntakeBadge))
                {
                    var badgeMapping = _instance.GetBadgeToLocationMapping();
                    if (badgeMapping.TryGetValue(ACHIEVEMENTTYPE.CalciumIntakeBadge, out string locationName))
                    {
                        _instance.ReportCheckByName("Calcium Intake Badge");
                        _instance._collectedBadges.Add(ACHIEVEMENTTYPE.CalciumIntakeBadge);
                    }
                }
            }
        }
        [HarmonyPatch(typeof(Campfire), "GetInteractionText")]
        public static class CampfireGetInteractionTextPatch
        {
            static void Postfix(Campfire __instance, ref string __result)
            {
                try
                {
                    if (_instance == null) return;
                    
                    if (__instance.state == Campfire.FireState.Spent && __instance.advanceToSegment != 0)
                    {
                        int currentSegmentIndex = CampfireHelper.GetCurrentSegmentIndex(Singleton<MapHandler>.Instance);
                        if (currentSegmentIndex < 0) return;

                        if (currentSegmentIndex < (int)__instance.advanceToSegment)
                        {
                            var currentSegment = Singleton<MapHandler>.Instance.segments[currentSegmentIndex];
                            int required = CampfireHelper.GetRequiredProgressiveMountain(currentSegment.biome, currentSegmentIndex);
                            string hintText = "";
                            var hint = _instance.GetNextMountainHint();
                            if (hint != null)
                            {
                                hintText = $"{hint.Location} ({hint.Player}'s {hint.Game})";
                            }
                            
                            if (required > 0 && _instance._progressiveMountainCount < required)
                            {
                                __result = $"{hintText}";
                            }
                            else
                            {
                                __result = LocalizedText.GetText("LIGHT");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _instance?._log.LogError($"[PeakPelago] GetInteractionText patch error: {ex.Message}");
                }
            }
        }
        [HarmonyPatch(typeof(Campfire), "IsInteractible")]
        public static class CampfireIsInteractiblePatch
        {
            static void Postfix(Campfire __instance, Character interactor, ref bool __result)
            {
                try
                {
                    if (_instance == null) return;
                    
                    if (__instance.state == Campfire.FireState.Spent && __instance.advanceToSegment != 0)
                    {
                        int currentSegmentIndex = CampfireHelper.GetCurrentSegmentIndex(Singleton<MapHandler>.Instance);
                        if (currentSegmentIndex < 0) return;

                        if (currentSegmentIndex < (int)__instance.advanceToSegment)
                        {
                            __result = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError($"[PeakPelago] IsInteractible patch error: {ex.Message}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Campfire), "IsConstantlyInteractable")]
        public static class CampfireIsConstantlyInteractablePatch
        {
            static void Postfix(Campfire __instance, Character interactor, ref bool __result)
            {
                try
                {
                    if (_instance == null) return;
                    
                    if (__instance.state == Campfire.FireState.Spent && __instance.advanceToSegment != 0)
                    {
                        int currentSegmentIndex = CampfireHelper.GetCurrentSegmentIndex(Singleton<MapHandler>.Instance);
                        if (currentSegmentIndex < 0) return;

                        if (currentSegmentIndex < (int)__instance.advanceToSegment)
                        {
                            __result = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError($"[PeakPelago] IsConstantlyInteractable patch error: {ex.Message}");
                    }
                }
            }
        }
        [HarmonyPatch(typeof(Campfire), "Interact_CastFinished")]
        public static class CampfireInteractCastFinishedPatch
        {
            static bool Prefix(Campfire __instance, Character interactor)
            {
                try
                {
                    if (_instance == null) return true;
                    
                    // Only host processes the logic
                    if (!PhotonNetwork.IsMasterClient) return true;
                    
                    // Only if campfire is SPENT and it's a transition campfire
                    if (__instance.state == Campfire.FireState.Spent && __instance.advanceToSegment != 0)
                    {
                        var mapHandler = Singleton<MapHandler>.Instance;
                        if (mapHandler == null) return true;
                        
                        int currentSegmentIndex = CampfireHelper.GetCurrentSegmentIndex(mapHandler);
                        if (currentSegmentIndex < 0) return true;

                        // Check if we haven't advanced yet
                        if (currentSegmentIndex < (int)__instance.advanceToSegment)
                        {
                            var currentSegmentData = mapHandler.segments[currentSegmentIndex];

                            int requiredMountain = CampfireHelper.GetRequiredProgressiveMountain(currentSegmentData.biome, currentSegmentIndex);

                            // If we now have enough, trigger the advancement
                            if (requiredMountain > 0 && _instance._progressiveMountainCount >= requiredMountain)
                            {

                                // Light on all clients, which advances the segment and writes the quicksave
                                if (__instance.view != null && PhotonNetwork.IsConnected)
                                {
                                    __instance.view.RPC("Light_Rpc", RpcTarget.All, true, 0f);
                                }
                                else
                                {
                                    // Fallback for single player
                                    mapHandler.GoToSegment(__instance.advanceToSegment);
                                }

                                return false; // Skip original method
                            }
                        }
                    }
                    
                    return true; // Allow normal behavior
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError($"[PeakPelago] Interact_CastFinished patch error: {ex.Message}");
                    }
                    return true;
                }
            }
        }
        
        [HarmonyPatch(typeof(Campfire), "Light_Rpc")]
        public static class CampfireLightRpcPatch
        {
            static bool Prefix(Campfire __instance, bool updateSegment, float burningFor)
            {
                try
                {
                    if (_instance == null) return true;
                    if (!updateSegment) return true;

                    var advanceToSegment = __instance.advanceToSegment;
                    if (advanceToSegment == 0) return true;

                    var mapHandler = FindFirstObjectByType<MapHandler>();
                    if (mapHandler == null) return true;

                    int currentSegmentIndex = CampfireHelper.GetCurrentSegmentIndex(mapHandler);
                    if (currentSegmentIndex < 0)
                    {
                        return true;
                    }

                    var currentSegment = mapHandler.segments[currentSegmentIndex];
                    string currentBiomeName = currentSegment.biome.ToString().ToUpper();

                    int requiredMountain = CampfireHelper.GetRequiredProgressiveMountain(currentSegment.biome, currentSegmentIndex);

                    if (requiredMountain > 0)
                    {
                        if (_instance._progressiveMountainCount < requiredMountain)
                        {
                            _instance._log.LogWarning($"[PeakPelago] Cannot advance from {currentBiomeName} - need {requiredMountain} Progressive Mountain, have {_instance._progressiveMountainCount}");
                            
                            // Show warning notification
                            _instance._notifications?.ShowWarningMessage($"Need {requiredMountain} Mountain Progress to advance! (Have {_instance._progressiveMountainCount})");
                            var hint = _instance.GetNextMountainHint();
                            if (hint != null)
                            {
                                _instance._notifications?.ShowSimpleMessage($"Mountain #{requiredMountain}: {hint.Location} ({hint.Player}'s {hint.Game})");
                                if (_instance._session != null && hint.LocationId > 0)
                                {
                                    try
                                    {
                                        _instance._session.Hints.CreateHints(hint.PlayerSlot, HintStatus.Unspecified, hint.LocationId);
                                    }
                                    catch (Exception ex)
                                    {
                                        _instance._log.LogDebug($"[PeakPelago] Could not create hint: {ex.Message}");
                                    }
                                }
                            }
                            
                            if (PhotonNetwork.IsMasterClient)
                            {
                                string completedPeakName = GetPeakNameForBiome(currentSegment.biome);
                                if (completedPeakName != null)
                                {
                                    _instance.HandleAscentPeakReached(completedPeakName);
                                    string baseBadge = GetBaseBadgeForPeak(completedPeakName);
                                    if (baseBadge != null)
                                    {
                                        _instance.ReportCheckByName(baseBadge);
                                    }
                                }
                            }

                            // Light the campfire but don't advance
                            __instance.state = Campfire.FireState.Lit;
                            __instance.beenBurningFor = burningFor;
                            Shader.SetGlobalFloat("FakeMountainEnabled", (!__instance.disableFogFakeMountain) ? 1 : 0);

                            var updateLitMethod = typeof(Campfire).GetMethod("UpdateLit",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            updateLitMethod?.Invoke(__instance, null);
                            
                            __instance.smokeParticlesOff.Stop();
                            __instance.smokeParticlesLit.Play();
                            
                            if (GUIManager.instance != null)
                            {
                                GUIManager.instance.RefreshInteractablePrompt();
                            }
                            
                            return false;
                        }
                    }
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _instance?._log.LogError($"[PeakPelago] Campfire Light_Rpc patch error: {ex.Message}");
                    return true;
                }
            }
            
        }
        [HarmonyPatch(typeof(PauseMenuMainPage), "Quit")]
        public static class PauseMenuQuitPatch
        {
            static void Prefix()
            {
                if (_instance?._session?.Socket != null && _instance._session.Socket.Connected)
                {
                    _instance._log?.LogInfo("[PeakPelago] Disconnecting from Archipelago (quit to menu)");
                    _instance._intentionalDisconnect = true;
                    _instance.CancelReconnect();
                    _instance._session.Socket.Disconnect();
                    _instance._status = "Disconnected";
                }
            }
        }
        [HarmonyPatch(typeof(Character), "UseStamina")]
        public static class CharacterUseStaminaPatch
        {
            static void Prefix(Character __instance, ref float usage)
            {
                try
                {
                    if (_instance == null) return;
                    if (__instance.photonView != null && __instance.photonView.Owner != null)
                    {
                        var props = __instance.photonView.Owner.CustomProperties;
                        if (props.ContainsKey("AP_EnduranceMultiplier"))
                        {
                            float multiplier = (float)props["AP_EnduranceMultiplier"];
                            usage *= multiplier;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError($"[PeakPelago] UseStamina patch error: {ex.Message}");
                    }
                }
            }
        }
        [HarmonyPatch(typeof(RunManager), "StartRun")]
        public static class RunManagerStartRunPatch
        {
            static void Postfix(RunManager __instance)
            {
                try
                {
                    if (_instance == null) return;

                    _instance.ResetRunCounters();

                    if (_instance._staminaManager != null)
                    {
                        int upgrades = _instance._staminaManager.GetStaminaUpgradesReceived();
                        _instance._staminaManager._localStaminaUpgrades = 0;
                        _instance._staminaManager._localBaseMaxStamina = 0.25f;
                        for (int i = 0; i < upgrades; i++)
                        {
                            _instance._staminaManager.ApplyStaminaUpgrade();
                        }
                        _instance.StartCoroutine(_instance.ForceStaminaUIUpdate());
                        _instance._log.LogInfo($"[PeakPelago] Stamina reset and reloaded {upgrades} upgrades on run start");
                    }



                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError($"[PeakPelago] StartRun patch error: {ex.Message}");
                    }
                }
            }
            


        }
        [HarmonyPatch(typeof(CharacterItems), "HammerClimbingSpike")]
        public static class CharacterItemsHammerClimbingSpikePatch
        {
            static void Postfix(CharacterItems __instance, RaycastHit hit)
            {
                try
                {
                    if (_instance == null) return;
                    
                    // Only track for master client to avoid duplicates
                    if (!PhotonNetwork.IsMasterClient) return;
                    
                    _instance._stateData.PitonsPlaced++;
                    // Report Bouldering Badge at 10 pitons
                    if (_instance._stateData.PitonsPlaced >= 10)
                    {
                        _instance.ReportCheckByName("Bouldering Badge");
                    }
                    
                    _instance.SaveState();
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError($"[PeakPelago] HammerClimbingSpike patch error: {ex.Message}");
                    }
                }
            }
        }
        [HarmonyPatch(typeof(ItemCooking), "FinishCookingRPC")]
        public static class ItemCookingFinishCookingRPCPatch
        {
            static void Postfix(ItemCooking __instance)
            {
                try
                {
                    if (_instance == null) return;
                    
                    // Only track for master client to avoid duplicates
                    if (!PhotonNetwork.IsMasterClient) return;
                    
                    // Increment the cooked items counter
                    _instance._stateData.TotalItemsCooked++;

                    
                    // Report Cooking Badge at 20 items
                    if (_instance._stateData.TotalItemsCooked >= 20)
                    {
                        _instance.ReportCheckByName("Cooking Badge");
                    }
                    
                    _instance.SaveState();
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError($"[PeakPelago] FinishCookingRPC patch error: {ex.Message}");
                    }
                }
            }
        }
                
        ///This updates the DeathLink checkpoint spawn when we initialize a new map segment (like when we activate a campfire at the top of one of the biomes)
        [HarmonyPatch(typeof(MapHandler), "GoToSegment")]
        public static class MapHandlerGoToSegmentPatch
        {
            static void Postfix(MapHandler __instance)
            {
                try
                {
                    if (_instance == null) return;
                    
                    int currentSegment = CampfireHelper.GetCurrentSegmentIndex(__instance);
                    if (currentSegment < 0) return;

                    // Update spawn points for DeathLink
                    var segment = __instance.segments[currentSegment];
                    if (segment.reconnectSpawnPos != null)
                    {
                        foreach (var character in Character.AllCharacters)
                        {
                            if (character != null)
                            {
                                character.data.spawnPoint = segment.reconnectSpawnPos;
                            }
                        }
                    }


                    // Award badge for the segment we just COMPLETED (previous segment)
                    if (currentSegment > 0)
                    {
                        var previousSegment = __instance.segments[currentSegment - 1];
                        string peakName = GetPeakNameForBiome(previousSegment.biome);

                        if (peakName != null)
                        {
                            _instance.HandleAscentPeakReached(peakName);
                        }
                        else
                        {
                            _instance._log.LogWarning($"[PeakPelago] No ascent badge mapping for completed biome {previousSegment.biome}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError($"[PeakPelago] GoToSegment patch error: {ex.Message}");
                        _instance._log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
                    }
                }
            }
        }
        [HarmonyPatch(typeof(Luggage), "OpenLuggageRPC")]
        public static class LuggageOpenRPCPatch
        {
            static void Prefix(Luggage __instance, out bool __state)
            {
                __state = __instance.state == Luggage.LuggageState.Open;
            }

            static void Postfix(Luggage __instance, bool spawnItems, bool __state)
            {
                try
                {
                    if (_instance == null || !PhotonNetwork.IsMasterClient)
                    {
                        return;
                    }

                    // Only count a real player open that actually transitioned the luggage
                    if (!spawnItems || __state) return;

                    // Host observes ALL luggage opens via the RPC

                    _instance.IncrementLuggageCount();
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError($"[PeakPelago] OpenLuggageRPC patch error: {ex.Message}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Player), "AddItem")]
        public static class PlayerAddItemPatch
        {
            static void Postfix(Player __instance, ushort itemID, ItemInstanceData instanceData, ref ItemSlot slot, bool __result)
            {
                try
                {
                    if (!__result) return; // Item wasn't actually added
                    if (_instance == null) return;
                    if (!PhotonNetwork.IsMasterClient) return;

                    // Quick check - skip if no AP location for this item
                    if (!_instance._itemIdToLocationMapping.ContainsKey(itemID)) return;

                    // Check if already reported
                    if (_instance._itemIdToLocationMapping.TryGetValue(itemID, out string locationName))
                    {
                        if (_instance._locationCache.TryGetValue(locationName, out long cachedId) 
                            && _instance._stateData.ReportedChecks.Contains(cachedId))
                        {
                            return;
                        }
                    }

                    string itemName = "Unknown";
                    if (ItemDatabase.TryGetItem(itemID, out Item item))
                    {
                        itemName = item.UIData?.itemName ?? "Unknown";
                    }

                    // Defer to next frames - let Photon fully settle
                    _instance.StartCoroutine(DeferredTrackItem(itemName, itemID));
                }
                catch (Exception)
                {
                    // Silently ignore
                }
            }

            private static System.Collections.IEnumerator DeferredTrackItem(string itemName, ushort itemId)
            {
                yield return null;
                yield return null;
                
                if (_instance != null)
                {
                    _instance._log.LogDebug($"[PeakPelago] Tracking: {itemName} (ID: {itemId})");
                    _instance.TrackItemAcquisition(itemName, itemId);
                }
            }
        }

        /// <summary>
        /// PATCH ZOMBIFYING TOO BECAUSE IT DOESNT CALL DEATH FUNCTIONS >:[ THANKS ROOTS UPDATE
        /// </summary>
        [HarmonyPatch(typeof(Character), "RPCA_Die")]
        public static class CharacterRPCADiePatch
        {
            static void Postfix(Character __instance)
            {
                try
                {
                    if (_instance == null) return;
                    if (_instance._deathLinkService == null) return;
                    
                    // Check if Death Link is enabled
                    if (!_instance._deathLinkEnabled || !_instance.cfgDeathLinkEnabled.Value)
                    {
                        _instance._log.LogDebug("[PeakPelago] Death Link is disabled, not sending death link");
                        return;
                    }

                    if (_instance._isDyingFromDeathLink)
                    {

                        return;
                    }
            

                    
                    // Send when any player dies
                    if (_instance._deathLinkSendBehavior == 0)
                    {

                        _instance.SendDeathLink($"{_instance.cfgSlot.Value} died");
                    }
                    // Check if all players are dead
                    else if (_instance._deathLinkSendBehavior == 1)
                    {
                        bool allDead = true;
                        foreach (var character in Character.AllCharacters)
                        {
                            if (!character.data.dead)
                            {
                                allDead = false;
                                break;
                            }
                        }
                        
                        if (allDead)
                        {
                            _instance.SendDeathLink("Everyone died");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError("[PeakPelago] RPCA_Die patch error: " + ex.Message);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Character), "FinishZombifying")]
        public static class CharacterFinishZombifyingPatch
        {
            static void Postfix(Character __instance)
            {
                try
                {
                    if (_instance == null) return;
                    if (_instance._deathLinkService == null) return;
                    
                    // Check if Death Link is enabled
                    if (!_instance._deathLinkEnabled || !_instance.cfgDeathLinkEnabled.Value)
                    {
                        _instance._log.LogDebug("[PeakPelago] Death Link is disabled, not sending death link");
                        return;
                    }

                    if (_instance._isDyingFromDeathLink)
                    {
                        return;
                    }

                    if (_instance._deathLinkSendBehavior == 0)
                    {
                        _instance.SendDeathLink($"{_instance.cfgSlot.Value} turned into a zombie");
                    }
                    else if (_instance._deathLinkSendBehavior == 1)
                    {
                        bool allDead = true;
                        foreach (var character in Character.AllCharacters)
                        {
                            if (!character.data.dead && !character.data.zombified)
                            {
                                allDead = false;
                                break;
                            }
                        }
                        
                        if (allDead)
                        {
                            _instance.SendDeathLink("Everyone is dead or zombified");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError("[PeakPelago] FinishZombifying patch error: " + ex.Message);
                    }
                }
            }
        }
        [HarmonyPatch(typeof(AchievementManager), "ThrowAchievement")]
        public static class AchievementManagerThrowAchievementPatch
        {
            static bool Prefix(ACHIEVEMENTTYPE type)
            {
                try
                {
                    if (_instance == null)
                    {
                        return true;
                    }

                    if (!_instance._badgesHidden)
                    {
                        return true;
                    }


                    // Check if this badge was originally unlocked
                    bool wasOriginallyUnlocked = _instance._originalUnlockedBadges.Contains(type);

                    // Get the location name for this badge
                    var badgeMapping = _instance.GetBadgeToLocationMapping();
                    if (badgeMapping.TryGetValue(type, out string locationName))
                    {

                        // Report the check to Archipelago
                        _instance.ReportCheckByName(locationName);

                        // If it was originally unlocked, also award the badge normally
                        if (wasOriginallyUnlocked)
                        {
                            return true; // Allow normal badge awarding
                        }
                        else
                        {
                            return true; // Allow normal badge awarding for new badges too
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError("[PeakPelago] Badge patch error: " + ex.Message);
                    }
                }

                return true; // Allow normal processing
            }
        }

        [HarmonyPatch(typeof(AchievementManager), "IsAchievementUnlocked")]
        public static class AchievementManagerIsAchievementUnlockedPatch
        {
            static bool Prefix(ACHIEVEMENTTYPE achievementType, ref bool __result)
            {
                try
                {
                    if (_instance == null || !_instance._badgesHidden || _instance._session == null) return true;

                    var badgeMapping = _instance.GetBadgeToLocationMapping();
                    if (badgeMapping.ContainsKey(achievementType))
                    {
                        string locationName = badgeMapping[achievementType];
                        long locationId = _instance._session.Locations.GetLocationIdFromName("PEAK", locationName);

                        // Check if this location is checked in Archipelago
                        bool isCheckedInAP = _instance._session.Locations.AllLocationsChecked.Contains(locationId);
                        
                        __result = isCheckedInAP;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _instance?._log.LogError($"[Patch] IsAchievementUnlocked error: {ex.Message}");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(AchievementManager), "GetMaxAscent")]
        public static class AchievementManagerGetMaxAscentPatch
        {
            static bool Prefix(ref int __result)
            {
                try
                {
                    if (_instance == null || !_instance._badgesHidden) return true;

                    if (_instance._stateData.UnlockedAscents.Count > 0)
                    {
                        // Sort ascents and find the highest consecutive ascent starting from 1
                        var sortedAscents = _instance._stateData.UnlockedAscents.OrderBy(x => x).ToList();

                        int maxConsecutiveAscent = 0;

                        // Check for consecutive ascents starting from 1
                        for (int i = 1; i <= 8; i++) // Ascents go from 1 to 8
                        {
                            if (sortedAscents.Contains(i))
                            {
                                maxConsecutiveAscent = i;
                            }
                            else
                            {
                                // If we don't have this ascent, we can't go higher
                                break;
                            }
                        }

                        __result = maxConsecutiveAscent;
                        _instance._log.LogDebug("[PeakPelago] GetMaxAscent returning: " + __result + " (AP unlocked: " + string.Join(", ", sortedAscents) + ", consecutive up to: " + maxConsecutiveAscent + ")");
                        return false; // Skip original method
                    }
                    else
                    {
                        // No ascents unlocked via AP yet, return 0 (only base ascent available)
                        __result = 0;
                        _instance._log.LogDebug("[PeakPelago] GetMaxAscent returning: 0 (no AP ascents unlocked)");
                        return false; // Skip original method
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError("[PeakPelago] GetMaxAscent patch error: " + ex.Message);
                    }
                }

                return true; // Allow normal processing
            }
        }

        [HarmonyPatch(typeof(MountainProgressHandler), "CheckAreaAchievement")]
        public static class MountainProgressHandlerCheckAreaAchievementPatch
        {
            static void Postfix(ProgressPoint pointReached)
            {
                try
                {
                    if (_instance == null) return;

                    string peakName = pointReached?.title ?? "Unknown";
                    
                    // ONLY handle PEAK here - other segments are handled by GoToSegment patch
                    if (peakName.ToUpper() == "PEAK")
                    {
                        _instance.HandleAscentPeakReached(peakName);
                    }
                    else
                    {
                        _instance._log.LogDebug($"[PeakPelago] Ignoring progress point {peakName} - handled by GoToSegment");
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError($"[PeakPelago] CheckAreaAchievement patch error: {ex.Message}");
                    }
                }
            }
        }

        public void SetConnectionDetails(string server, string port, string slot, string password)
        {
            cfgServer.Value = server;
            if (int.TryParse(port, out int portNum))
            {
                cfgPort.Value = portNum;
            }
            cfgSlot.Value = slot;
            cfgPassword.Value = password;
        }


        // ===== Connection =====

        public void Connect()
        {
            if (_isConnecting) return;

            _isConnecting = true;
            _intentionalDisconnect = false;
            _status = "Connecting...";

            while (_itemQueue.TryDequeue(out _)) { }

            try
            {
                // Check for port changes before connecting
                string host = cfgServer.Value;
                string url = host.Contains(":") ? host : (host + ":" + cfgPort.Value);

                _session = ArchipelagoSessionFactory.CreateSession(url);

                // Log server messages to the BepInEx console
                _session.MessageLog.OnMessageReceived += OnApMessage;

                _session.Socket.SocketClosed += reason =>
                {
                    _log.LogWarning("[PeakPelago] Socket closed: " + reason);
                    _status = "Disconnected";

                    if (_intentionalDisconnect)
                    {

                        return;
                    }

                    _mainThreadActions.Enqueue(() =>
                    {
                        _wantReconnect = true;
                        _reconnectAttempts = 0;
                        _reconnectAttemptTime = Time.time + RECONNECT_DELAY;
                    });

                };

                // Capture values for the background thread
                string slotName = cfgSlot.Value;
                string password = string.IsNullOrEmpty(cfgPassword.Value) ? null : cfgPassword.Value;

                // Run the blocking TryConnectAndLogin on a background thread to avoid freezing the game
                new Thread(() =>
                {
                    try
                    {
                        var result = _session.TryConnectAndLogin(
                            "PEAK",
                            slotName,
                            ItemsHandlingFlags.AllItems,
                            null,
                            null,
                            null,                        // uuid
                            password,
                            true                         // requestSlotData
                        );

                        // Marshal all post-login work back to the main thread
                        _mainThreadActions.Enqueue(() => OnConnectResult(result, slotName));
                    }
                    catch (Exception ex)
                    {
                        _mainThreadActions.Enqueue(() =>
                        {
                            _status = "Error";
                            _log.LogError("[PeakPelago] Connect error: " + ex.GetBaseException().Message);
                            TryCloseSession();
                            _isConnecting = false;
                        });
                    }
                }) { IsBackground = true, Name = "PeakPelago-Connect" }.Start();
            }
            catch (Exception ex)
            {
                _status = "Error";
                _log.LogError("[PeakPelago] Connect error: " + ex.GetBaseException().Message);
                TryCloseSession();
                _isConnecting = false;
            }
        }

        public void SendUpdatedLinkTags()
        {
            if (_session?.Socket == null || !_session.Socket.Connected) return;
            var tags = new List<string>();
            if (_deathLinkEnabled && cfgDeathLinkEnabled.Value) tags.Add("DeathLink" + _deathLinkGroup);
            if (_ringLinkEnabled) tags.Add("RingLink");
            if (_hardRingLinkEnabled) tags.Add("HardRingLink");
            if (energyLinkEnabled) tags.Add("EnergyLink");
            if (_breathLinkEnabled) tags.Add("BreathLink");
            if (_trapLinkEnabled) tags.Add("TrapLink");
            if (_damageLinkEnabled) tags.Add("SharedDamage" + _damageLinkGroup);
            if (_knockbackLinkEnabled) tags.Add("KnockbackLink");

            try
            {
                var updatePacket = new ConnectUpdatePacket { Tags = tags.ToArray() };
                _session.Socket.SendPacket(updatePacket);
                _log.LogInfo($"[PeakPelago] Updated link tags: [{string.Join(", ", tags)}]");
            }
            catch (Exception ex)
            {
                _log.LogWarning("[PeakPelago] Failed to send link tags: " + ex.Message);
            }
        }

        public void CancelReconnect()
        {
            _wantReconnect = false;
            _reconnectAttempts = 0;
            _status = "Disconnected";
            _log.LogInfo("[PeakPelago] Reconnection cancelled by user.");
        }

        private static bool ReadBoolOption(LoginSuccessful result, string key, bool def = false)
        {
            return result.SlotData.ContainsKey(key) ? Convert.ToInt32(result.SlotData[key]) != 0 : def;
        }

        private static int ReadIntOption(LoginSuccessful result, string key, int def = 0)
        {
            return result.SlotData.ContainsKey(key) ? Convert.ToInt32(result.SlotData[key]) : def;
        }

        private void OnConnectResult(LoginResult result, string slotName)
        {
            try
            {
                if (!result.Successful)
                {
                    string msg = (result is LoginFailure lf) ? lf.Errors?.FirstOrDefault() ?? "Login failed." : "Login failed.";
                    _status = "Login failed";
                    _log.LogError("[PeakPelago] " + msg);
                    TryCloseSession();
                    return;
                }

                // Ask for datapackage for our game so helper name<->id lookups work
                _session.Socket.SendPacket(new GetDataPackagePacket { Games = new[] { "PEAK" } });
                _deathLinkService = _session.CreateDeathLinkService();

                _deathLinkService.OnDeathLinkReceived += (deathLink) =>
                {
                    try
                    {


                        if (deathLink.Source == slotName)
                        {
                            _log.LogDebug("[PeakPelago] Ignoring own death link");
                            return;
                        }

                        string cause = deathLink.Cause ?? "Death Link";
                        string source = deathLink.Source;
                        _mainThreadActions.Enqueue(() => HandleDeathLinkReceived(cause, source));
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"[PeakPelago] Error handling death link: {ex.Message}");
                    }
                };
                ///ITEM RECEIVED HANDLER
                _session.Items.ItemReceived += helper =>
                {
                    try
                    {
                        var info = helper.DequeueItem();
                        string itemName = helper.GetItemName(info.ItemId, info.ItemGame) ?? ("Item " + info.ItemId);
                        int currentIndex = helper.Index - 1;
                        bool isTrap = IsTrapItem(itemName);

                        _mainThreadActions.Enqueue(() =>
                        {
                            try
                            {
                                if (currentIndex > _stateData.LastProcessedItemIndex)
                                {
                                    _itemQueue.Enqueue((itemName, isTrap, currentIndex));

                                }
                                else
                                {
                                    _log.LogDebug($"[PeakPelago] Skipping already processed item #{currentIndex}: {itemName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                _log.LogError("[PeakPelago] Error queueing item: " + ex.Message);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[PeakPelago] ItemReceived handler error: " + ex.Message);
                    }
                };

                _session.Socket.SendPacket(new SyncPacket());
                if (_stateData.ReportedChecks.Count > 0)
                {
                    try
                    {
                        _session.Locations.CompleteLocationChecks(_stateData.ReportedChecks.ToArray());

                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning("[PeakPelago] Resubmit checks failed: " + ex.Message);
                    }
                }
                var loginResult = result as LoginSuccessful;
                if (loginResult != null && loginResult.SlotData != null)
                {
                    CheckAndHandleSessionChange(loginResult);
                    LoadState();
                    if (_session != null && _session.Items != null)
                    {
                        int actualItemCount = _session.Items.AllItemsReceived.Count;
                        if (_stateData.LastProcessedItemIndex > actualItemCount)
                        {
                            _log.LogWarning($"[PeakPelago] State file index ({_stateData.LastProcessedItemIndex}) exceeds actual items ({actualItemCount}) - resetting to {actualItemCount}");
                            _stateData.LastProcessedItemIndex = actualItemCount;
                        }
                    }



                    foreach (var kvp in loginResult.SlotData)
                    {

                    }


                    bool progressiveEnabled = false;
                    bool additionalEnabled = false;

                    List<string> tags = new List<string>();

                    if (loginResult.SlotData.ContainsKey("mountain_hints"))
                    {
                        try
                        {
                            _mountainHints.Clear();
                            var hintsData = loginResult.SlotData["mountain_hints"];
                            if (hintsData is JArray hintsArray)
                            {
                                foreach (var hint in hintsArray)
                                {
                                    _mountainHints.Add(new MountainHint
                                    {
                                        Location = hint["location"]?.ToString() ?? "Unknown",
                                        Player = hint["player"]?.ToString() ?? "Unknown",
                                        Game = hint["game"]?.ToString() ?? "Unknown",
                                        LocationId = hint["location_id"]?.ToObject<long>() ?? 0,
                                        PlayerSlot = hint["player_slot"]?.ToObject<int>() ?? 0,
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.LogError($"[PeakPelago] Error parsing mountain_hints: {ex.Message}");
                        }
                    }

                    SeedHintCache();

                    if (loginResult.SlotData.ContainsKey("breath_link"))
                    {
                        _breathLinkEnabled = ReadBoolOption(loginResult, "breath_link", _breathLinkEnabled);

                        if (_breathLinkEnabled)
                        {
                            tags.Add("BreathLink");
                        }
                    }

                    if (loginResult.SlotData.ContainsKey("ring_link"))
                    {
                        _ringLinkEnabled = ReadBoolOption(loginResult, "ring_link", _ringLinkEnabled);

                        if (_ringLinkEnabled )
                        {
                            tags.Add("RingLink");
                        }
                    }

                    if (loginResult.SlotData.ContainsKey("energy_link"))
                    {
                        energyLinkEnabled = ReadBoolOption(loginResult, "energy_link", energyLinkEnabled);

                        if (energyLinkEnabled)
                        {
                            tags.Add("EnergyLink");

                            _energyLinkTeamName = _session.ConnectionInfo.Team.ToString();
                        }
                    }


                    if (loginResult.SlotData.ContainsKey("hard_ring_link"))
                    {
                        _hardRingLinkEnabled = ReadBoolOption(loginResult, "hard_ring_link", _hardRingLinkEnabled);

                        if (_hardRingLinkEnabled)
                        {
                            tags.Add("HardRingLink");
                        }
                    }

                    if (loginResult.SlotData.ContainsKey("trap_link"))
                    {
                        _trapLinkEnabled = ReadBoolOption(loginResult, "trap_link", _trapLinkEnabled);

                        if (_trapLinkEnabled)
                        {
                            tags.Add("TrapLink");
                        }
                    }

                    if (loginResult.SlotData.ContainsKey("damage_link"))
                    {
                        _damageLinkEnabled = ReadBoolOption(loginResult, "damage_link", _damageLinkEnabled);
                        if (loginResult.SlotData.ContainsKey("damage_link_group"))
                        {
                            _damageLinkGroup = loginResult.SlotData["damage_link_group"]?.ToString() ?? "";
                        }

                        if (_damageLinkEnabled)
                        {
                            tags.Add("SharedDamage" + _damageLinkGroup);
                        }
                    }
                    _damageLinkService.Initialize(_session, _damageLinkEnabled, _damageLinkGroup, slotName);

                    if (loginResult.SlotData.ContainsKey("knockback_link"))
                    {
                        _knockbackLinkEnabled = ReadBoolOption(loginResult, "knockback_link", _knockbackLinkEnabled);

                        if (_knockbackLinkEnabled)
                        {
                            tags.Add("KnockbackLink");
                        }
                    }
                    _knockbackLinkService.Initialize(_session, _knockbackLinkEnabled, slotName);



                    if (loginResult.SlotData.ContainsKey("death_link"))
                    {
                        _deathLinkEnabled = ReadBoolOption(loginResult, "death_link", _deathLinkEnabled);
                        if (loginResult.SlotData.ContainsKey("death_link_group"))
                        {
                            _deathLinkGroup = loginResult.SlotData["death_link_group"]?.ToString() ?? "";
                        }

                        if (_deathLinkEnabled)
                        {
                            tags.Add("DeathLink" + _deathLinkGroup);
                            if (string.IsNullOrEmpty(_deathLinkGroup))
                            {
                                _deathLinkService.EnableDeathLink();
                            }
                            else
                            {
                                _session.Socket.PacketReceived += OnGroupedDeathLinkPacket;
                                _log.LogInfo($"[PeakPelago] Death Link using group tag: DeathLink{_deathLinkGroup}");
                            }
                        }
                    }

                    if (tags.Count > 0)
                    {
                        var updatePacket = new ConnectUpdatePacket
                        {
                            Tags = tags.ToArray()
                        };
                        _session.Socket.SendPacket(updatePacket);
                        //
                    }
                    if (_ringLinkEnabled)
                    {
                        _ringLinkService.Initialize(_session, _ringLinkEnabled);
                    }
                    if (_hardRingLinkEnabled)
                    {
                        _hardRingLinkService.Initialize(_session, _hardRingLinkEnabled);
                    }
                    if (_breathLinkEnabled)
                    {
                        _breathLinkService.Initialize(_session, _breathLinkEnabled);
                    }
                    if (energyLinkEnabled)
                    {
                        _energyLinkService.Initialize(_session, energyLinkEnabled, _energyLinkTeamName, this);
                        CampfireModelSpawner.SetEnergyLinkService(_energyLinkService);
                    }

                    _enabledTraps = new HashSet<string>();
                    if (_trapLinkEnabled && loginResult.SlotData.ContainsKey("active_traps"))
                    {
                        try
                        {
                            var activeTrapsData = loginResult.SlotData["active_traps"];
                            if (activeTrapsData is JObject activeTrapsObj)
                            {
                                foreach (var kvp in activeTrapsObj)
                                {
                                    string trapKey = kvp.Key;
                                    int weight = kvp.Value.ToObject<int>();
                                    string trapName = MapSlotKeyToTrapName(trapKey);
                                    if (weight > 0 && !string.IsNullOrEmpty(trapName))
                                    {
                                        _enabledTraps.Add(trapName);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.LogError($"[PeakPelago] Error parsing active_traps: {ex.Message}");
                            _enabledTraps = TrapTypeExtensions.GetAllTrapNames();
                        }
                    }
                    else if (_trapLinkEnabled)
                    {
                        _log.LogWarning("[PeakPelago] active_traps not found in slot data, enabling all traps");
                        _enabledTraps = TrapTypeExtensions.GetAllTrapNames();
                    }

                    // Always initialize - trap queue works with or without TrapLink
                    _trapLinkService.Initialize(
                        _session,
                        _trapLinkEnabled,
                        slotName,
                        _enabledTraps,
                        ApplyItemEffect
                    );
                    _goalNeedsAscent = false;
                    _goalNeedsBadges = false;
                    _goalNeedsIdol = false;
                    _goalNeedsSoul = false;
                    if (loginResult.SlotData.ContainsKey("goals"))
                    {
                        try
                        {
                            var goalsToken = JToken.FromObject(loginResult.SlotData["goals"]);
                            foreach (var goalEntry in goalsToken)
                            {
                                switch (goalEntry.ToString())
                                {
                                    case "Reach Peak": _goalNeedsAscent = true; break;
                                    case "Collect Badges": _goalNeedsBadges = true; break;
                                    case "24 Karat Badge": _goalNeedsIdol = true; break;
                                    case "Free The Soul": _goalNeedsSoul = true; break;
                                }
                            }
                            _log.LogInfo($"[PeakPelago] Goals: ascent={_goalNeedsAscent}, badges={_goalNeedsBadges}, idol={_goalNeedsIdol}, soul={_goalNeedsSoul}");
                        }
                        catch (Exception ex)
                        {
                            _log.LogError($"[PeakPelago] Error parsing goals: {ex.Message}");
                            _goalNeedsAscent = true;
                        }
                    }
                    else
                    {
                        _goalNeedsAscent = true;
                    }

                    _slotRequiredAscent = ReadIntOption(loginResult, "ascent_count", _slotRequiredAscent);

                    _slotRequiredBadges = ReadIntOption(loginResult, "badge_count", _slotRequiredBadges);

                    _deathLinkBehavior = ReadIntOption(loginResult, "death_link_behavior", _deathLinkBehavior);
                    _deathLinkSendBehavior = ReadIntOption(loginResult, "death_link_send_behavior", _deathLinkSendBehavior);

                    progressiveEnabled = ReadBoolOption(loginResult, "progressive_stamina", progressiveEnabled);

                    additionalEnabled = ReadBoolOption(loginResult, "additional_stamina_bars", additionalEnabled);
                    _itemSanityEnabled = ReadBoolOption(loginResult, "item_sanity", _itemSanityEnabled);
                    _lootSanityMode = ReadIntOption(loginResult, "loot_sanity", _lootSanityMode);
                    if (loginResult.SlotData.ContainsKey("loot_biome_assignments"))
                    {
                        try
                        {
                            var raw = loginResult.SlotData["loot_biome_assignments"];
                            if (raw is Newtonsoft.Json.Linq.JObject jObj)
                            {
                                _lootBiomeAssignments = jObj.ToObject<Dictionary<string, int>>();
                                _log.LogInfo($"[PeakPelago] Loaded {_lootBiomeAssignments.Count} loot biome assignments from slot data");
                            }
                            else if (raw is Dictionary<string, object> dict)
                            {
                                _lootBiomeAssignments = new Dictionary<string, int>();
                                foreach (var kvp in dict)
                                    _lootBiomeAssignments[kvp.Key] = Convert.ToInt32(kvp.Value);
                                _log.LogInfo($"[PeakPelago] Loaded {_lootBiomeAssignments.Count} loot biome assignments from slot data");
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.LogWarning($"[PeakPelago] Failed to parse loot_biome_assignments: {ex.Message}");
                        }
                    }
                    _logicalScoutStatue = ReadBoolOption(loginResult, "logical_scout_statue", _logicalScoutStatue);
                    _scoutAmuletSanity = ReadBoolOption(loginResult, "scout_amulet_sanity", _scoutAmuletSanity);

                    LoadOfflineChecks();
                    if (_stateData.OfflineChecks.Count > 0)
                    {
                        ReportOfflineChecks();
                    }
                    _reconnectAttempts = 0;
                    _wantReconnect = false;

                    RecoverAscentUnlocks();
                    RecoverProgressiveMountain();
                    RecoverEnduranceUpgrades();
                    EvaluateGoalCompletion();

                    _staminaManager.Initialize(progressiveEnabled, additionalEnabled);
                    _mainThreadActions.Enqueue(() =>
                    {
                        _wasInAirport = false;
                        StartCoroutine(WaitForCharacterAndRebuild());
                    });


                }
                else
                {
                    _log.LogWarning("[PeakPelago] No slot data available, using default stamina settings");
                    _staminaManager.Initialize(false, false);
                }
                SaveState();
                _status = "Connected";
                _wantReconnect = false;
                UnlockedItemsManager.Initialize(_log, _session, _itemSanityEnabled, _lootSanityMode, _logicalScoutStatue, _progressiveMountainCount, _stateData, _lootBiomeAssignments, _scoutAmuletSanity);
                _notifications.ShowConnected();
                if (LootData.AllSpawnWeightData != null)
                {

                    UnlockedItemsManager.RefreshLootTables();
                }


            }
            catch (Exception ex)
            {
                _status = "Error";
                _log.LogError("[PeakPelago] Connect error: " + ex.GetBaseException().Message);
                TryCloseSession();
            }
            finally
            {
                _isConnecting = false;
            }
        }

        private System.Collections.IEnumerator RefreshLootTablesAfterStartup()
        {
            yield return new WaitForSeconds(2f);
            while (!_itemQueue.IsEmpty)
            {
                yield return new WaitForSeconds(0.5f);
            }
            UnlockedItemsManager.RefreshLootTables();
        }

        private void RebuildCollectedBadgesFromChecks()
        {
            try
            {
                if (_session == null)
                {
                    _log.LogWarning("[PeakPelago] Cannot rebuild badges - no session");
                    return;
                }
                
                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Cannot rebuild badges - no local character");
                    return;
                }
                
                _collectedBadges.Clear();
                
                var badgeMapping = GetBadgeToLocationMapping();
                var reverseBadgeMapping = badgeMapping.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
                
                // Get all checked locations from the AP session
                var checkedLocations = _session.Locations.AllLocationsChecked;
                
                
                
                // Check all checked locations for badge locations
                foreach (long checkId in checkedLocations)
                {
                    try
                    {
                        string locationName = null;
                        
                        try
                        {
                            locationName = _session.Locations.GetLocationNameFromId(checkId, "PEAK");
                        }
                        catch (Exception ex)
                        {
                            _log.LogDebug($"[PeakPelago] Failed to get location name for check {checkId}: {ex.Message}");
                        }
                        
                        if (!string.IsNullOrEmpty(locationName) && reverseBadgeMapping.ContainsKey(locationName))
                        {
                            ACHIEVEMENTTYPE badgeType = reverseBadgeMapping[locationName];
                            _collectedBadges.Add(badgeType);
                            
                            // Set the badge in the character's badge status array
                            int badgeIndex = -1;
                            var badgeData = GUIManager.instance?.mainBadgeManager?.badgeData;
                            if (badgeData != null)
                            {
                                for (int i = 0; i < badgeData.Length; i++)
                                {
                                    if (badgeData[i] != null && badgeData[i].linkedAchievement == badgeType)
                                    {
                                        badgeIndex = i;
                                        break;
                                    }
                                }
                            }
                            if (badgeIndex >= 0 && badgeIndex < Character.localCharacter.data.badgeStatus.Length)
                            {
                                Character.localCharacter.data.badgeStatus[badgeIndex] = true;
                                
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"[PeakPelago] Error rebuilding badge for check {checkId}: {ex.Message}");
                    }
                }
                
                // Update the badge visuals
                if (Character.localCharacter != null)
                {
                    var badgeUnlocker = Character.localCharacter.GetComponent<BadgeUnlocker>();
                    badgeUnlocker?.BadgeUnlockVisual();
                }
                
                
                
                if (_collectedBadges.Count >= _slotRequiredBadges)
                {
                    ReportCheckByName("All Badges Collected");
                }

                if (_collectedBadges.Contains(ACHIEVEMENTTYPE.TwentyFourKaratBadge))
                {
                    if (!_stateData.GoalIdolDunked)
                    {
                        _stateData.GoalIdolDunked = true;
                        SaveState();
                    }
                    ReportCheckByName("Idol Dunked");
                }

                EvaluateGoalCompletion();
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error rebuilding collected badges: {ex.Message}");
            }
        }
        private void RecoverAscentUnlocks()
        {
            try
            {
                RebuildAscentUnlocks();
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error recovering ascent unlocks: {ex.Message}");
            }
        }
        
        private static Dictionary<string, string> _slotKeyToTrapNameMapping;

        private string MapSlotKeyToTrapName(string slotKey)
        {
            if (_slotKeyToTrapNameMapping == null)
            {
                _slotKeyToTrapNameMapping = new Dictionary<string, string>
                {
                    { "instant_death_trap", "Instant Death Trap" },
                { "items_to_bombs", "Items to Bombs" },
                { "pokemon_trivia_trap", "Pokemon Trivia Trap" },
                { "blackout_trap", "Blackout Trap" },
                { "spawn_bee_swarm", "Spawn Bee Swarm" },
                { "banana_peel_trap", "Banana Peel Trap" },
                { "minor_poison_trap", "Minor Poison Trap" },
                { "poison_trap", "Poison Trap" },
                { "deadly_poison_trap", "Deadly Poison Trap" },
                { "tornado_trap", "Tornado Trap" },
                { "swap_trap", "Swap Trap" },
                { "nap_time_trap", "Nap Time Trap" },
                { "hungry_hungry_camper_trap", "Hungry Hungry Camper Trap" },
                { "balloon_trap", "Balloon Trap" },
                { "slip_trap", "Slip Trap" },
                { "freeze_trap", "Freeze Trap" },
                { "cold_trap", "Cold Trap" },
                { "hot_trap", "Hot Trap" },
                { "injury_trap", "Injury Trap" },
                { "cactus_ball_trap", "Cactus Ball Trap" },
                { "yeet_trap", "Yeet Trap" },
                { "tumbleweed_trap", "Tumbleweed Trap" },
                { "zombie_horde_trap", "Zombie Horde Trap" },
                { "gust_trap", "Gust Trap" },
                { "mandrake_trap", "Mandrake Trap" },
                { "fungal_infection_trap", "Fungal Infection Trap" },
                { "fear_trap", "Fear Trap" },
                { "scoutmaster_trap", "Scoutmaster Trap" },
                { "zoom_trap", "Zoom Trap" },
                { "screen_flip_trap", "Screen Flip Trap" },
                { "drop_everything_trap", "Drop Everything Trap"},
                { "pixel_trap", "Pixel Trap" },
                { "eruption_trap", "Eruption Trap" },
                { "beetle_horde_trap", "Beetle Horde Trap" },
                    { "custom_trivia_trap", "Custom Trivia Trap" },
                };
            }

            return _slotKeyToTrapNameMapping.TryGetValue(slotKey, out string trapName) ? trapName : null;
        }

        public void TryCloseSession()
        {
            try
            {
                if (_session != null)
                {
                    try
                    {
                        _session.MessageLog.OnMessageReceived -= OnApMessage;
                    }
                    catch (Exception ex)
                    {
                        _log.LogDebug("[PeakPelago] Error unsubscribing from messages: " + ex.Message);
                    }
                    
                    try
                    {
                        if (_session.Socket != null && _session.Socket.Connected)
                        {
                            _session.Socket.Disconnect();
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogDebug("[PeakPelago] Error disconnecting socket: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("[PeakPelago] Error in TryCloseSession: " + ex.Message);
            }
            finally
            {
                _session = null;
                _hasRebuiltBadges = false;
                _status = "Disconnected";
            }
        }

        private void HandleHintMessage(HintItemSendLogMessage hintMsg)
        {
            if (!hintMsg.IsRelatedToActivePlayer || _session == null) return;
            if (hintMsg.Receiver.Slot != _session.ConnectionInfo.Slot) return;

            string receivingGame = _session.ConnectionInfo.Game;
            string unlockName = _session.Items.GetItemName(hintMsg.Item.ItemId, receivingGame);
            if (string.IsNullOrEmpty(unlockName)) return;

            bool isFound = hintMsg.IsFound;
            string display = null;
            if (!isFound)
            {
                string findingPlayerName = _session.Players.GetPlayerName(hintMsg.Sender.Slot);
                string findingGame = _session.Players.GetPlayerInfo(hintMsg.Sender.Slot)?.Game ?? "???";
                string locName = _session.Locations.GetLocationNameFromId(hintMsg.Item.LocationId, findingGame) ?? $"Location {hintMsg.Item.LocationId}";
                display = $"{findingPlayerName}'s {findingGame}: {locName}";
            }

            _mainThreadActions.Enqueue(() =>
            {
                bool changed;
                if (isFound)
                {
                    changed = _activeHints.Remove(unlockName);
                }
                else if (!_activeHints.TryGetValue(unlockName, out string existing) || existing != display)
                {
                    _activeHints[unlockName] = display;
                    changed = true;
                }
                else
                {
                    changed = false;
                }

                if (changed)
                {
                    OnHintsChanged?.Invoke();
                }
            });
        }

        private void HandleCollectedItemForHints(ItemSendLogMessage itemMsg)
        {
            if (!itemMsg.IsRelatedToActivePlayer || _session == null) return;
            if (itemMsg.Receiver.Slot != _session.ConnectionInfo.Slot) return;

            string receivingGame = _session.ConnectionInfo.Game;
            string unlockName = _session.Items.GetItemName(itemMsg.Item.ItemId, receivingGame);
            if (string.IsNullOrEmpty(unlockName)) return;

            _mainThreadActions.Enqueue(() =>
            {
                if (_activeHints.Remove(unlockName))
                {
                    OnHintsChanged?.Invoke();
                }
            });
        }

        public void SeedHintCache()
        {
            if (_session == null) return;
            int slot = _session.ConnectionInfo.Slot;
            _session.Hints.GetHintsAsync((hints) =>
            {
                if (hints == null) return;
                var seeded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in hints)
                {
                    if (h.ReceivingPlayer != slot || h.Found) continue;
                    string receivingGame = _session.Players.GetPlayerInfo(h.ReceivingPlayer)?.Game ?? _session.ConnectionInfo.Game;
                    string unlockName = _session.Items.GetItemName(h.ItemId, receivingGame);
                    if (string.IsNullOrEmpty(unlockName)) continue;
                    string findingPlayerName = _session.Players.GetPlayerName(h.FindingPlayer);
                    string findingGame = _session.Players.GetPlayerInfo(h.FindingPlayer)?.Game ?? "???";
                    string locName = _session.Locations.GetLocationNameFromId(h.LocationId, findingGame) ?? $"Location {h.LocationId}";
                    seeded[unlockName] = $"{findingPlayerName}'s {findingGame}: {locName}";
                }
                _mainThreadActions.Enqueue(() =>
                {
                    _activeHints.Clear();
                    foreach (var kvp in seeded) _activeHints[kvp.Key] = kvp.Value;
                    OnHintsChanged?.Invoke();
                });
            });
        }

        private void OnApMessage(LogMessage msg)
        {
            try
            {
                switch (msg)
                {
                    case HintItemSendLogMessage hintMsg:
                        HandleHintMessage(hintMsg);
                        return;
                    case ItemSendLogMessage itemSendMsg:
                        HandleCollectedItemForHints(itemSendMsg);

                        var sender = itemSendMsg.Sender;
                        var receiver = itemSendMsg.Receiver;
                        var item = itemSendMsg.Item;

                        string senderName = sender.Name ?? $"Player {sender.Slot}";
                        string receiverName = receiver.Name ?? $"Player {receiver.Slot}";
                        string itemName = item.ItemName ?? $"Item {item.ItemId}";

                        if (item.ItemGame != "PEAK" && item.ItemGame != null)
                        {
                            itemName = $"{itemName} ({item.ItemGame})";
                        }

                        var flags = item.Flags;

                        _mainThreadActions.Enqueue(() =>
                        {
                            _notifications?.ShowItemNotification(senderName, receiverName, itemName, flags);
                        });


                        return;

                    default:
                        string msgString = msg.ToString();
                        if (msgString.Contains("Cheat console:")) return;
                        
                        
                        _mainThreadActions.Enqueue(() =>
                        {
                            _notifications?.ShowSimpleMessage(msgString);
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in OnApMessage: {ex.Message}");
            }
        }

        // ===== Trap Queue UI =====
        private GUIStyle _trapQueueTitleStyle;
        private GUIStyle _trapQueueActiveStyle;
        private GUIStyle _trapQueueInactiveStyle;
        private Font _trapQueueFont;
        private bool _trapQueueStylesInitialized = false;

        private void InitTrapQueueStyles()
        {
            if (_trapQueueStylesInitialized) return;

            _trapQueueFont = TriviaUIHelper.LoadCustomFont();

            _trapQueueTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new UnityEngine.Color(1f, 1f, 1f, 0.9f) }
            };
            if (_trapQueueFont != null) _trapQueueTitleStyle.font = _trapQueueFont;

            _trapQueueActiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new UnityEngine.Color(1f, 1f, 1f, 1f) }
            };
            if (_trapQueueFont != null) _trapQueueActiveStyle.font = _trapQueueFont;

            _trapQueueInactiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new UnityEngine.Color(0.5f, 0.5f, 0.5f, 0.7f) }
            };
            if (_trapQueueFont != null) _trapQueueInactiveStyle.font = _trapQueueFont;

            _trapQueueStylesInitialized = true;
        }

        private void OnGUI()
        {
            if (_trapLinkService == null) return;

            var (activeTrap, queued) = _trapLinkService.GetQueueState(5);

            // Nothing to show
            if (activeTrap == null && queued.Count == 0) return;

            InitTrapQueueStyles();

            float panelWidth = 350f;
            float lineHeight = 24f;
            float titleHeight = 30f;
            float padding = 10f;
            float x = Screen.width - panelWidth - padding;
            float y = Screen.height * 0.4f;

            // Title
            GUI.Label(new Rect(x, y, panelWidth, titleHeight), "TRAP QUEUE", _trapQueueTitleStyle);
            y += titleHeight;

            // Active trap (white)
            if (activeTrap != null)
            {
                GUI.Label(new Rect(x, y, panelWidth, lineHeight), $"> {activeTrap}", _trapQueueActiveStyle);
                y += lineHeight;
            }

            // Queued traps (greyed out)
            foreach (var trap in queued)
            {
                GUI.Label(new Rect(x, y, panelWidth, lineHeight), trap, _trapQueueInactiveStyle);
                y += lineHeight;
            }

            // Show remaining count if more than 5
            int remaining = _trapLinkService.QueueCount - queued.Count;
            if (remaining > 0)
            {
                GUI.Label(new Rect(x, y, panelWidth, lineHeight), $"...and {remaining} more", _trapQueueInactiveStyle);
            }
        }

        private void Update()
        {
            try
            {
                if (RunEnded && LoadingScreenHandler.loading)
                {
                    RunEnded = false;
                }

                bool isCurrentlyConnected = _session != null && _session.Socket != null && _session.Socket.Connected;
                if (_wasConnected && !isCurrentlyConnected)
                {
                    _log.LogWarning("[PeakPelago] Connection lost detected in Update()");
                    _status = "Disconnected";
                    
                    if (_intentionalDisconnect)
                    {
                        
                        _wasConnected = false;
                    }
                    else
                    {
                        _wantReconnect = true;
                        _reconnectAttempts = 0;
                        _reconnectAttemptTime = Time.time + RECONNECT_DELAY;
                    }
                }
                
                _wasConnected = isCurrentlyConnected;
                if (_wantReconnect && !_isConnecting && Time.time >= _reconnectAttemptTime)
                {
                    if (_reconnectAttempts < MAX_RECONNECT_ATTEMPTS)
                    {
                        _reconnectAttempts++;
                        
                        
                        Connect();
                        
                        // Schedule next attempt if this one fails
                        _reconnectAttemptTime = Time.time + RECONNECT_DELAY;
                    }
                    else
                    {
                        _log.LogError($"[PeakPelago] Max reconnection attempts ({MAX_RECONNECT_ATTEMPTS}) reached. Giving up.");
                        _wantReconnect = false;
                        _status = "Failed to reconnect";
                    }
                }
                _trapLinkService?.Update();
                ProcessItemQueue();
                CampfireModelSpawner.CleanupDestroyedCampfires();

                {
                    string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    bool isInAirport = currentScene.Contains("Airport");
                    bool isInLevel = currentScene.StartsWith("Level_");
                    if (isInAirport && !_wasInAirport && _session != null)
                    {
                        StartCoroutine(WaitForCharacterAndRebuild());
                    }
                    if (isInLevel && !_wasInLevel && _session != null)
                    {
                        StartCoroutine(WaitForCharacterAndCleanShore());
                    }
                    _wasInAirport = isInAirport;
                    _wasInLevel = isInLevel;
                }
                if (_stateManager.StateDirty && !_stateManager.IsSaving && Time.time - _stateManager.LastStateSave > _stateManager.SaveInterval)
                {
                    SaveState();
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in Update: {ex.Message}");
                _log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
        private System.Collections.IEnumerator WaitForCharacterAndRebuild()
        {
            while (Character.localCharacter == null)
            {
                yield return new WaitForSeconds(0.5f);
            }

            if (_session != null && _staminaManager != null && _staminaManager.IsProgressiveStaminaEnabled())
            {
                ApplyProgressiveStamina();
            }

            if (!_hasRebuiltBadges)
            {
                RebuildCollectedBadgesFromChecks();
                _hasRebuiltBadges = true;
            }
        }

        private System.Collections.IEnumerator WaitForCharacterAndCleanShore()
        {
            while (Character.localCharacter == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(1f);

            DestroyLockedHandPlacedItems();
        }

        private static readonly string[] HandPlacedShoreItems = { "Binoculars", "BingBong", "Guidebook", "Flare", "Compass", "Bugle", "Lantern", "Frisbee", "Shell Big" };

        private void DestroyLockedHandPlacedItems()
        {
            try
            {
                if (!_itemSanityEnabled) return;
                if (!PhotonNetwork.IsMasterClient) return;
                if (_session == null) return;

                HashSet<ushort> unlocked = UnlockedItemsManager.GetUnlockedItemIds();

                var items = UnityEngine.Object.FindObjectsByType<Item>(FindObjectsSortMode.None);
                int destroyed = 0;
                foreach (var item in items)
                {
                    if (item == null || item.UIData == null) continue;
                    string itemName = item.UIData.itemName;
                    if (string.IsNullOrEmpty(itemName)) continue;
                    if (System.Array.IndexOf(HandPlacedShoreItems, itemName) < 0) continue;

                    if (!ItemIdMappings.TryGetId(itemName, out ushort itemId)) continue;
                    if (unlocked.Contains(itemId)) continue;

                    try
                    {
                        if (item.photonView != null && item.photonView.IsMine)
                        {
                            PhotonNetwork.Destroy(item.gameObject);
                        }
                        else if (item.photonView == null)
                        {
                            UnityEngine.Object.Destroy(item.gameObject);
                        }
                        else
                        {
                            PhotonNetwork.Destroy(item.gameObject);
                        }
                        destroyed++;
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning($"[PeakPelago] Failed to destroy locked item {itemName}: {ex.Message}");
                    }
                }

                _log.LogInfo($"[PeakPelago] Destroyed {destroyed} locked hand-placed shore items");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in DestroyLockedHandPlacedItems: {ex.Message}");
            }
        }

        /// <summary>
        /// Process items from the queue gradually to prevent overwhelming the game
        /// </summary>
        private void ProcessItemQueue()
        {
            while (_mainThreadActions.TryDequeue(out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    _log.LogError($"[PeakPelago] Error in main thread action: {ex.Message}");
                }
            }

            if (!OriginalLootWeights.HasCaptured)
            {
                LootData.PopulateLootData();
                
                OriginalLootWeights.CaptureOriginalWeights();
                UnlockedItemsManager.CheckDeferredRefresh();
            }
    
            if (_itemQueue.IsEmpty || LoadingScreenHandler.loading)
            {
                return;
            }

            // Fast-track unlock items - they don't spawn anything, so process them all immediately
            bool processedAnyUnlocks = false;
            while (_itemQueue.TryPeek(out var unlockPeek) && unlockPeek.itemName.EndsWith(" Unlock"))
            {
                if (!_itemQueue.TryDequeue(out var unlockItem)) break;
                try
                {
                    ApplyItemEffect(unlockItem.itemName);
                    _stateData.LastProcessedItemIndex = unlockItem.itemIndex;
                    processedAnyUnlocks = true;
                }
                catch (Exception ex)
                {
                    _log.LogError($"[PeakPelago] Error processing unlock {unlockItem.itemName}: {ex.Message}");
                }
            }
            if (processedAnyUnlocks)
            {
                SaveState();
                UnlockedItemsManager.RequestRefresh();
            }

            // Normal cooldown-gated processing for non-unlock items
            if (Time.time - _lastItemProcessed < ITEM_PROCESSING_COOLDOWN)
            {
                return;
            }

            if (!_itemQueue.TryPeek(out var peekedItem))
            {
                return;
            }

            var (itemName, isTrap, itemIndex) = peekedItem;
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            bool isInLevel = currentScene.StartsWith("Level_");
            bool isInAirport = currentScene.Contains("Airport");
            bool isProgressionItem = itemName.Contains("Progressive") || itemName.Contains("Badge");

            if (isProgressionItem)
            {
                if (!isInAirport && !isInLevel)
                {
                    return;
                }
            }
            else
            {
                if (!isInLevel)
                {
                    return;
                }
            }

            if (!_itemQueue.TryDequeue(out var item))
            {
                return;
            }

            try
            {
                

                if (item.isTrap && _trapLinkService != null)
                {
                    _trapLinkService.QueueTrap(item.itemName);
                }
                else
                {
                    ApplyItemEffect(item.itemName);
                }
                _stateData.LastProcessedItemIndex = item.itemIndex;
                SaveState();
                _lastItemProcessed = Time.time;
                UnlockedItemsManager.RequestRefresh();
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error processing queued item {item.itemName}: {ex.Message}");
                _lastItemProcessed = Time.time;
            }
        }

        // ===== Tiny state persistence (no Unity JsonUtility dependency) ======
        private void CheckAndHandleSessionChange(LoginSuccessful loginResult)
        {
            try
            {
                if (loginResult == null || loginResult.SlotData == null)
                {
                    _log.LogWarning("[PeakPelago] No slot data to check session");
                    return;
                }

                string currentSessionId = null;
                if (loginResult.SlotData.ContainsKey("session_id"))
                {
                    currentSessionId = loginResult.SlotData["session_id"].ToString();
                }

                CampfireModelSpawner.SetSessionSeed(currentSessionId);
                PublishSessionSeedToRoom();

                _stateManager.CheckAndHandleSessionChange(currentSessionId, ClearCacheForSessionChange);
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error checking session change: " + ex.Message);
            }
        }

        private void ClearCacheForSessionChange()
        {
            _stateData.Clear();
            _staminaManager?.Initialize(false, false);
            
        }

        private void LoadState()
        {
            _stateManager.Load(_stateData);
            BasketballHoopScorePatch.SetTotalCount(_stateData.TotalBasketsScored);
        }
        private void SaveState()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (_session == null || _session.Socket == null || !_session.Socket.Connected) return;
            _stateManager.Save(_stateData);
        }
        private void SaveOfflineChecks()
        {
            _stateManager.SaveOfflineChecks(_stateData.OfflineChecks);
        }
        private void LoadOfflineChecks()
        {
            _stateManager.LoadOfflineChecks(_stateData.OfflineChecks);
        }

        private void ReportOfflineChecks()
        {
            try
            {
                if (_stateData.OfflineChecks.Count == 0)
                    return;

                

                var checksToReport = _stateData.OfflineChecks.ToArray();
                var successfulChecks = new List<long>();

                foreach (long checkId in checksToReport)
                {
                    try
                    {
                        if (_stateData.ReportedChecks.Contains(checkId))
                        {
                            successfulChecks.Add(checkId);
                            continue;
                        }

                        _session.Locations.CompleteLocationChecks(new long[] { checkId });
                        _stateData.ReportedChecks.Add(checkId);
                        successfulChecks.Add(checkId);

                        string locationName = "Unknown";
                        try { locationName = _session.Locations.GetLocationNameFromId(checkId, "PEAK"); }
                        catch { }

                        
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning($"[PeakPelago] Failed to report queued check {checkId}: {ex.Message}");
                    }
                }

                foreach (long checkId in successfulChecks)
                {
                    _stateData.OfflineChecks.Remove(checkId);
                }

                if (_stateData.OfflineChecks.Count > 0)
                {
                    _log.LogWarning($"[PeakPelago] {_stateData.OfflineChecks.Count} checks still queued (failed to report)");
                    SaveOfflineChecks();
                }
                else
                {
                    _stateManager.DeleteOfflineChecksFile();
                }

                SaveState();
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error reporting offline checks: " + ex.Message);
            }
        }
        /// <summary>Handle achievement events to report badge checks to Archipelago</summary>
        private void OnAchievementThrown(ACHIEVEMENTTYPE achievementType)
        {
            try
            {
                
                // Get the badge to location mapping
                var badgeMapping = GetBadgeToLocationMapping();

                // Check if this achievement corresponds to an Archipelago location
                if (badgeMapping.TryGetValue(achievementType, out string locationName))
                {
                    ReportCheckByName(locationName);

                    // Track this badge as collected
                    _collectedBadges.Add(achievementType);
                    //if enough badges collected, report "All Badges Collected" location
                    if (_collectedBadges.Count >= _slotRequiredBadges)
                    {
                        
                        ReportCheckByName("All Badges Collected");
                    }
                    if (achievementType == ACHIEVEMENTTYPE.TwentyFourKaratBadge)
                    {
                        
                        ReportCheckByName("Idol Dunked");
                    }
                }
                else
                {
                    _log.LogDebug("[PeakPelago] Achievement " + achievementType + " is not tracked by Archipelago");
                }

                // Check for goal completion
                CheckForGoalCompletion(achievementType);
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error handling achievement event: " + ex.Message);
            }
        }

        // ===== Item Acquisition Event Handling =====

        /// <summary>Handle item request events to track item acquisitions</summary>
        private void OnSoulFreed(int phase)
        {
            try
            {
                // Phase 0 is the pre-stinger beat; phase 1 is the state change the game itself gates on
                if (phase != 1) return;

                _log.LogInfo("[PeakPelago] Scoutmaster's soul freed in the Nadir");
                ReportCheckByName("Soul Freed");
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error handling soul freed: " + ex.Message);
            }
        }

        private void OnItemRequested(Item item, Character character)
        {
            try
            {
                if (character != null && item != null)
                {
                    TrackItemAcquisition(item.UIData.itemName, item.itemID);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error handling item request event: " + ex.Message);
            }
        }

        public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            _log.LogWarning("[PeakPelago] Player left room: " + otherPlayer.NickName);
        }

        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {

        }

        public void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps)
        {

        }

        public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            _log.LogInfo("[PeakPelago] Master client switched to: " + newMasterClient.NickName);
        }
    }
}