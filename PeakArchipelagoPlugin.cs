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

namespace Peak.AP
{
    [BepInPlugin("com.mickemoose.peak.ap", "Peak Archipelago", "0.4.9")]
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
        private ConfigEntry<string> cfgGameId;
        private ConfigEntry<bool> cfgAutoReconnect;
        private ConfigEntry<int> cfgGoalType;
        private ConfigEntry<int> cfgRequiredBadges;
        private ConfigEntry<int> cfgRequiredAscent;

        // ===== Session =====
        private ArchipelagoSession _session;
        private string _status = "Disconnected";
        private bool _isConnecting;
        private bool _wantReconnect;
        private string _currentPort = "";
        private string StateFilePath { get { return Path.Combine(Paths.ConfigPath, "Peak.AP.state." + _currentPort.Replace(":", "_") + ".txt"); } }
        private int _lastProcessedItemIndex = 0;
        private readonly HashSet<long> _reportedChecks = new HashSet<long>();

        // ===== Archipelago Item Receiving Debug =====
        private int _itemsReceivedFromAP = 0;
        private string _lastReceivedItemName = "None";
        private DateTime _lastReceivedItemTime = DateTime.MinValue;

        // ===== Debug counter for luggage opens =====
        private int _luggageOpenedCount = 0;
        private int _luggageOpenedThisRun = 0;

        // ===== State persistence =====
        private int _totalLuggageOpened = 0;
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

        // ===== Ascent Management =====
        private int _originalMaxAscent = 0;
        private int _slotGoalType = 0;
        private int _slotRequiredAscent = 0;
        private int _slotRequiredBadges = 20;
        private HashSet<int> _unlockedAscents = new HashSet<int>(); // Track which ascents are unlocked via AP items

        // ===== AP Link Management =====
        private RingLinkService _ringLinkService;
        private HardRingLinkService _hardRingLinkService;
        private TrapLinkService _trapLinkService;
        private DeathLinkService _deathLinkService;
        private EnergyLinkService _energyLinkService;
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
        private LinkedList<(string itemName, bool isTrap, int itemIndex)> _itemQueue = new LinkedList<(string, bool, int)>();
        private float _lastItemProcessed = 0f;
        private const float ITEM_PROCESSING_COOLDOWN = 1f;
        private void Awake()
        {
            try
            {
                _log = Logger;
                _instance = this;
                _log.LogInfo("[PeakPelago] Plugin is initializing...");
                cfgServer = Config.Bind("Connection", "Server", "archipelago.gg", "Host, or host:port");
                cfgPort = Config.Bind("Connection", "Port", 38281, "Port (ignored if Server already contains :port)");
                cfgSlot = Config.Bind("Connection", "Slot", "Player", "Your AP slot name");
                cfgPassword = Config.Bind("Connection", "Password", "", "Room password (optional)");
                cfgGameId = Config.Bind("Connection", "GameId", "PEAK", "Game ID (must match the room)");
                cfgAutoReconnect = Config.Bind("Connection", "AutoReconnect", true, "Try to reconnect when socket closes");
                cfgGoalType = Config.Bind("Goal", "Type", 0, "Goal type: 0=Reach Peak, 1=All Badges, 2=24 Karat Badge");
                cfgRequiredBadges = Config.Bind("Goal", "BadgeCount", 20, "Number of badges required for Complete All Badges goal");
                cfgRequiredAscent = Config.Bind("Goal", "RequiredAscent", 4, "Ascent level required for Reach Peak goal (0-7)");
                _notifications = new ArchipelagoNotificationManager(_log, cfgSlot.Value);
                _staminaManager = new ProgressiveStaminaManager(_log);
                CharacterGetMaxStaminaPatch.SetStaminaManager(_staminaManager);
                CharacterClampStaminaPatch.SetStaminaManager(_staminaManager);
                CharacterHandleLifePatch.SetStaminaManager(_staminaManager);
                StaminaBarUpdatePatch.SetStaminaManager(_staminaManager);
                CharacterHandlePassedOutPatch.SetStaminaManager(_staminaManager);
                BarAfflictionUpdateAfflictionPatch.SetStaminaManager(_staminaManager);
                BarAfflictionChangeAfflictionPatch.SetStaminaManager(_staminaManager);
                _ringLinkService = new RingLinkService(_log, _notifications);
                _hardRingLinkService = new HardRingLinkService(_log, _notifications);
                _trapLinkService = new TrapLinkService(_log, _notifications);
                _energyLinkService = new EnergyLinkService(_log, _notifications);
                CampfireModelSpawner.SetEnergyLinkService(_energyLinkService);
                SwapTrapEffect.Initialize(_log, this);
                AfflictionTrapEffect.Initialize(_log);
                PokemonTriviaTrapEffect.Initialize(_log, this);
                BlackoutTrapEffect.Initialize(_log, this);
                CampfireModelSpawner.Initialize(_log);
                CheckAndHandlePortChange();
                _ui = gameObject.AddComponent<ArchipelagoUI>();
                _ui.Initialize(this);
                InitializeItemMapping();
                InitializeItemEffectHandlers();
                GlobalEvents.OnAchievementThrown += OnAchievementThrown;
                GlobalEvents.OnItemRequested += OnItemRequested;
                _log.LogInfo("[PeakPelago] About to apply Harmony patches...");
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
                // Find the target player
                var targetPlayer = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == actorNumber);

                if (targetPlayer != null)
                {
                    _photonView.RPC("ApplyAfflictionToPlayer", targetPlayer, actorNumber, statusType, amount);
                    _log.LogInfo($"[PeakPelago] Sent affliction RPC to actor {actorNumber}");
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
            _totalLuggageOpened++;
            _log.LogInfo($"[PeakPelago] Luggage count - Total: {_totalLuggageOpened}, This run: {_luggageOpenedThisRun}");
            
            CheckLuggageAchievements();
            SaveState();
        }

        private void OnDestroy()
        {
            // Unsubscribe from achievement events
            GlobalEvents.OnAchievementThrown -= OnAchievementThrown;
            // Unsubscribe from item acquisition events
            GlobalEvents.OnItemRequested -= OnItemRequested;
            _ringLinkService?.Cleanup();
            _hardRingLinkService?.Cleanup();
            _trapLinkService?.Cleanup();
            _energyLinkService?.Cleanup();
            // Remove Harmony patches
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

        public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            try
            {
                _log.LogInfo($"[PeakPelago] Player {newPlayer.NickName} joined the room");

                // Only the host syncs stamina to new players
                if (PhotonNetwork.IsMasterClient && _photonView != null)
                {
                    // Get the current stamina configuration
                    bool progressiveEnabled = _staminaManager?.IsProgressiveStaminaEnabled() ?? false;
                    int totalUpgrades = _staminaManager?.GetStaminaUpgradesReceived() ?? 0;

                    _log.LogInfo($"[PeakPelago] Host syncing to {newPlayer.NickName}: progressive={progressiveEnabled}, upgrades={totalUpgrades}");

                    // Always send the configuration, even if progressive is disabled
                    _photonView.RPC("SyncStaminaConfiguration", newPlayer, progressiveEnabled, totalUpgrades);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in OnPlayerEnteredRoom: {ex.Message}");
            }
        }
        [PunRPC]
        private void SyncStaminaUpgrade(int totalUpgrades)
        {
            try
            {
                _log.LogInfo($"[PeakPelago] RPC received: SyncStaminaUpgrade - total upgrades: {totalUpgrades}");

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
                    _log.LogInfo($"[PeakPelago] Applied stamina upgrade {currentUpgrades}/{totalUpgrades}");
                }

                // Force UI update
                StartCoroutine(ForceStaminaUIUpdate());
                
                _log.LogInfo($"[PeakPelago] Stamina sync complete - now at {currentUpgrades} upgrades");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in SyncStaminaUpgrade: {ex.Message}");
                _log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }
        [PunRPC]
        private void SyncStaminaConfiguration(bool progressiveEnabled, int totalUpgrades)
        {
            try
            {
                _log.LogInfo($"[PeakPelago] CLIENT: Received stamina configuration: progressive={progressiveEnabled}, upgrades={totalUpgrades}");
                
                if (_staminaManager == null)
                {
                    _log.LogError("[PeakPelago] CLIENT: StaminaManager is null!");
                    return;
                }
                
                _staminaManager.Initialize(progressiveEnabled, true);
                _log.LogInfo($"[PeakPelago] CLIENT: Initialized stamina manager");
                for (int i = 0; i < totalUpgrades; i++)
                {
                    _staminaManager.ApplyStaminaUpgrade();
                    _log.LogInfo($"[PeakPelago] CLIENT: Applied upgrade {i + 1}/{totalUpgrades}");
                }
                _log.LogInfo($"[PeakPelago] CLIENT: Final base max stamina: {_staminaManager.GetBaseMaxStamina()}");
                StartCoroutine(ForceStaminaUIUpdate());
                
                _log.LogInfo($"[PeakPelago] CLIENT: Successfully configured stamina system");
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
        private void ReceiveCheckFromClient(string locationName, int senderId)
        {
            try
            {
                _log.LogInfo($"[PeakPelago] Received check '{locationName}' from player {senderId}");
                
                // Only the host processes and reports to Archipelago
                if (PhotonNetwork.IsMasterClient)
                {
                    ReportCheckByName(locationName);
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

                var deathLink = new DeathLink(cfgSlot.Value, cause);
                _deathLinkService.SendDeathLink(deathLink);
                _lastDeathLinkSent = DateTime.Now;
                _notifications.ShowDeathLinkSent("DeathLink Sent!");
                _log.LogInfo($"[PeakPelago] *** DEATH LINK SENT ***: {cause} from {cfgSlot.Value}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to send death link: {ex.Message}");
            }
        }

        /// <summary>Handle incoming death link from Archipelago</summary>
        private void HandleDeathLinkReceived(string cause, string source)
        {
            if (_deathLinkService == null)
            {
                _log.LogDebug("[PeakPelago] Death link received but disabled");
                return;
            }

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!currentScene.StartsWith("Level_"))
            {
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

                _log.LogInfo($"[PeakPelago] *** DEATH LINK RECEIVED ***: {cause} from {source}");

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

                _log.LogInfo($"[PeakPelago] Applying death link to {characterName} due to death from {source} ({cause})");

                _notifications.ShowDeathLink(cause, source);
                _notifications.ShowHeroTitle("RIP " + characterName.ToUpper());

                if (_deathLinkBehavior == 1)
                {
                    _log.LogInfo($"[PeakPelago] Death link behavior: Reset to checkpoint");
                    
                    // Get checkpoint position
                    Vector3 checkpointPos = GetLastCheckpointPosition();
                    
                    foreach (var character in validCharacters)
                    {
                        try
                        {
                            character.WarpPlayerRPC(checkpointPos, true);
                            _log.LogInfo($"[PeakPelago] Warped {character.characterName ?? "player"} to checkpoint at {checkpointPos}");
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

                _log.LogInfo($"[PeakPelago] Death link applied to {characterName}");
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
                _log.LogInfo($"[PeakPelago] Executing death for {characterName}");
                
                var dieInstantlyMethod = targetCharacter.GetType().GetMethod("DieInstantly", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (dieInstantlyMethod != null)
                {
                    dieInstantlyMethod.Invoke(targetCharacter, null);
                    _log.LogInfo($"[PeakPelago] DieInstantly succeeded for {characterName}");
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

                // Method 2: Use the current segment's campfire position
                var mapHandler = GetMapHandler();
                if (mapHandler != null)
                {
                    // Cast to the actual MapHandler type to access GetCurrentSegment method
                    var mapHandlerType = mapHandler.GetType();
                    var getCurrentSegmentMethod = mapHandlerType.GetMethod("GetCurrentSegment");
                    if (getCurrentSegmentMethod != null)
                    {
                        var currentSegment = getCurrentSegmentMethod.Invoke(mapHandler, null);
                        var segments = GetMapSegments(mapHandler);

                        if (segments != null && segments is Array segmentsArray && (int)currentSegment < segmentsArray.Length)
                        {
                            var segment = segmentsArray.GetValue((int)currentSegment);
                            if (segment != null)
                            {
                                // Use reflection to get segmentCampfire property
                                var segmentCampfireField = segment.GetType().GetField("segmentCampfire");
                                if (segmentCampfireField != null)
                                {
                                    var segmentCampfire = segmentCampfireField.GetValue(segment);
                                    if (segmentCampfire != null)
                                    {
                                        var transformProperty = segmentCampfire.GetType().GetProperty("transform");
                                        if (transformProperty != null)
                                        {
                                            var transform = transformProperty.GetValue(segmentCampfire);
                                            var positionProperty = transform.GetType().GetProperty("position");
                                            if (positionProperty != null)
                                            {
                                                Vector3 campfirePos = (Vector3)positionProperty.GetValue(transform);
                                                _log.LogDebug("[PeakPelago] Using current segment campfire: " + campfirePos);
                                                return campfirePos;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Method 3: Use the current segment's reconnect spawn position
                if (mapHandler != null)
                {
                    var mapHandlerType = mapHandler.GetType();
                    var getCurrentSegmentMethod = mapHandlerType.GetMethod("GetCurrentSegment");
                    if (getCurrentSegmentMethod != null)
                    {
                        var currentSegment = getCurrentSegmentMethod.Invoke(mapHandler, null);
                        var segments = GetMapSegments(mapHandler);

                        if (segments != null && segments is Array segmentsArray && (int)currentSegment < segmentsArray.Length)
                        {
                            var segment = segmentsArray.GetValue((int)currentSegment);
                            if (segment != null)
                            {
                                // Use reflection to get reconnectSpawnPos property
                                var reconnectSpawnPosField = segment.GetType().GetField("reconnectSpawnPos");
                                if (reconnectSpawnPosField != null)
                                {
                                    var reconnectSpawnPos = reconnectSpawnPosField.GetValue(segment);
                                    if (reconnectSpawnPos != null)
                                    {
                                        var transformProperty = reconnectSpawnPos.GetType().GetProperty("transform");
                                        if (transformProperty != null)
                                        {
                                            var transform = transformProperty.GetValue(reconnectSpawnPos);
                                            var positionProperty = transform.GetType().GetProperty("position");
                                            if (positionProperty != null)
                                            {
                                                Vector3 reconnectPos = (Vector3)positionProperty.GetValue(transform);
                                                _log.LogDebug("[PeakPelago] Using reconnect spawn position: " + reconnectPos);
                                                return reconnectPos;
                                            }
                                        }
                                    }
                                }
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

        /// <summary>Get the MapHandler singleton using reflection</summary>
        private object GetMapHandler()
        {
            try
            {
                var singletonType = typeof(UnityEngine.Object).Assembly.GetType("Zorro.Core.Singleton`1");
                if (singletonType != null)
                {
                    var mapHandlerType = typeof(UnityEngine.Object).Assembly.GetType("MapHandler");
                    if (mapHandlerType != null)
                    {
                        var genericType = singletonType.MakeGenericType(mapHandlerType);
                        var instanceProperty = genericType.GetProperty("Instance");
                        if (instanceProperty != null)
                        {
                            return instanceProperty.GetValue(null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to get MapHandler: " + ex.Message);
            }
            return null;
        }

        /// <summary>Get the map segments array using reflection</summary>
        private object GetMapSegments(object mapHandler)
        {
            try
            {
                if (mapHandler == null) return null;

                var segmentsField = mapHandler.GetType().GetField("segments");
                if (segmentsField != null)
                {
                    return segmentsField.GetValue(mapHandler);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to get map segments: " + ex.Message);
            }
            return null;
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
                                bool isUnlocked = (bool)isUnlockedMethod.Invoke(achievementManager, new object[] { badgeType });
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
                // Use reflection to get the AchievementManager singleton
                var singletonType = typeof(UnityEngine.Object).Assembly.GetType("Zorro.Core.Singleton`1");
                if (singletonType != null)
                {
                    var achievementManagerType = typeof(UnityEngine.Object).Assembly.GetType("AchievementManager");
                    if (achievementManagerType != null)
                    {
                        var genericType = singletonType.MakeGenericType(achievementManagerType);
                        var instanceProperty = genericType.GetProperty("Instance");
                        if (instanceProperty != null)
                        {
                            return instanceProperty.GetValue(null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to get AchievementManager: " + ex.Message);
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

        private Dictionary<ACHIEVEMENTTYPE, string> GetBadgeToLocationMapping()
        {
            return new Dictionary<ACHIEVEMENTTYPE, string>
            {
                { ACHIEVEMENTTYPE.BeachcomberBadge, "Beachcomber Badge" },
                { ACHIEVEMENTTYPE.TrailblazerBadge, "Trailblazer Badge" },
                { ACHIEVEMENTTYPE.AlpinistBadge, "Alpinist Badge" },
                { ACHIEVEMENTTYPE.VolcanologyBadge, "Volcanology Badge" },
                { ACHIEVEMENTTYPE.CookingBadge, "Cooking Badge" },
                { ACHIEVEMENTTYPE.BoulderingBadge, "Bouldering Badge" },
                { ACHIEVEMENTTYPE.ToxicologyBadge, "Toxicology Badge" },
                { ACHIEVEMENTTYPE.ForagingBadge, "Foraging Badge" },
                { ACHIEVEMENTTYPE.EsotericaBadge, "Esoterica Badge" },
                { ACHIEVEMENTTYPE.PeakBadge, "Peak Badge" },
                { ACHIEVEMENTTYPE.LoneWolfBadge, "Lone Wolf Badge" },
                { ACHIEVEMENTTYPE.BalloonBadge, "Balloon Badge" },
                { ACHIEVEMENTTYPE.LeaveNoTraceBadge, "Leave No Trace Badge" },
                { ACHIEVEMENTTYPE.SpeedClimberBadge, "Speed Climber Badge" },
                { ACHIEVEMENTTYPE.BingBongBadge, "Bing Bong Badge" },
                { ACHIEVEMENTTYPE.NaturalistBadge, "Naturalist Badge" },
                { ACHIEVEMENTTYPE.GourmandBadge, "Gourmand Badge" },
                { ACHIEVEMENTTYPE.MycologyBadge, "Mycology Badge" },
                { ACHIEVEMENTTYPE.SurvivalistBadge, "Survivalist Badge" },
                { ACHIEVEMENTTYPE.AnimalSerenadingBadge, "Animal Serenading Badge" },
                { ACHIEVEMENTTYPE.ArboristBadge, "Arborist Badge" },
                { ACHIEVEMENTTYPE.MentorshipBadge, "Mentorship Badge" },
                { ACHIEVEMENTTYPE.KnotTyingBadge, "Knot Tying Badge" },
                { ACHIEVEMENTTYPE.PlundererBadge, "Plunderer Badge" },
                { ACHIEVEMENTTYPE.EnduranceBadge, "Endurance Badge" },
                { ACHIEVEMENTTYPE.NomadBadge, "Nomad Badge" },
                { ACHIEVEMENTTYPE.CoolCucumberBadge, "Cool Cucumber Badge" },
                { ACHIEVEMENTTYPE.NeedlepointBadge, "Needlepoint Badge" },
                { ACHIEVEMENTTYPE.AeronauticsBadge, "Aeronautics Badge" },
                { ACHIEVEMENTTYPE.TwentyFourKaratBadge, "24 Karat Badge" },
                { ACHIEVEMENTTYPE.DaredevilBadge, "Daredevil Badge" },
                { ACHIEVEMENTTYPE.MegaentomologyBadge, "Megaentomology Badge" },
                { ACHIEVEMENTTYPE.AstronomyBadge, "Astronomy Badge" },
                { ACHIEVEMENTTYPE.BundledUpBadge, "Bundled Up Badge" },
                //ROOTS UPDATE BADGES
                { ACHIEVEMENTTYPE.ForestryBadge, "Forestry Badge" },
                { ACHIEVEMENTTYPE.DisasterResponseBadge, "Disaster Response Badge" },
                { ACHIEVEMENTTYPE.UndeadEncounterBadge, "Undead Encounter Badge"},
                { ACHIEVEMENTTYPE.WebSecurityBadge, "Web Security Badge"},
                { ACHIEVEMENTTYPE.AdvancedMycologyBadge, "Advanced Mycology Badge"},
                { ACHIEVEMENTTYPE.AppliedEsotericaBadge, "Applied Esoterica Badge"},
                { ACHIEVEMENTTYPE.CalciumIntakeBadge, "Calcium Intake Badge"},
                { ACHIEVEMENTTYPE.CompetitiveEatingBadge, "Competitive Eating Badge"},
                { ACHIEVEMENTTYPE.CryptogastronomyBadge, "Cryptogastronomy Badge"},
                { ACHIEVEMENTTYPE.MycoacrobaticsBadge, "Mycoacrobatics Badge"},
                { ACHIEVEMENTTYPE.TreadLightlyBadge, "Tread Lightly Badge"},

            };
        }

        // ===== Luggage Achievement Checking =====

        private void CheckLuggageAchievements()
        {
            if (_session == null || !_hasOpenedLuggageThisSession) return;

            try
            {
                // Check total luggage achievements
                CheckAndReportAchievement("Open 1 luggage", 1, _totalLuggageOpened);
                CheckAndReportAchievement("Open 10 luggage", 10, _totalLuggageOpened);
                CheckAndReportAchievement("Open 25 luggage", 25, _totalLuggageOpened);
                CheckAndReportAchievement("Open 50 luggage", 50, _totalLuggageOpened);

                // Check single run achievements
                CheckAndReportAchievement("Open 5 luggage in a single run", 5, _luggageOpenedThisRun);
                CheckAndReportAchievement("Open 10 luggage in a single run", 10, _luggageOpenedThisRun);
                CheckAndReportAchievement("Open 20 luggage in a single run", 20, _luggageOpenedThisRun);
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
                        _log.LogInfo($"[PeakPelago] Sending check '{locationName}' to host");
                        _photonView.RPC(CHECK_RPC_NAME, RpcTarget.MasterClient, locationName, PhotonNetwork.LocalPlayer.ActorNumber);
                    }
                    else
                    {
                        _log.LogWarning($"[PeakPelago] Cannot send check '{locationName}' - not connected to Photon");
                    }
                    return;
                }

                // Host processing: Report to Archipelago
                if (_session == null)
                {
                    _log.LogWarning("[PeakPelago] Not connected to AP; cannot report checks.");
                    return;
                }

                try
                {
                    long id = _session.Locations.GetLocationIdFromName(cfgGameId.Value, locationName);

                    if (id <= 0)
                    {
                        _log.LogDebug($"[PeakPelago] Location '{locationName}' not found in AP");
                        return;
                    }

                    if (_reportedChecks.Add(id))
                    {
                        _session.Locations.CompleteLocationChecks(new long[] { id });
                        _log.LogInfo($"[PeakPelago] ✓ Reported NEW check: {locationName} (ID: {id})");
                        SaveState();

                        // Broadcast to all clients that this check was completed
                        BroadcastCheckCompleted(locationName, id);
                    }
                    else
                    {
                        _log.LogDebug($"[PeakPelago] ✗ Check already reported: {locationName} (ID: {id})");
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError("[PeakPelago] ReportCheckByName failed: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in ReportCheckByName wrapper: {ex.Message}");
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
                _reportedChecks.Add(locationId);
                _log.LogInfo($"[PeakPelago] Check completed notification: {locationName}");
                
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

            // Only check if the goal is "Reach Peak"
            if (_slotGoalType == 0)
            {
                if (currentAscent >= _slotRequiredAscent)
                {
                    _log.LogInfo($"[PeakPelago] PEAK reached on Ascent {currentAscent} - goal complete!");
                    SendGoalComplete();
                    string completionLocation = $"Ascent {currentAscent} Completed";
                    ReportCheckByName(completionLocation);
                }
                else
                {
                    _log.LogInfo($"[PeakPelago] Peak reached but on Ascent {currentAscent}, need Ascent {_slotRequiredAscent} for goal");
                }
            }
        }

        /// <summary>
        /// Check for goal completion based on the badges.
        /// </summary>
        /// <param name="achievementType"></param>
        private void CheckForGoalCompletion(ACHIEVEMENTTYPE achievementType)
        {
            if (_session == null) return;

            switch (_slotGoalType)
            {
                case 1: // Complete All Badges goal
                    if (_collectedBadges.Count >= _slotRequiredBadges)
                    {
                        _log.LogInfo($"[PeakPelago] Collected {_collectedBadges.Count}/{_slotRequiredBadges} badges - goal complete!");
                        SendGoalComplete();
                    }
                    else
                    {
                        _log.LogInfo($"[PeakPelago] Progress: {_collectedBadges.Count}/{_slotRequiredBadges} badges collected");
                    }
                    break;

                case 2: // 24 Karat Badge goal
                    if (achievementType == ACHIEVEMENTTYPE.TwentyFourKaratBadge)
                    {
                        _log.LogInfo("[PeakPelago] 24 Karat Badge earned - goal complete!");
                        SendGoalComplete();
                    }
                    break;

                case 0: // Reach Peak goal
                    // This is handled in CheckReachPeakGoal method
                    break;
                default:
                    // This is handled in CheckReachPeakGoal method
                    break;
            }
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
                var pkt = new StatusUpdatePacket { Status = ArchipelagoClientState.ClientGoal };
                _session.Socket.SendPacket(pkt);
                _log.LogInfo("[PeakPelago] Goal sent.");
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
        private Dictionary<string, string> _itemToLocationMapping = new Dictionary<string, string>();

        // Track which ascent badges have been awarded to avoid duplicates
        private HashSet<string> _awardedAscentBadges = new HashSet<string>();
        // Track which badges have been collected to avoid duplicates
        private HashSet<ACHIEVEMENTTYPE> _collectedBadges = new HashSet<ACHIEVEMENTTYPE>();

        // Item effect handlers for Archipelago items
        private Dictionary<string, System.Action> _itemEffectHandlers = new Dictionary<string, System.Action>();

        private void InitializeItemMapping()
        {
            // Map in-game item names (all caps) to Archipelago location names
            // Based on the Locations.py file from the .apworld
            _itemToLocationMapping = new Dictionary<string, string>
            {
                // Rope items
                { "ROPE SPOOL", "Acquire Rope Spool" },
                { "ROPE CANNON", "Acquire Rope Cannon" },
                { "ANTI-ROPE SPOOL", "Acquire Anti-Rope Spool" },
                { "ANTI-ROPE CANNON", "Acquire Anti-Rope Cannon" },
                { "CHAIN LAUNCHER", "Acquire Chain Launcher" },
                { "PITON", "Acquire Piton" },
                { "RESCUE CLAW", "Acquire Rescue Claw" },
                
                // Special items
                { "MAGIC BEAN", "Acquire Magic Bean" },
                { "PARASOL", "Acquire Parasol" },
                { "BALLOON", "Acquire Balloon" },
                { "BALLOON BUNCH", "Acquire Balloon Bunch" },
                { "SCOUT CANNON", "Acquire Scout Cannon" },
                { "FLYING DISC", "Acquire Flying Disc" },
                { "GUIDEBOOK", "Acquire Guidebook" },
                
                // Fire/light items
                { "PORTABLE STOVE", "Acquire Portable Stove" },
                { "FIREWOOD", "Acquire FireWood" },
                { "LANTERN", "Acquire Lantern" },
                { "FLARE", "Acquire Flare" },
                { "TORCH", "Acquire Torch" },
                { "FAERIE LANTERN", "Acquire Faerie Lantern" },
                
                // Navigation items
                { "CACTUS BALL", "Acquire CactusBall" },
                { "COMPASS", "Acquire Compass" },
                { "PIRATE COMPASS", "Acquire Pirate Compass" },
                { "BINOCULARS", "Acquire Binoculars" },
                
                // Medical items
                { "BANDAGES", "Acquire Bandages" },
                { "FIRST AID KIT", "Acquire First-Aid Kit" },
                { "ANTIDOTE", "Acquire Antidote" },
                { "HEAT PACK", "Acquire Heat Pack" },
                { "CURE-ALL", "Acquire Cure-All" },
                { "REMEDY FUNGUS", "Acquire Remedy Fungus" },
                { "MEDICINAL ROOT", "Acquire Medicinal Root" },
                { "ALOE VERA", "Acquire Aloe Vera" },
                { "SUNSCREEN", "Acquire Sunscreen" },
                { "MARSHMALLOW", "Acquire Marshmallow" },
                { "GLIZZY", "Acquire Glizzy" },
                { "FORTIFIED MILK", "Acquire Fortified Milk" },
                
                // Special objects
                { "SCOUT EFFIGY", "Acquire Scout Effigy" },
                { "CURSED SKULL", "Acquire Cursed Skull" },
                { "PANDORA'S LUNCHBOX", "Acquire Pandora's Lunchbox" },
                { "ANCIENT IDOL", "Acquire Ancient Idol" },
                { "STRANGE GEM", "Acquire Strange Gem" },
                { "BOOK OF BONES", "Acquire Book of Bones" },
                { "CHECKPOINT FLAG", "Acquire Checkpoint Flag" },
                
                // Musical items
                { "BUGLE OF FRIENDSHIP", "Acquire Bugle of Friendship" },
                { "BUGLE", "Acquire Bugle" },
                
                // Mushrooms
                { "SHELF SHROOM", "Acquire Shelf Shroom" },
                { "BOUNCE SHROOM", "Acquire Bounce Shroom" },
                { "BUTTON SHROOM", "Acquire Button Shroom" },
                { "BUGLE SHROOM", "Acquire Bugle Shroom" },
                { "CLUSTER SHROOM", "Acquire Cluster Shroom" },
                { "CHUBBY SHROOM", "Acquire Chubby Shroom" },
                
                // Food items
                { "TRAIL MIX", "Acquire TrailMix" },
                { "GRANOLA BAR", "Acquire Granola Bar" },
                { "SCOUT COOKIES", "Acquire Scout Cookies" },
                { "AIRLINE FOOD", "Acquire Airline Food" },
                { "ENERGY DRINK", "Acquire Energy Drink" },
                { "SPORTS DRINK", "Acquire Sports Drink" },
                { "BIG LOLLIPOP", "Acquire Big Lollipop" },
                { "BIG EGG", "Acquire Big Egg" },
                { "EGG", "Acquire Egg" },
                { "COOKED BIRD", "Acquire Cooked Bird" },
                { "HONEYCOMB", "Acquire Honeycomb" },
                { "BEEHIVE", "Acquire Beehive" },
                { "SCORPION", "Acquire Scorpion" },
                
                // Miscellaneous items
                { "CONCH", "Acquire Conch" },
                { "BANANA PEEL", "Acquire Banana Peel" },
                { "DYNAMITE", "Acquire Dynamite" },
                { "BING BONG", "Acquire Bing Bong" },
                
                // Berries
                { "RED CRISPBERRY", "Acquire Red Crispberry" },
                { "GREEN CRISPBERRY", "Acquire Green Crispberry" },
                { "YELLOW CRISPBERRY", "Acquire Yellow Crispberry" },
                { "COCONUT", "Acquire Coconut" },
                { "COCONUT HALF", "Acquire Coconut Half" },
                { "BROWN BERRYNANA", "Acquire Brown Berrynana" },
                { "BLUE BERRYNANA", "Acquire Blue Berrynana" },
                { "PINK BERRYNANA", "Acquire Pink Berrynana" },
                { "YELLOW BERRYNANA", "Acquire Yellow Berrynana" },
                { "ORANGE WINTERBERRY", "Acquire Orange Winterberry" },
                { "YELLOW WINTERBERRY", "Acquire Yellow Winterberry" },
                { "RED PRICKLEBERRY", "Acquire Red Prickleberry" },
                { "GOLD PRICKLEBERRY", "Acquire Gold Prickleberry" },
            };

            _log.LogInfo("[PeakPelago] Initialized item mapping with " + _itemToLocationMapping.Count + " items");
        }

        private void InitializeItemEffectHandlers()
        {
            _itemEffectHandlers = new Dictionary<string, System.Action>
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
                { "Pirate Compass", () => SpawnPhysicalItem("Pirate Compass") },
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
                { "Airline Food", () => SpawnPhysicalItem("Airline Food") },
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

                //Item Bundles
                { "Bundle: Glizzy Gobbler", () => SpawnPhysicalItems("Glizzy", 3) },
                { "Bundle: Marshmallow Muncher", () => SpawnPhysicalItems("Marshmallow", 3) },
                { "Bundle: Trailblazer Snacks", () => {
                    SpawnPhysicalItems("Granola Bar", 2);
                    SpawnPhysicalItems("TrailMix", 2);
                }},
                { "Bundle: Lovely Bunch", () => SpawnPhysicalItems("Item_Coconut", 3) },
                { "Bundle: Bear Favorite", () => SpawnPhysicalItems("Item_Honeycomb", 6) },
                { "Bundle: Rainy Day", () => SpawnPhysicalItems("Parasol", 4) },
                { "Bundle: Turkey Day", () => SpawnPhysicalItems("EggTurkey", 3) },


                // Progression Items (76019-76025) - Unlock ascents
                { "Ascent 1 Unlock", () => UnlockAscent(1) },
                { "Ascent 2 Unlock", () => UnlockAscent(2) },
                { "Ascent 3 Unlock", () => UnlockAscent(3) },
                { "Ascent 4 Unlock", () => UnlockAscent(4) },
                { "Ascent 5 Unlock", () => UnlockAscent(5) },
                { "Ascent 6 Unlock", () => UnlockAscent(6) },
                { "Ascent 7 Unlock", () => UnlockAscent(7) },
                { "Progressive Stamina Bar", () => ApplyProgressiveStamina() },

                // Trap Items
                { "Spawn Bee Swarm", () => BeeSwarmTrapEffect.ApplyBeeSwarmTrap(_log) },
                { "Destroy Held Item", () => DestroyHeldItem() },
                { "Blue Berrynana Peel", () => SpawnPhysicalItem("Berrynana Peel Blue Variant") },
                { "Banana Peel Trap", () => SpawnPhysicalItem("Berrynana Peel Yellow") },
                { "Minor Poison Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.25f, CharacterAfflictions.STATUSTYPE.Poison) },
                { "Poison Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.53f, CharacterAfflictions.STATUSTYPE.Poison) },
                { "Deadly Poison Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.95f, CharacterAfflictions.STATUSTYPE.Poison) },
                { "Tornado Trap", () => TornadoTrapEffect.SpawnTornadoOnPlayer(_log) },
                { "Pokemon Trivia Trap", () => PokemonTriviaTrapEffect.ApplyPokemonTriviaTrap(_log) },
                { "Swap Trap", () => SwapTrapEffect.ApplyPositionSwapTrap(_log) },
                { "Nap Time Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 1.0f, CharacterAfflictions.STATUSTYPE.Drowsy) },
                { "Hungry Hungry Camper Trap", () => HungryHungryCamperTrapEffect.ApplyHungerTrap(_log) },
                { "Balloon Trap", () => BalloonTrapEffect.ApplyBalloonTrap(_log) },
                { "Slip Trap", () => SlipTrapEffect.ApplySlipTrap(_log) },
                { "Clear All Effects", () => ClearAllEffects() },
                { "Speed Upgrade", () => ApplySpeedUpgrade() },
                { "Cactus Ball Trap", () =>ItemToWhateverTrapEffect.ApplyItemToWhateverTrap(_log, "CactusBall") },
                { "Freeze Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 1.0f, CharacterAfflictions.STATUSTYPE.Cold) },
                { "Cold Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.5f, CharacterAfflictions.STATUSTYPE.Cold) },
                { "Hot Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.5f, CharacterAfflictions.STATUSTYPE.Hot) },
                { "Injury Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.5f, CharacterAfflictions.STATUSTYPE.Injury) },
                { "Bounce Fungus", () => SpawnPhysicalItem("BounceShroom") },
                { "Cloud Fungus", () => SpawnPhysicalItem("CloudFungus") },
                { "Instant Death Trap", () => InstantDeathTrapEffect.ApplyInstantDeathTrap(_log) },
                { "Yeet Trap", () => YeetItemTrapEffect.ApplyYeetTrap(_log)},
                { "Tumbleweed Trap", () => TumbleweedTrapEffect.ApplyTumbleweedTrap(_log) },
                { "Items to Bombs", () => ItemToBombTrapEffect.ApplyItemToBombTrap(_log) },
                { "Zombie Horde Trap", () => ZombieHordeTrapEffect.ApplyZombieHordeTrap(_log) },
                { "Gust Trap", () => GustTrapEffect.ApplyGustTrap(_log) },
                { "Mandrake Trap", () => ItemToWhateverTrapEffect.ApplyItemToWhateverTrap(_log, "Mandrake") },
                { "Blackout Trap", () => BlackoutTrapEffect.ApplyBlackoutTrap(_log) },
                { "Fungal Infection Trap", () => StatusOverTimeTrapEffect.ApplyStatusOverTime(_log, StatusOverTimeTrapEffect.TargetMode.RandomPlayer,
                CharacterAfflictions.STATUSTYPE.Spores,
                amountPerTick: 0.1f,
                tickInterval: 1.0f,
                duration: 5.0f
                ) },


            };

            _log.LogInfo("[PeakPelago] Initialized item effect handlers with " + _itemEffectHandlers.Count + " items");
        }

        [PunRPC]
        private void StartBlackoutTrapRPC()
        {
            try
            {
                _log.LogInfo("[PeakPelago] RPC received: Start Blackout Trap");
                BlackoutTrapEffect.ApplyBlackoutTrapLocal(_log);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartBlackoutTrapRPC: {ex.Message}");
            }
        }

        [PunRPC]
        private void StartPokemonTriviaRPC()
        {
            try
            {
                _log.LogInfo("[PeakPelago] RPC received: Start Pokemon Trivia");
                PokemonTriviaTrapEffect.ApplyPokemonTriviaTrapLocal(_log);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in StartPokemonTriviaRPC: {ex.Message}");
            }
        }

        [PunRPC]
        private void ExpandWindBoundsRPC(float centerX, float centerY, float centerZ)
        {
            try
            {
                Vector3 center = new Vector3(centerX, centerY, centerZ);
                _log.LogInfo($"[PeakPelago] RPC received: ExpandWindBounds at ({centerX}, {centerY}, {centerZ})");
                GustTrapEffect.ExpandWindBounds(center);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in ExpandWindBoundsRPC: {ex.Message}");
            }
        }

        [PunRPC]
        private void RestoreWindBoundsRPC()
        {
            try
            {
                _log.LogInfo($"[PeakPelago] RPC received: RestoreWindBounds");
                GustTrapEffect.RestoreWindBounds();
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in RestoreWindBoundsRPC: {ex.Message}");
            }
        }

        [PunRPC]
        private void StartDOTTrapRPC(int targetActorNumber, int statusType, float amountPerTick, float tickInterval, float duration)
        {
            try
            {
                _log.LogInfo($"[PeakPelago] RPC received: StartDOTTrap for actor {targetActorNumber}, type {statusType}");

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
                _log.LogInfo($"[PeakPelago] Starting DOT on local character: {(CharacterAfflictions.STATUSTYPE)statusType}");
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
        private void SwapTrapWarpRPC(int targetActorNumber, float posX, float posY, float posZ)
        {
            try
            {
                _log.LogInfo($"[PeakPelago] RPC received: SwapTrapWarp for actor {targetActorNumber} to ({posX}, {posY}, {posZ})");

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
                _log.LogInfo($"[PeakPelago] Warping local character to {targetPosition}");
                
                Character.localCharacter.WarpPlayerRPC(targetPosition, true);
                
                _log.LogInfo($"[PeakPelago] Warp executed successfully!");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in SwapTrapWarpRPC: {ex.Message}");
                _log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        [PunRPC]
        private void ApplyAfflictionToPlayer(int targetActorNumber, int statusType, float amount)
        {
            try
            {
                _log.LogInfo($"[PeakPelago] RPC received: ApplyAfflictionToPlayer for actor {targetActorNumber}, type {statusType}, amount {amount}");

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
                _log.LogInfo($"[PeakPelago] Applying {(CharacterAfflictions.STATUSTYPE)statusType} ({amount}) to local character");
                Character.localCharacter.refs.afflictions.AddStatus((CharacterAfflictions.STATUSTYPE)statusType, amount);
                _log.LogInfo($"[PeakPelago] Affliction applied successfully!");
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
                    _log.LogInfo($"[PeakPelago] Synced {totalUpgrades} stamina upgrades to new player");
                }
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

                int upgradesBeforeApply = _staminaManager.GetStaminaUpgradesReceived();

                _staminaManager.ApplyStaminaUpgrade();

                int totalUpgradesAfterApply = upgradesBeforeApply + 1;
                if (_photonView != null && PhotonNetwork.IsConnected)
                {
                    _photonView.RPC("SyncStaminaUpgrade", RpcTarget.Others, totalUpgradesAfterApply);
                    _log.LogInfo($"[PeakPelago] Broadcasted stamina upgrade to others: {totalUpgradesAfterApply} total");
                }

                if (Character.localCharacter != null)
                {
                    StartCoroutine(ForceStaminaUIUpdate());
                }

                SaveState();
                _log.LogInfo("[PeakPelago] Saved stamina state after upgrade");
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error in ApplyProgressiveStamina: " + ex.Message);
            }
        }
        

        private void SpawnPhysicalItems(string itemName, int quantity)
        {
            for (int i = 0; i < quantity; i++)
            {
                System.Threading.Thread.Sleep(500);
                SpawnPhysicalItem(itemName);
            }
        }

        private void SpawnPhysicalItem(string itemName)
        {
            try
            {
                Item itemToSpawn = null;

                // DEBUG: Log all available items in the database
                _log.LogInfo("[PeakPelago] === AVAILABLE ITEMS IN DATABASE ===");
                for (ushort itemID = 1; itemID < 300; itemID++)
                {
                    if (ItemDatabase.TryGetItem(itemID, out Item item))
                    {
                        _log.LogInfo("[PeakPelago] Item ID " + itemID + ": " + item.name);
                    }
                }
                _log.LogInfo("[PeakPelago] === END OF AVAILABLE ITEMS ===");

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
                    _log.LogWarning("[PeakPelago] Could not find item in database: " + itemName);
                    return;
                }

                // Get all valid characters
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    _log.LogWarning("[PeakPelago] Cannot spawn items - no characters found");
                    return;
                }

                // Filter to only active, alive characters
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

                _log.LogInfo($"[PeakPelago] Spawning {itemName} for {validCharacters.Count} players");

                // Spawn item for each valid character
                foreach (var character in validCharacters)
                {
                    try
                    {
                        Vector3 spawnPosition = character.Center + character.transform.forward * 2f;
                        spawnPosition += Vector3.up * 0.5f; // Slightly above ground

                        // Spawn the item prefab
                        GameObject spawnedItem = PhotonNetwork.Instantiate("0_Items/" + itemToSpawn.name, spawnPosition, Quaternion.identity, 0);
                        
                        string characterName = character == Character.localCharacter ? "local player" : character.characterName;
                        _log.LogInfo($"[PeakPelago] Spawned {itemName} for {characterName}");
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"[PeakPelago] Error spawning item for character: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error spawning physical item " + itemName + ": " + ex.Message);
            }
        }

        private void UnlockAscent(int ascentLevel)
        {
            try
            {

                // Track the unlocked ascent
                _unlockedAscents.Add(ascentLevel);

                // Use reflection to access the Ascents class and unlock the ascent
                var ascentsType = Type.GetType("Ascents");
                if (ascentsType != null)
                {
                    var unlockMethod = ascentsType.GetMethod("UnlockAscent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (unlockMethod != null)
                    {
                        unlockMethod.Invoke(null, new object[] { ascentLevel });

                        // Log all currently unlocked ascents
                        var sortedAscents = _unlockedAscents.OrderBy(x => x).ToList();
                    }
                    else
                    {
                        _log.LogWarning("[PeakPelago] Could not find UnlockAscent method");
                    }
                }
                else
                {
                    _log.LogWarning("[PeakPelago] Could not find Ascents class");
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error unlocking ascent " + ascentLevel + ": " + ex.Message);
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
                _log.LogInfo("[PeakPelago] Destroyed held item");
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

                // Use reflection to access and clear all afflictions
                var afflictionsType = afflictions.GetType();
                var clearAllMethod = afflictionsType.GetMethod("ClearAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (clearAllMethod != null)
                {
                    clearAllMethod.Invoke(afflictions, null);
                    _log.LogInfo("[PeakPelago] Cleared all status effects");
                }
                else
                {
                    _log.LogWarning("[PeakPelago] Could not find ClearAll method for afflictions");
                }
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
                // Track the received item for debug purposes
                _itemsReceivedFromAP++;
                _lastReceivedItemName = itemName;
                _lastReceivedItemTime = DateTime.Now;

                // Check if we have a handler for this item
                if (_itemEffectHandlers.ContainsKey(itemName))
                {
                    _itemEffectHandlers[itemName].Invoke();

                    // Send trap link if this is a trap item and not already from trap link
                    if (!fromTrapLink && _trapLinkService != null && IsTrapItem(itemName))
                    {
                        _trapLinkService.SendTrapLink(itemName);
                    }
                }
                else
                {
                    _log.LogWarning("[PeakPelago] No effect handler found for item: " + itemName);
                }
            }
            catch (System.Exception ex)
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
            if (string.IsNullOrEmpty(itemName)) return;

            // Update last acquired item info
            _lastAcquiredItemName = itemName;
            _lastAcquiredItemId = itemId;
            _lastAcquiredItemTime = Time.time;

            // Check if this item has an Archipelago location to report
            if (_itemToLocationMapping.TryGetValue(itemName.ToUpper(), out string locationName))
            {
                ReportCheckByName(locationName);
            }
            else
            {
                _log.LogDebug("[PeakPelago] No Archipelago location found for item: " + itemName);
            }
            SaveState();
        }

        /// <summary>Call this when a new run starts to reset run-specific counters</summary>
        public void ResetRunCounters()
        {
            _luggageOpenedThisRun = 0;
            _log.LogInfo("[PeakPelago] Run counters reset for new run");
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
                string badgeLocation = GetAscentBadgeLocation(peakName, currentAscent);
                if (!string.IsNullOrEmpty(badgeLocation))
                {
                    string badgeKey = badgeLocation + "_" + currentAscent;
                    
                    if (!_awardedAscentBadges.Contains(badgeKey))
                    {
                        ReportCheckByName(badgeLocation);
                        _awardedAscentBadges.Add(badgeKey);
                    }
                    else
                    {
                        _log.LogInfo("[PeakPelago] Badge already awarded for key: " + badgeKey);
                    }
                }
                else
                {
                    _log.LogWarning("[PeakPelago] No badge location found for peak: " + peakName);
                }

                // Award scout sash only when reaching the final peak (PEAK) - this means escaping/completing the ascent
                if (peakName.ToUpper() == "PEAK")
                {
                    string sashLocation = GetScoutSashLocation(currentAscent);

                    if (!string.IsNullOrEmpty(sashLocation))
                    {
                        string sashKey = sashLocation + "_" + currentAscent;

                        if (!_awardedAscentBadges.Contains(sashKey))
                        {
                            ReportCheckByName(sashLocation);
                            _awardedAscentBadges.Add(sashKey);
                        }
                        else
                        {
                            _log.LogInfo("[PeakPelago] Sash already awarded for key: " + sashKey);
                        }
                    }
                    CheckReachPeakGoal(currentAscent);
                }
                else
                {
                    _log.LogInfo("[PeakPelago] Not awarding scout sash - not at final peak (PEAK), currently at: " + peakName);
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

        private string GetAscentBadgeLocation(string peakName, int ascentLevel)
        {
            // Map peak names to their ascent badge locations
            switch (peakName.ToUpper())
            {
                case "SHORE":
                    return "Beachcomber " + GetRomanNumeral(ascentLevel + 1) + " Badge (Ascent " + ascentLevel + ")";
                case "TROPICS":
                    return "Trailblazer " + GetRomanNumeral(ascentLevel + 1) + " Badge (Ascent " + ascentLevel + ")";
                case "MESA":
                    return "Nomad " + GetRomanNumeral(ascentLevel + 1) + " Badge (Ascent " + ascentLevel + ")";
                case "ALPINE":
                    return "Desolate " + GetRomanNumeral(ascentLevel + 1) + " Badge (Ascent " + ascentLevel + ")";
                case "CALDERA":
                case "THE KILN":
                    return "Volcanology " + GetRomanNumeral(ascentLevel + 1) + " Badge (Ascent " + ascentLevel + ")";
                case "ROOTS":
                    return "Forestry " + GetRomanNumeral(ascentLevel + 1) + " Badge (Ascent " + ascentLevel + ")";
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
                default: return number.ToString();
            }
        }

        private System.Collections.IEnumerator ForceStaminaUIUpdate()
        {
            // Wait a bit for the character and UI to be fully initialized
            yield return new WaitForSeconds(0.5f);

            _log.LogInfo("[PeakPelago] CLIENT: Forcing stamina UI update...");

            // Force update the character's stamina
            if (Character.localCharacter != null && _staminaManager != null)
            {
                // Recalculate and clamp stamina
                float baseMax = _staminaManager.GetBaseMaxStamina();
                float statusSum = Character.localCharacter.refs.afflictions.statusSum;
                float effectiveMax = Mathf.Max(baseMax - statusSum, 0f);
                Character.localCharacter.data.currentStamina = Mathf.Min(Character.localCharacter.data.currentStamina, effectiveMax);

                _log.LogInfo($"[PeakPelago] CLIENT: Set stamina to {Character.localCharacter.data.currentStamina} (max: {effectiveMax}, base: {baseMax})");

                // Force the stamina bar UI to refresh
                if (GUIManager.instance != null && GUIManager.instance.bar != null)
                {
                    GUIManager.instance.bar.ChangeBar();
                    _log.LogInfo("[PeakPelago] CLIENT: Stamina bar UI refreshed");
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

        // ===== Harmony Patches =====
        
        ///This updates the DeathLink checkpoint spawn when we initialize a new map segment (like when we activate a campfire at the top of one of the biomes)
        [HarmonyPatch(typeof(MapHandler), "GoToSegment")]
        public static class MapHandlerGoToSegmentPatch
        {
            static void Postfix(MapHandler __instance)
            {
                try
                {
                    if (_instance == null) return;
                    int currentSegment = (int)__instance.GetCurrentSegment();
                    if (currentSegment >= 0 && currentSegment < __instance.segments.Length)
                    {
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
                            
                            _instance._log.LogInfo($"[PeakPelago] Updated spawn point for all players to segment {currentSegment}: {segment.reconnectSpawnPos.position}");
                        }
                        else
                        {
                            _instance._log.LogWarning($"[PeakPelago] Segment {currentSegment} has no reconnect spawn position");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError($"[PeakPelago] GoToSegment patch error: {ex.Message}");
                    }
                }
            }
        }
        [HarmonyPatch(typeof(Luggage), "OpenLuggageRPC")]
        public static class LuggageOpenRPCPatch
        {
            static void Postfix(Luggage __instance)
            {
                try
                {
                    if (_instance == null || !PhotonNetwork.IsMasterClient)
                    {
                        return;
                    }

                    // Host observes ALL luggage opens via the RPC
                    _instance._log.LogInfo($"[PeakPelago] HOST: Luggage opened: {__instance.GetName()}");
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

        [HarmonyPatch(typeof(Item), "RequestPickup")]
        public static class ItemRequestPickupPatch
        {
            static void Postfix(Item __instance, PhotonView characterView)
            {
                try
                {
                    if (_instance == null) return;
                    if (!PhotonNetwork.IsMasterClient) return;

                    // Get the character who picked up the item
                    Character character = characterView.GetComponent<Character>();
                    if (character == null) return;

                    // Get item name
                    string itemName = __instance.UIData.itemName;
                    ushort itemId = __instance.itemID;

                    _instance._log.LogDebug($"[PeakPelago] HOST: Player {character.characterName} (Actor: {characterView.Owner.ActorNumber}) picked up item: {itemName} (ID: {itemId})");

                    _instance.TrackItemAcquisition(itemName, itemId);
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError("[PeakPelago] RequestPickup patch error: " + ex.Message);
                        _instance._log.LogError("[PeakPelago] Stack trace: " + ex.StackTrace);
                    }
                }
            }
        }

        /// <summary>
        /// PATCH ZOMBIFYING TOO BECAUSE IT DOESNT CALL DEATH FUNCTIONS >:[ THANKS ROOTS UPDATE
        /// </summary>
        [HarmonyPatch(typeof(Character), "FinishZombifying")]
        public static class CharacterFinishZombifyingPatch
        {
            static void Postfix(Character __instance)
            {
                try
                {
                    if (_instance == null) return;
                    if (_instance._deathLinkService == null) return;

                    if (_instance._isDyingFromDeathLink)
                    {
                        _instance._log.LogInfo("[PeakPelago] Zombification was caused by DeathLink, not sending another DeathLink");
                        return;
                    }

                    _instance._log.LogInfo($"[PeakPelago] Character zombified: {__instance.characterName}");
                    if (_instance._deathLinkSendBehavior == 0)
                    {
                        _instance._log.LogInfo("[PeakPelago] Sending Death Link (zombification - any player mode)");
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
                            _instance._log.LogInfo("[PeakPelago] Sending Death Link (all players dead/zombified mode)");
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

        [HarmonyPatch(typeof(Character), "RPCA_Die")]
        public static class CharacterRPCADiePatch
        {
            static void Postfix(Character __instance)
            {
                try
                {
                    if (_instance == null) return;
                    if (_instance._deathLinkService == null) return;

                    if (_instance._isDyingFromDeathLink)
                    {
                        _instance._log.LogInfo("[PeakPelago] Death was caused by DeathLink, not sending another DeathLink");
                        return;
                    }
            
                    
                    _instance._log.LogInfo($"[PeakPelago] Character died: {__instance.characterName}");
                    
                    // Send when any player dies
                    if (_instance._deathLinkSendBehavior == 0)
                    {
                        _instance._log.LogInfo("[PeakPelago] Sending Death Link (any player dies mode)");
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
                            _instance._log.LogInfo("[PeakPelago] Sending Death Link (all players dead mode)");
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
        [HarmonyPatch(typeof(AchievementManager), "ThrowAchievement")]
        public static class AchievementManagerThrowAchievementPatch
        {
            static bool Prefix(ACHIEVEMENTTYPE type)
            {
                try
                {
                    if (_instance == null)
                    {
                        UnityEngine.Debug.Log("[PeakPelago] ThrowAchievementPatch: _instance is null");
                        return true;
                    }

                    if (!_instance._badgesHidden)
                    {
                        UnityEngine.Debug.Log("[PeakPelago] ThrowAchievementPatch: badges not hidden, _badgesHidden = " + _instance._badgesHidden);
                        return true;
                    }

                    UnityEngine.Debug.Log("[PeakPelago] ThrowAchievementPatch: Processing badge " + type);

                    // Check if this badge was originally unlocked
                    bool wasOriginallyUnlocked = _instance._originalUnlockedBadges.Contains(type);

                    // Get the location name for this badge
                    var badgeMapping = _instance.GetBadgeToLocationMapping();
                    if (badgeMapping.TryGetValue(type, out string locationName))
                    {
                        _instance._log.LogInfo("[PeakPelago] Badge condition met: " + locationName);

                        // Report the check to Archipelago
                        _instance.ReportCheckByName(locationName);

                        // If it was originally unlocked, also award the badge normally
                        if (wasOriginallyUnlocked)
                        {
                            _instance._log.LogInfo("[PeakPelago] Re-awarding originally unlocked badge: " + locationName);
                            return true; // Allow normal badge awarding
                        }
                        else
                        {
                            _instance._log.LogInfo("[PeakPelago] New badge earned: " + locationName);
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
                    if (_instance == null || !_instance._badgesHidden) return true;

                    // Get the location name for this badge
                    var badgeMapping = _instance.GetBadgeToLocationMapping();
                    if (badgeMapping.ContainsKey(achievementType))
                    {
                        // Check if this badge has been reported to Archipelago
                        string locationName = badgeMapping[achievementType];
                        long locationId = _instance._session?.Locations.GetLocationIdFromName(_instance.cfgGameId.Value, locationName) ?? -1;

                        if (locationId > 0 && _instance._reportedChecks.Contains(locationId))
                        {
                            // This badge has been reported to AP, so show it as unlocked
                            __result = true;
                            return false; // Skip original method
                        }
                        else
                        {
                            // This badge hasn't been reported to AP yet, so hide it
                            __result = false;
                            return false; // Skip original method
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError("[PeakPelago] IsAchievementUnlocked patch error: " + ex.Message);
                    }
                }

                return true; // Allow normal processing for non-badge achievements
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

                    if (_instance._unlockedAscents.Count > 0)
                    {
                        // Sort ascents and find the highest consecutive ascent starting from 1
                        var sortedAscents = _instance._unlockedAscents.OrderBy(x => x).ToList();

                        int maxConsecutiveAscent = 0;

                        // Check for consecutive ascents starting from 1
                        for (int i = 1; i <= 7; i++) // Ascents go from 1 to 7
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

                    // Get the peak name from the progress point
                    string peakName = "Unknown";
                    if (pointReached != null)
                    {
                        var titleField = pointReached.GetType().GetField("title");
                        if (titleField != null)
                        {
                            peakName = (string)titleField.GetValue(pointReached) ?? "Unknown";
                        }
                    }

                    _instance._log.LogInfo("[PeakPelago] Player reached peak: " + peakName);
                    _instance.HandleAscentPeakReached(peakName);
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError("[PeakPelago] CheckAreaAchievement patch error: " + ex.Message);
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
            _status = "Connecting...";

            try
            {
                // Check for port changes before connecting
                CheckAndHandlePortChange();

                string host = cfgServer.Value;
                string url = host.Contains(":") ? host : (host + ":" + cfgPort.Value);
                LoadState();
                _log.LogInfo("[PeakPelago] Connecting to " + url + " as " + cfgSlot.Value + " (game=" + cfgGameId.Value + ")");

                _session = ArchipelagoSessionFactory.CreateSession(url);

                // Log server messages to the BepInEx console
                _session.MessageLog.OnMessageReceived += OnApMessage;

                // Correct signature: one string argument (reason)
                _session.Socket.SocketClosed += reason =>
                {
                    _log.LogWarning("[PeakPelago] Socket closed: " + reason);
                    _status = "Disconnected";
                    if (cfgAutoReconnect.Value)
                    {
                        _wantReconnect = true;
                    }
                };

                // Use TryConnectAndLogin instead of ConnectAsync/LoginAsync
                var result = _session.TryConnectAndLogin(
                    cfgGameId.Value,
                    cfgSlot.Value,
                    ItemsHandlingFlags.AllItems,
                    null,
                    null,
                    null,                        // uuid
                    string.IsNullOrEmpty(cfgPassword.Value) ? null : cfgPassword.Value,
                    true                         // requestSlotData
                );

                if (!result.Successful)
                {
                    string msg = (result is LoginFailure lf) ? lf.Errors?.FirstOrDefault() ?? "Login failed." : "Login failed.";
                    _status = "Login failed";
                    _log.LogError("[PeakPelago] " + msg);
                    TryCloseSession();
                    return;
                }

                // Ask for datapackage for our game so helper name<->id lookups work
                _session.Socket.SendPacket(new GetDataPackagePacket { Games = new[] { cfgGameId.Value } });
                _deathLinkService = _session.CreateDeathLinkService();
                _log.LogInfo("[PeakPelago] Death Link service created");
                _deathLinkService.OnDeathLinkReceived += (deathLink) =>
                {
                    try
                    {
                        _log.LogInfo($"[PeakPelago] Death Link received from {deathLink.Source}: {deathLink.Cause}");

                        if (deathLink.Source == cfgSlot.Value)
                        {
                            _log.LogDebug("[PeakPelago] Ignoring own death link");
                            return;
                        }

                        HandleDeathLinkReceived(deathLink.Cause ?? "Death Link", deathLink.Source);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"[PeakPelago] Error handling death link: {ex.Message}");
                    }
                };
                _session.Items.ItemReceived += helper =>
                {
                    try
                    {
                        var info = helper.DequeueItem();
                        string itemName = helper.GetItemName(info.ItemId, info.ItemGame) ?? ("Item " + info.ItemId);
                        string fromName = _session.Players.GetPlayerName(info.Player) ?? ("Player " + info.Player);
                        string toName = cfgSlot.Value;
                        ItemFlags classification = info.Flags;

                        if (helper.Index > _lastProcessedItemIndex)
                        {
                            _notifications.ShowItemNotification(fromName, toName, itemName, classification);
                            bool isTrap = IsTrapItem(itemName);
                            _itemQueue.AddLast((itemName, isTrap, helper.Index));

                            _log.LogInfo($"[PeakPelago] Queued NEW item #{helper.Index}: {itemName} (Queue size: {_itemQueue.Count})");
                        }
                        else
                        {
                            _log.LogDebug($"[PeakPelago] Skipping already-processed item #{helper.Index}: {itemName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogError("[PeakPelago] ItemReceived handler error: " + ex.Message);
                    }
                };


                _session.Socket.SendPacket(new SyncPacket());
                if (_reportedChecks.Count > 0)
                {
                    try
                    {
                        _session.Locations.CompleteLocationChecks(_reportedChecks.ToArray());
                        _log.LogInfo("[PeakPelago] Resubmitted " + _reportedChecks.Count + " previously checked locations.");
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning("[PeakPelago] Resubmit checks failed: " + ex.Message);
                    }
                }
                var loginResult = result as LoginSuccessful;
                if (loginResult != null && loginResult.SlotData != null)
                {
                    _log.LogInfo("[PeakPelago] Received slot data with " + loginResult.SlotData.Count + " entries");

                    _log.LogInfo("[PeakPelago] ===== ALL SLOT DATA =====");
                    foreach (var kvp in loginResult.SlotData)
                    {
                        _log.LogInfo($"[PeakPelago] Key: '{kvp.Key}' | Value: '{kvp.Value}' | Type: {kvp.Value?.GetType().Name}");
                    }
                    _log.LogInfo("[PeakPelago] ===== END SLOT DATA =====");

                    bool progressiveEnabled = false;
                    bool additionalEnabled = false;
                    bool deathLinkEnabled = false;
                    bool trapLinkEnabled = false;
                    bool ringLinkEnabled = false;
                    bool hardRingLinkEnabled = false;
                    bool energyLinkEnabled = false;

                    List<string> tags = new List<string>();

                    if (loginResult.SlotData.ContainsKey("ring_link"))
                    {
                        var value = loginResult.SlotData["ring_link"];
                        ringLinkEnabled = Convert.ToInt32(value) != 0;
                        _log.LogInfo($"[PeakPelago] Ring Link from slot data: {ringLinkEnabled}");

                        if (ringLinkEnabled)
                        {
                            tags.Add("RingLink");
                        }
                    }

                    if (loginResult.SlotData.ContainsKey("energy_link"))
                    {
                        var value = loginResult.SlotData["energy_link"];
                        energyLinkEnabled = Convert.ToInt32(value) != 0;
                        _log.LogInfo($"[PeakPelago] Energy Link from slot data: {energyLinkEnabled}");

                        if (energyLinkEnabled)
                        {
                            tags.Add("EnergyLink");
                        }
                    }

                    if (loginResult.SlotData.ContainsKey("hard_ring_link"))
                    {
                        var value = loginResult.SlotData["hard_ring_link"];
                        hardRingLinkEnabled = Convert.ToInt32(value) != 0;
                        _log.LogInfo($"[PeakPelago] Hard Ring Link from slot data: {hardRingLinkEnabled}");

                        if (hardRingLinkEnabled)
                        {
                            tags.Add("HardRingLink");
                        }
                    }

                    if (loginResult.SlotData.ContainsKey("trap_link"))
                    {
                        var value = loginResult.SlotData["trap_link"];
                        trapLinkEnabled = Convert.ToInt32(value) != 0;
                        _log.LogInfo($"[PeakPelago] Trap Link from slot data: {trapLinkEnabled}");

                        if (trapLinkEnabled)
                        {
                            tags.Add("TrapLink");
                        }
                    }



                    if (loginResult.SlotData.ContainsKey("death_link"))
                    {
                        var value = loginResult.SlotData["death_link"];
                        deathLinkEnabled = Convert.ToInt32(value) != 0;
                        _log.LogInfo($"[PeakPelago] Death Link from slot data: {deathLinkEnabled}");

                        if (deathLinkEnabled)
                        {
                            tags.Add("DeathLink");
                        }
                    }

                    if (tags.Count > 0)
                    {
                        var updatePacket = new ConnectUpdatePacket
                        {
                            Tags = tags.ToArray()
                        };
                        _session.Socket.SendPacket(updatePacket);
                        _log.LogInfo($"[PeakPelago] Sent tags: {string.Join(", ", tags)}");
                    }
                    if (ringLinkEnabled)
                    {
                        _ringLinkService.Initialize(_session, ringLinkEnabled);
                    }
                    if (hardRingLinkEnabled)
                    {
                        _hardRingLinkService.Initialize(_session, hardRingLinkEnabled);
                    }
                    if (energyLinkEnabled)
                    {
                        int teamNumber = 0;
                        if (loginResult.SlotData.ContainsKey("team"))
                        {
                            teamNumber = Convert.ToInt32(loginResult.SlotData["team"]);
                        }
                        
                        string teamName = teamNumber.ToString();
                        _energyLinkService.Initialize(_session, energyLinkEnabled, teamName);
                        CampfireModelSpawner.SetEnergyLinkService(_energyLinkService);
                    }

                    if (trapLinkEnabled)
                    {
                        HashSet<string> enabledTraps = new HashSet<string>();
                        if (loginResult.SlotData.ContainsKey("active_traps"))
                        {
                            try
                            {
                                var activeTrapsData = loginResult.SlotData["active_traps"];
                                _log.LogInfo($"[PeakPelago] Active traps data type: {activeTrapsData?.GetType().Name}");
                                _log.LogInfo($"[PeakPelago] Active traps raw value: {activeTrapsData}");

                                if (activeTrapsData is JObject activeTrapsObj)
                                {
                                    foreach (var kvp in activeTrapsObj)
                                    {
                                        string trapKey = kvp.Key;
                                        int weight = kvp.Value.ToObject<int>();
                                        string trapName = MapSlotKeyToTrapName(trapKey);
                                        if (weight > 0 && !string.IsNullOrEmpty(trapName))
                                        {
                                            enabledTraps.Add(trapName);
                                            _log.LogInfo($"[PeakPelago] Enabled trap: {trapName} (weight: {weight})");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _log.LogError($"[PeakPelago] Error parsing active_traps: {ex.Message}");
                                enabledTraps = TrapTypeExtensions.GetAllTrapNames();
                            }
                        }
                        else
                        {
                            _log.LogWarning("[PeakPelago] active_traps not found in slot data, enabling all traps");
                            enabledTraps = TrapTypeExtensions.GetAllTrapNames();
                        }

                        _log.LogInfo($"[PeakPelago] Initializing Trap Link with {enabledTraps.Count} enabled traps");
                        _trapLinkService.Initialize(
                            _session,
                            trapLinkEnabled,
                            cfgSlot.Value,
                            enabledTraps,
                            ApplyItemEffect
                        );
                    }
                    if (loginResult.SlotData.ContainsKey("goal"))
                    {
                        var value = loginResult.SlotData["goal"];
                        _slotGoalType = Convert.ToInt32(value);
                    }
                    else
                    {
                        _slotGoalType = cfgGoalType.Value;
                    }

                    if (loginResult.SlotData.ContainsKey("ascent_count"))
                    {
                        var value = loginResult.SlotData["ascent_count"];
                        _slotRequiredAscent = Convert.ToInt32(value);
                    }
                    else
                    {
                        _slotRequiredAscent = cfgRequiredAscent.Value;
                    }

                    if (loginResult.SlotData.ContainsKey("badge_count"))
                    {
                        var value = loginResult.SlotData["badge_count"];
                        _slotRequiredBadges = Convert.ToInt32(value);
                        _log.LogInfo($"[PeakPelago] Required badges from slot data: {_slotRequiredBadges}");
                    }
                    else
                    {
                        _slotRequiredBadges = cfgRequiredBadges.Value;
                        _log.LogWarning($"[PeakPelago] badge_count not found in slot data, using config: {_slotRequiredBadges}");
                    }

                    if (loginResult.SlotData.ContainsKey("death_link_behavior"))
                    {
                        var value = loginResult.SlotData["death_link_behavior"];
                        _deathLinkBehavior = Convert.ToInt32(value);
                        _log.LogInfo($"[PeakPelago] Death Link Behavior from slot data: {_deathLinkBehavior}");
                    }
                    else
                    {
                        _log.LogWarning("[PeakPelago] death_link_behavior not found in slot data");
                    }
                    if (loginResult.SlotData.ContainsKey("death_link_send_behavior"))
                    {
                        var value = loginResult.SlotData["death_link_send_behavior"];
                        _deathLinkSendBehavior = Convert.ToInt32(value);
                        _log.LogInfo($"[PeakPelago] Death Link Send Behavior: {_deathLinkSendBehavior}");
                    }
                    else
                    {
                        _log.LogWarning("[PeakPelago] death_link_send_behavior not found in slot data");
                    }

                    if (loginResult.SlotData.ContainsKey("progressive_stamina"))
                    {
                        var value = loginResult.SlotData["progressive_stamina"];
                        progressiveEnabled = Convert.ToInt32(value) != 0;
                    }
                    else
                    {
                        _log.LogWarning("[PeakPelago] progressive_stamina not found in slot data");
                    }

                    if (loginResult.SlotData.ContainsKey("additional_stamina_bars"))
                    {
                        var value = loginResult.SlotData["additional_stamina_bars"];
                        additionalEnabled = Convert.ToInt32(value) != 0;
                    }
                    else
                    {
                        _log.LogWarning("[PeakPelago] additional_stamina_bars not found in slot data");
                    }

                    _log.LogInfo($"[PeakPelago] Initializing stamina manager with progressive={progressiveEnabled}, additional={additionalEnabled}");
                    _staminaManager.Initialize(progressiveEnabled, additionalEnabled);
                }
                else
                {
                    _log.LogWarning("[PeakPelago] No slot data available, using default stamina settings");
                    _staminaManager.Initialize(false, false);
                }
                SaveState();
                _status = "Connected";
                _wantReconnect = false;
                _notifications.ShowConnected();
                _log.LogInfo("[PeakPelago] Connected.");
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
        
        private string MapSlotKeyToTrapName(string slotKey)
        {
            var mapping = new Dictionary<string, string>
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
                { "fungal_infection_trap", "Fungal Infection Trap" }
            };
            
            return mapping.TryGetValue(slotKey, out string trapName) ? trapName : null;
        }

        private void TryCloseSession()
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
                _status = "Disconnected";
            }
        }

        private void OnApMessage(LogMessage msg)
        {
            try
            {
                _log.LogInfo("[AP] " + msg.ToString());
                // hide some messages that are spammy or not useful to show
                if (msg.ToString().Contains("Cheat console:")) return;
                _notifications.ShowSimpleMessage(msg.ToString());
            }
            catch { /* ignore formatting errors */ }
        }

        private void Update()
        {
            try
            {
                _trapLinkService?.Update();
                ProcessItemQueue();
                CampfireModelSpawner.CleanupDestroyedCampfires();
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error in Update: {ex.Message}");
            }
        }

        /// <summary>
        /// Process items from the queue gradually to prevent overwhelming the game
        /// </summary>
        private void ProcessItemQueue()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!currentScene.StartsWith("Level_"))
            {
                return;
            }
            
            if (_itemQueue.Count == 0 || Time.time - _lastItemProcessed < ITEM_PROCESSING_COOLDOWN)
            {
                return;
            }
            
            var (itemName, isTrap, itemIndex) = _itemQueue.First.Value;
            _itemQueue.RemoveFirst();
            
            try
            {
                _log.LogInfo($"[PeakPelago] Processing queued item #{itemIndex}: {itemName} (Remaining: {_itemQueue.Count})");
                
                if (isTrap)
                {
                    _trapLinkService?.QueueTrap(itemName);
                }
                else
                {
                    ApplyItemEffect(itemName);
                }
                _lastProcessedItemIndex = itemIndex;
                SaveState();
                _lastItemProcessed = Time.time;
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error processing queued item {itemName}: {ex.Message}");
                _lastItemProcessed = Time.time;
            }
        }

        // ===== Tiny state persistence (no Unity JsonUtility dependency) ======
        private void CheckAndHandlePortChange()
        {
            try
            {
                // Get the current port from config
                string host = cfgServer.Value;
                string currentPort = host.Contains(":") ? host : (host + ":" + cfgPort.Value);

                // If port has changed, clear the cache and update the current port
                if (_currentPort != currentPort)
                {
                    if (!string.IsNullOrEmpty(_currentPort))
                    {
                        _log.LogInfo("[PeakPelago] Port changed from " + _currentPort + " to " + currentPort + " - clearing cache");
                        ClearCacheForPortChange();
                    }
                    _currentPort = currentPort;
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error checking port change: " + ex.Message);
            }
        }

        private void ClearCacheForPortChange()
        {
            try
            {
                // Clear all cached data
                _lastProcessedItemIndex = 0;
                _reportedChecks.Clear();
                _totalLuggageOpened = 0;
                _lastAcquiredItemName = "None";
                _lastAcquiredItemId = 0;
                _lastAcquiredItemTime = 0f;
                _awardedAscentBadges.Clear();
                _unlockedAscents.Clear();

                if (_staminaManager != null)
                {
                    _staminaManager.Initialize(false, false);
                }

                _log.LogInfo("[PeakPelago] Cleared all cached data for port change");
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error clearing cache for port change: " + ex.Message);
            }
        }

        private void LoadState()
        {
            try
            {
                CheckAndHandlePortChange();

                if (!File.Exists(StateFilePath))
                {
                    _log.LogInfo("[PeakPelago] No state file found for port " + _currentPort + " - starting fresh");
                    return;
                }
                
                string[] lines = File.ReadAllLines(StateFilePath);
                _log.LogInfo($"[PeakPelago] Loading state file with {lines.Length} lines");

                // Load item index (Line 1)
                if (lines.Length >= 1)
                {
                    if (int.TryParse(lines[0].Trim(), out int idx))
                    {
                        _lastProcessedItemIndex = idx;
                        _log.LogDebug($"[PeakPelago] Loaded item index: {idx}");
                    }
                    else
                    {
                        _log.LogWarning($"[PeakPelago] Failed to parse item index from: '{lines[0]}', using 0");
                    }
                }

                // Load reported checks (Line 2)
                if (lines.Length >= 2 && !string.IsNullOrEmpty(lines[1]))
                {
                    try
                    {
                        var parts = lines[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        int successCount = 0;
                        int failCount = 0;
                        
                        foreach (var p in parts)
                        {
                            if (long.TryParse(p.Trim(), out long id))
                            {
                                _reportedChecks.Add(id);
                                successCount++;
                            }
                            else
                            {
                                _log.LogWarning($"[PeakPelago] Failed to parse check ID: '{p}'");
                                failCount++;
                            }
                        }
                        
                        _log.LogInfo($"[PeakPelago] Loaded {successCount} reported checks ({failCount} failed)");
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"[PeakPelago] Error parsing reported checks: {ex.Message}");
                    }
                }

                // Load total luggage count (Line 3)
                if (lines.Length >= 3)
                {
                    if (int.TryParse(lines[2].Trim(), out int total))
                    {
                        _totalLuggageOpened = total;
                        _log.LogDebug($"[PeakPelago] Loaded luggage count: {total}");
                    }
                    else
                    {
                        _log.LogWarning($"[PeakPelago] Failed to parse luggage count from: '{lines[2]}', using 0");
                    }
                }

                // Skip line 4 (was item acquisition counts, but not needed atm)

                // Load stamina state (Line 5)
                if (lines.Length >= 5 && !string.IsNullOrEmpty(lines[4]))
                {
                    try
                    {
                        _staminaManager?.LoadState(lines[4]);
                        _log.LogDebug("[PeakPelago] Loaded stamina state");
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning($"[PeakPelago] Failed to load stamina state: {ex.Message}");
                    }
                }
                
                _log.LogInfo($"[PeakPelago] State loaded successfully: {_reportedChecks.Count} checks, {_totalLuggageOpened} luggage");
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] CRITICAL ERROR loading state file: " + ex.Message);
                _log.LogError("[PeakPelago] Stack trace: " + ex.StackTrace);
                _log.LogWarning("[PeakPelago] Starting with fresh state to avoid crashes");
                
                _reportedChecks.Clear();
                _totalLuggageOpened = 0;
                _lastProcessedItemIndex = 0;
            }
        }
        private void SaveState()
        {
            try
            {
                string line1 = _lastProcessedItemIndex.ToString();
                string line2 = string.Join(",", _reportedChecks.Select(x => x.ToString()).ToArray());
                string line3 = _totalLuggageOpened.ToString();
                string line4 = ""; // Reserved for future use (was item acquisition counts but we decided we dont need that atm so no point in saving it)
                string line5 = _staminaManager?.SaveState() ?? "0,1.00";
                
                // Write to temp file first, then rename to try and stop corruption
                string tempPath = StateFilePath + ".tmp";
                File.WriteAllLines(tempPath, new[] { line1, line2, line3, line4, line5 });
                
                if (File.Exists(StateFilePath))
                {
                    File.Delete(StateFilePath);
                }
                File.Move(tempPath, StateFilePath);
                
                _log.LogDebug("[PeakPelago] Saved state to port-specific file: " + _currentPort);
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to save state file: " + ex.Message);
                _log.LogError("[PeakPelago] Stack trace: " + ex.StackTrace);
                // Don't crash the game just because we couldn't save >:[
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