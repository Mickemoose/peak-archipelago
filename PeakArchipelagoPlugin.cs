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

namespace Peak.AP
{
    [BepInPlugin("com.mickemoose.peak.ap", "Peak Archipelago", "0.4.3")]
    public class PeakArchipelagoPlugin : BaseUnityPlugin
    {
        // ===== BepInEx / logging =====
        private ManualLogSource _log;
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
        private bool _hasOpenedLuggageThisSession = false; // Track if we've actually opened luggage this session
        private ProgressiveStaminaManager _staminaManager;
        private ArchipelagoNotificationManager _notifications;
        private PhotonView _photonView;
        private const string CHECK_RPC_NAME = "ReceiveCheckFromClient";

        // ===== Badge Management =====
        private HashSet<ACHIEVEMENTTYPE> _originalUnlockedBadges = new HashSet<ACHIEVEMENTTYPE>();
        private bool _badgesHidden = false;
        private bool _hasHiddenBadges = false;

        // ===== Ascent Management =====
        private int _originalMaxAscent = 0;
        private HashSet<int> _unlockedAscents = new HashSet<int>(); // Track which ascents are unlocked via AP items

        // ===== AP Link Management =====
        private RingLinkService _ringLinkService;
        private TrapLinkService _trapLinkService;
        private DeathLinkService _deathLinkService;
        private int _deathLinkBehavior = 0;
        private bool _deathLinkReceivedThisSession = false;
        private int _deathLinkSendBehavior = 0;
        private DateTime _lastDeathLinkSent = DateTime.MinValue;
        private DateTime _lastDeathLinkReceived = DateTime.MinValue;
        private string _lastDeathLinkSource = "None";
        private string _lastDeathLinkCause = "None";
        private static PeakArchipelagoPlugin _instance;
        public string Status => _status;
        private ArchipelagoUI _ui;
        private void Awake()
        {
            try
            {
                _log = Logger;
                _instance = this;

                // Debug log to confirm plugin is loading
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
                // Initialize stamina manager
                _staminaManager = new ProgressiveStaminaManager(_log);
                CharacterGetMaxStaminaPatch.SetStaminaManager(_staminaManager);
                CharacterClampStaminaPatch.SetStaminaManager(_staminaManager);
                StaminaBarUpdatePatch.SetStaminaManager(_staminaManager);
                BarAfflictionUpdateAfflictionPatch.SetStaminaManager(_staminaManager);
                _ringLinkService = new RingLinkService(_log);
                _trapLinkService = new TrapLinkService(_log);
                // Check for port changes and initialize port-specific caching
                CheckAndHandlePortChange();

                LoadState();
                _ui = gameObject.AddComponent<ArchipelagoUI>();
                _ui.Initialize(this);

                // Initialize item to location mapping
                InitializeItemMapping();

                // Initialize item effect handlers
                InitializeItemEffectHandlers();

                // Subscribe to achievement events for badge checking
                GlobalEvents.OnAchievementThrown += OnAchievementThrown;
                GlobalEvents.OnItemRequested += OnItemRequested;

                // Apply Harmony patches
                _log.LogInfo("[PeakPelago] About to apply Harmony patches...");
                _harmony = new Harmony("com.mickemoose.peak.ap");
                _harmony.PatchAll();
                _log.LogInfo("[PeakPelago] Harmony patches applied successfully");


                SetupPhotonView();

                // Hide existing badges after a short delay to let the game initialize
                Invoke(nameof(HideExistingBadges), 1f);

                // Store original ascent level
                Invoke(nameof(StoreOriginalAscent), 1f);

                _status = "Ready";
                _log.LogInfo("[PeakPelago] Plugin ready.");

                _notifications = new ArchipelagoNotificationManager(_log, cfgSlot.Value);
                Invoke(nameof(CountExistingBadges), 1.5f);
                
            }
            catch (System.Exception ex)
            {
                _log.LogError("[PeakPelago] CRITICAL ERROR during plugin initialization: " + ex.Message);
                _log.LogError("[PeakPelago] Stack trace: " + ex.StackTrace);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from achievement events
            GlobalEvents.OnAchievementThrown -= OnAchievementThrown;

            // Unsubscribe from item acquisition events
            GlobalEvents.OnItemRequested -= OnItemRequested;
            _ringLinkService?.Cleanup();
            _trapLinkService?.Cleanup();
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
                    
                    // Move all players to checkpoint
                    foreach (var character in validCharacters)
                    {
                        character.transform.position = checkpointPos;
                        _log.LogInfo($"[PeakPelago] Moved {character.characterName ?? "player"} to last checkpoint due to death link");
                    }
                }
                else
                {
                    
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
            // Wait a frame to ensure we're on the main thread
            yield return null;

            try
            {
                _log.LogInfo($"[PeakPelago] Executing death for {characterName}");
                
                // Use reflection to call DieInstantly
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
                { ACHIEVEMENTTYPE.BundledUpBadge, "Bundled Up Badge" }
            };
        }

        // ===== Luggage Achievement Checking =====

        private void CheckLuggageAchievements()
        {
            if (_session == null || !_hasOpenedLuggageThisSession) return; // Don't check until we've actually opened luggage

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
                // If we're not the host, send the check to the host via RPC
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

            int goalType = cfgGoalType.Value;

            // Only check if the goal is "Reach Peak"
            if (goalType == 0)
            {
                int requiredAscent = cfgRequiredAscent.Value;


                if (currentAscent >= requiredAscent)
                {
                    _log.LogInfo($"[PeakPelago] PEAK reached on Ascent {currentAscent} - goal complete!");
                    SendGoalComplete();

                    // Also report the specific ascent completion location
                    string completionLocation = $"Ascent {currentAscent} Completed";
                    ReportCheckByName(completionLocation);
                }
                else
                {
                    _log.LogInfo($"[PeakPelago] Peak reached but on Ascent {currentAscent}, need Ascent {requiredAscent} for goal");
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

            int goalType = cfgGoalType.Value;

            switch (goalType)
            {
                case 1: // Complete All Badges goal
                    int requiredBadges = cfgRequiredBadges.Value;
                    if (_collectedBadges.Count >= requiredBadges)
                    {
                        _log.LogInfo($"[PeakPelago] Collected {_collectedBadges.Count}/{requiredBadges} badges - goal complete!");
                        SendGoalComplete();
                    }
                    else
                    {
                        _log.LogInfo($"[PeakPelago] Progress: {_collectedBadges.Count}/{requiredBadges} badges collected");
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

        private Dictionary<string, int> _itemAcquisitionCounts = new Dictionary<string, int>();
        private Dictionary<string, int> _itemAcquisitionCountsThisRun = new Dictionary<string, int>();

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
                
                // Special items
                { "MAGIC BEAN", "Acquire Magic Bean" },
                { "PARASOL", "Acquire Parasol" },
                { "BALLOON", "Acquire Balloon" },
                { "BALLOON BUNCH", "Acquire Balloon Bunch" },
                { "SCOUT CANNON", "Acquire Scout Cannon" },
                { "FLYING DISC", "Acquire Flying Disc" },
                { "GUIDEBOOK", "Acquire Guidebook" },
                
                // Fire/light items
                { "PORTABLE STOVE", "Acquire PortableStovetopItem" },
                { "FIREWOOD", "Acquire FireWood" },
                { "LANTERN", "Acquire Lantern" },
                { "FLARE", "Acquire Flare" },
                { "TORCH", "Acquire Torch" },
                { "FAERIE LANTERN", "Acquire Lantern_Faerie" },
                
                // Navigation items
                { "CACTUS BALL", "Acquire CactusBall" },
                { "COMPASS", "Acquire Compass" },
                { "PIRATE COMPASS", "Acquire Pirate Compass" },
                { "BINOCULARS", "Acquire Binoculars" },
                
                // Medical items
                { "BANDAGES", "Acquire Bandages" },
                { "FIRST AID KIT", "Acquire FirstAidKit" },
                { "ANTIDOTE", "Acquire Antidote" },
                { "HEAT PACK", "Acquire Heat Pack" },
                { "CURE-ALL", "Acquire Cure-All" },
                { "REMEDY FUNGUS", "Acquire Remedy Fungus" },
                { "MEDICINAL ROOT", "Acquire Medicinal Root" },
                { "ALOE VERA", "Acquire Aloe Vera" },
                { "SUNSCREEN", "Acquire Sunscreen" },
                
                // Special objects
                { "SCOUT EFFIGY", "Acquire Scout Effigy" },
                { "CURSED SKULL", "Acquire Cursed Skull" },
                { "PANDORA'S LUNCHBOX", "Acquire Pandora's Lunchbox" },
                { "ANCIENT IDOL", "Acquire Ancient Idol" },
                { "STRANGE GEM", "Acquire Strange Gem" },
                
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
                { "EGG", "Acquire Egg" },
                { "TURKEY", "Acquire Turkey" },
                { "HONEYCOMB", "Acquire Honeycomb" },
                { "BEEHIVE", "Acquire Beehive" },
                
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
                { "YELLOW WINTERBERRY", "Acquire Yellow Winterberry" }
            };

            _log.LogInfo("[PeakPelago] Initialized item mapping with " + _itemToLocationMapping.Count + " items");
        }

        private void InitializeItemEffectHandlers()
        {
            _itemEffectHandlers = new Dictionary<string, System.Action>
            {
                // Physical Game Items (77000-77064) - Spawn directly
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
                { "Campfire", () => SpawnPhysicalItem("FireWood") },
                { "Lantern", () => SpawnPhysicalItem("Lantern") },
                { "Flare", () => SpawnPhysicalItem("Flare") },
                { "Torch", () => SpawnPhysicalItem("Torch") },
                { "Cactus", () => SpawnPhysicalItem("CactusBall") },
                { "Compass", () => SpawnPhysicalItem("Compass") },
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
                { "Sunscreen", () => SpawnPhysicalItem("Sunscreen") },
                { "Scout Effigy", () => SpawnPhysicalItem("ScoutEffigy") },
                { "Cursed Skull", () => SpawnPhysicalItem("Cursed Skull") },
                { "Pandora's Lunchbox", () => SpawnPhysicalItem("PandorasBox") },
                { "Ancient Idol", () => SpawnPhysicalItem("AncientIdol") },
                { "Strange Gem", () => SpawnPhysicalItem("Strange Gem") },
                { "Beehive", () => SpawnPhysicalItem("Beehive") },
                { "Honeycomb", () => SpawnPhysicalItem("Item_Honeycomb") },
                { "Egg", () => SpawnPhysicalItem("Egg") },
                { "Turkey", () => SpawnPhysicalItem("EggTurkey") },
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
                { "Spawn Bee Swarm", () => SpawnBeeSwarm() },
                { "Destroy Held Item", () => DestroyHeldItem() },
                { "Blue Berrynana Peel", () => SpawnPhysicalItem("Berrynana Peel Blue Variant") },
                { "Banana Peel Trap", () => SpawnPhysicalItem("Berrynana Peel Yellow") },
                { "Minor Poison Trap", () => PoisonTrapEffect.ApplyPoisonTrap(PoisonTrapEffect.PoisonTrapType.Minor, _log) },
                { "Poison Trap", () => PoisonTrapEffect.ApplyPoisonTrap(PoisonTrapEffect.PoisonTrapType.Normal, _log) },
                { "Deadly Poison Trap", () => PoisonTrapEffect.ApplyPoisonTrap(PoisonTrapEffect.PoisonTrapType.Deadly, _log) },
                { "Tornado Trap", () => TornadoTrapEffect.SpawnTornadoOnPlayer(_log) },
                { "Swap Trap", () => SwapTrapEffect.ApplyPositionSwapTrap(_log) },
                { "Nap Time Trap", () => NapTimeTrapEffect.ApplyNapTrap(_log) },
                { "Hungry Hungry Camper Trap", () => HungryHungryCamperTrapEffect.ApplyHungerTrap(_log) },
                { "Balloon Trap", () => BalloonTrapEffect.ApplyBalloonTrap(_log) },
                { "Slip Trap", () => SlipTrapEffect.ApplySlipTrap(_log) },
                { "Clear All Effects", () => ClearAllEffects() },
                { "Speed Upgrade", () => ApplySpeedUpgrade() },
                { "Cactus Ball Trap", () => SpawnPhysicalItem("CactusBall") },
                { "Freeze Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 1.0f, CharacterAfflictions.STATUSTYPE.Cold) },
                { "Cold Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.5f, CharacterAfflictions.STATUSTYPE.Cold) },
                { "Hot Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.5f, CharacterAfflictions.STATUSTYPE.Hot) },
                { "Injury Trap", () => AfflictionTrapEffect.ApplyAfflictionTrap(_log, AfflictionTrapEffect.TargetMode.RandomPlayer, 0.5f, CharacterAfflictions.STATUSTYPE.Injury) },
                { "Bounce Fungus", () => SpawnPhysicalItem("BounceShroom") },
                { "Instant Death Trap", () => InstantDeathTrapEffect.ApplyInstantDeathTrap(_log) },
                { "Yeet Trap", () => YeetItemTrapEffect.ApplyYeetTrap(_log)},
            };

            _log.LogInfo("[PeakPelago] Initialized item effect handlers with " + _itemEffectHandlers.Count + " items");
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
                
                if (Character.localCharacter == null)
                {
                    _staminaManager.ApplyStaminaUpgrade();
                    return;
                }          
                _staminaManager.ApplyStaminaUpgrade();
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error in ApplyProgressiveStamina: " + ex.Message);
                _log.LogError("[PeakPelago] Stack trace: " + ex.StackTrace);
            }
        }

        // Item Effect Implementation Methods
        private void SpawnPhysicalItem(string itemName)
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    return;
                }

                // Find the item by searching through all possible item IDs
                Item itemToSpawn = null;

                // DEBUG: Log all available items in the database
                _log.LogInfo("[PeakPelago] === AVAILABLE ITEMS IN DATABASE ===");
                for (ushort itemID = 1; itemID < 300; itemID++) // Limit to first 200 for now
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
                        if (item.name.Equals(itemName, System.StringComparison.OrdinalIgnoreCase))
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

                // Spawn item directly without adding to inventory (drop only)
                Vector3 spawnPosition = Character.localCharacter.Center + Character.localCharacter.transform.forward * 2f;
                spawnPosition += Vector3.up * 0.5f; // Slightly above ground

                // Spawn the item prefab directly without calling RequestPickup
                GameObject spawnedItem = PhotonNetwork.Instantiate("0_Items/" + itemToSpawn.name, spawnPosition, Quaternion.identity, 0);
            }
            catch (System.Exception ex)
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
                var ascentsType = System.Type.GetType("Ascents");
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
            catch (System.Exception ex)
            {
                _log.LogError("[PeakPelago] Error unlocking ascent " + ascentLevel + ": " + ex.Message);
            }
        }

        private void SpawnBeeSwarm()
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Cannot spawn bee swarm - no local character");
                    return;
                }

                // Spawn a bee swarm near the player
                Vector3 spawnPosition = Character.localCharacter.Center + Character.localCharacter.transform.forward * 3f;
                spawnPosition += Vector3.up * 1f;

                // Try to spawn a bee swarm using the existing system
                var beeSwarm = PhotonNetwork.Instantiate("BeeSwarm", spawnPosition, Quaternion.identity, 0);
                if (beeSwarm != null)
                {
                    _log.LogInfo("[PeakPelago] Spawned bee swarm at position " + spawnPosition);
                }
            }
            catch (System.Exception ex)
            {
                _log.LogError("[PeakPelago] Error spawning bee swarm: " + ex.Message);
            }
        }

        private void SpawnLightning()
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Cannot spawn lightning - no local character");
                    return;
                }

                // Spawn lightning near the player
                Vector3 spawnPosition = Character.localCharacter.Center + Character.localCharacter.transform.forward * 5f;
                spawnPosition += Vector3.up * 10f; // High above

                // Try to spawn lightning using the existing system
                var lightning = PhotonNetwork.Instantiate("Lightning", spawnPosition, Quaternion.identity, 0);
                if (lightning != null)
                {
                    _log.LogInfo("[PeakPelago] Spawned lightning at position " + spawnPosition);
                }
            }
            catch (System.Exception ex)
            {
                _log.LogError("[PeakPelago] Error spawning lightning: " + ex.Message);
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
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

        private void SpawnSmallLuggage()
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    _log.LogWarning("[PeakPelago] Cannot spawn luggage - no local character");
                    return;
                }

                // Spawn a small luggage near the player
                Vector3 spawnPosition = Character.localCharacter.Center + Character.localCharacter.transform.forward * 2f;
                spawnPosition += Vector3.up * 0.1f;

                // Try to spawn luggage using the existing system
                var luggage = PhotonNetwork.Instantiate("SmallLuggage", spawnPosition, Quaternion.identity, 0);
            }
            catch (System.Exception ex)
            {
                _log.LogError("[PeakPelago] Error spawning small luggage: " + ex.Message);
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
            // Check if this is a trap item
            var trapItems = new HashSet<string>
            {
                "Spawn Bee Swarm", "Destroy Held Item", "Minor Poison Trap", "Dynamite",
                "Poison Trap", "Deadly Poison Trap", "Tornado Trap", "Freeze Trap",
                "Nap Time Trap", "Balloon Trap", "Hungry Hungry Camper Trap", "Swap Trap",
                "Blue Berrynana Peel", "Yeet Trap", "Cactus Ball Trap", "Slip Trap"
            };
            return trapItems.Contains(itemName);
        }

        // Method to track when items are received from Archipelago
        public void TrackItemReceivedFromAP(string itemName)
        {
            try
            {
                _itemsReceivedFromAP++;
                _lastReceivedItemName = itemName;
                _lastReceivedItemTime = DateTime.Now;

                //_log.LogInfo("[PeakPelago] *** ITEM RECEIVED FROM ARCHIPELAGO ***: " + itemName + " (Total: " + _itemsReceivedFromAP + ")");

                // Apply the item effect
                ApplyItemEffect(itemName);
            }
            catch (System.Exception ex)
            {
                _log.LogError("[PeakPelago] Error tracking received item: " + ex.Message);
            }
        }

        private void CheckItemAcquisitionAchievements()
        {
            if (_session == null) return;

            try
            {
                // Check total item acquisition achievements for each item type
                foreach (var kvp in _itemAcquisitionCounts)
                {
                    string itemName = kvp.Key;
                    int totalCount = kvp.Value;

                    // Check various milestone achievements
                    CheckAndReportItemAchievement("Acquire " + itemName, 1, totalCount);
                    CheckAndReportItemAchievement("Acquire 5 " + itemName, 5, totalCount);
                    CheckAndReportItemAchievement("Acquire 10 " + itemName, 10, totalCount);
                    CheckAndReportItemAchievement("Acquire 25 " + itemName, 25, totalCount);
                }

                // Check single run achievements for each item type
                foreach (var kvp in _itemAcquisitionCountsThisRun)
                {
                    string itemName = kvp.Key;
                    int runCount = kvp.Value;

                    CheckAndReportItemAchievement("Acquire 3 " + itemName + " in a single run", 3, runCount);
                    CheckAndReportItemAchievement("Acquire 5 " + itemName + " in a single run", 5, runCount);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] CheckItemAcquisitionAchievements error: " + ex.Message);
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

            // Increment total count
            if (!_itemAcquisitionCounts.ContainsKey(itemName))
                _itemAcquisitionCounts[itemName] = 0;
            _itemAcquisitionCounts[itemName]++;

            // Increment run count
            if (!_itemAcquisitionCountsThisRun.ContainsKey(itemName))
                _itemAcquisitionCountsThisRun[itemName] = 0;
            _itemAcquisitionCountsThisRun[itemName]++;

            // Check if this item has an Archipelago location to report
            if (_itemToLocationMapping.TryGetValue(itemName.ToUpper(), out string locationName))
            {
                ReportCheckByName(locationName);
            }
            else
            {
                _log.LogDebug("[PeakPelago] No Archipelago location found for item: " + itemName);
            }

            // Check and report achievements
            CheckItemAcquisitionAchievements();

            // Save state
            SaveState();
        }

        /// <summary>Call this when a new run starts to reset run-specific counters</summary>
        public void ResetRunCounters()
        {
            _luggageOpenedThisRun = 0;
            _itemAcquisitionCountsThisRun.Clear();
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
                
                if (currentAscent < 1) 
                {
                    _log.LogInfo("[PeakPelago] HandleAscentPeakReached: Current ascent < 1, skipping badge award");
                    return; // Only award for ascents 1+ (currentAscent is 1-indexed)
                }


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

        // ===== Harmony Patches =====

        [HarmonyPatch(typeof(Character), "RPCA_Die")]
        public static class CharacterRPCADiePatch
        {
            static void Postfix(Character __instance)
            {
                try
                {
                    if (_instance == null) return;
                    if (_instance._deathLinkService == null) return;
                    
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

        [HarmonyPatch(typeof(Luggage), "Interact_CastFinished")]
        public static class LuggageInteractCastFinishedPatch
        {
            static void Postfix(object __instance, object interactor)
            {
                try
                {
                    if (_instance == null) return;
                    if (interactor == null) return;


                    // Get luggage name for logging using reflection
                    string luggageName = "Unknown";
                    if (__instance != null)
                    {
                        var getNameMethod = __instance.GetType().GetMethod("GetName");
                        if (getNameMethod != null)
                        {
                            luggageName = (string)getNameMethod.Invoke(__instance, null) ?? "Unknown";
                        }
                    }

                    _instance._log.LogInfo("[PeakPelago] Luggage opened: " + luggageName);

                    // Mark that we've opened luggage this session
                    _instance._hasOpenedLuggageThisSession = true;

                    // Increment counters
                    _instance._luggageOpenedCount++;
                    _instance._luggageOpenedThisRun++;
                    _instance._totalLuggageOpened++;

                    _instance._log.LogInfo("[PeakPelago] Luggage opened - Total: " + _instance._totalLuggageOpened + ", This run: " + _instance._luggageOpenedThisRun);

                    // Check and report all luggage-related achievements
                    _instance.CheckLuggageAchievements();

                    // Save state
                    _instance.SaveState();
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError("[PeakPelago] Luggage patch error: " + ex.Message);
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

        [HarmonyPatch(typeof(AchievementManager), "TestRequestedItem")]
        public static class AchievementManagerTestRequestedItemPatch
        {
            static void Postfix(Item item, Character character)
            {
                try
                {
                    if (_instance == null) return;

                    _instance._log.LogDebug("[PeakPelago] TestRequestedItemPatch: Item requested");

                    // Remove the isLocalPlayer check - allow any player to trigger

                    if (item != null)
                    {
                        // Get the item name using reflection
                        string itemName = "Unknown";
                        ushort itemId = 0;

                        try
                        {
                            // Try to get the item name using the GetName() method first
                            var getNameMethod = item.GetType().GetMethod("GetName");
                            if (getNameMethod != null)
                            {
                                itemName = (string)getNameMethod.Invoke(item, null) ?? "Unknown";
                                _instance._log.LogDebug("[PeakPelago] Got item name via GetName(): " + itemName);
                            }
                            else
                            {
                                // Fallback to UIData.itemName
                                var uidDataField = item.GetType().GetField("UIData");
                                if (uidDataField != null)
                                {
                                    var uidData = uidDataField.GetValue(item);
                                    if (uidData != null)
                                    {
                                        var itemNameField = uidData.GetType().GetField("itemName");
                                        if (itemNameField != null)
                                        {
                                            itemName = (string)itemNameField.GetValue(uidData) ?? "Unknown";
                                        }
                                    }
                                }
                            }

                            // Get the item ID using reflection
                            var itemIdField = item.GetType().GetField("itemID");
                            if (itemIdField != null)
                            {
                                itemId = (ushort)itemIdField.GetValue(item);
                                _instance._log.LogDebug("[PeakPelago] Got item ID: " + itemId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _instance._log.LogError("[PeakPelago] Error getting item info: " + ex.Message);
                        }

                        _instance._log.LogInfo("[PeakPelago] Player requested item: " + itemName + " (ID: " + itemId + ")");
                        _instance.TrackItemAcquisition(itemName, itemId);
                    }
                }
                catch (Exception ex)
                {
                    if (_instance != null)
                    {
                        _instance._log.LogError("[PeakPelago] TestRequestedItem patch error: " + ex.Message);
                    }
                }
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
                        _lastProcessedItemIndex = Mathf.Max(_lastProcessedItemIndex, helper.Index);
                        SaveState();
                        ItemFlags classification = info.Flags;
                        _notifications.ShowItemNotification(fromName, toName, itemName, classification);
                        if (IsTrapItem(itemName))
                        {
                            _trapLinkService?.QueueTrap(itemName);
                        }
                        _instance.ApplyItemEffect(itemName);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError("[PeakPelago] ItemReceived handler error: " + ex.Message);
                    }
                };

                // After (re)login, resync index (defensive) and resend any checks we tracked locally
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

                    if (loginResult.SlotData.ContainsKey("ring_link"))
                    {
                        var value = loginResult.SlotData["ring_link"];
                        bool ringLinkEnabled = Convert.ToInt32(value) != 0;
                        _ringLinkService.Initialize(_session, ringLinkEnabled);
                        _log.LogInfo($"[PeakPelago] Ring Link from slot data: {ringLinkEnabled}");
                    }

                    if (loginResult.SlotData.ContainsKey("trap_link"))
                    {
                        var value = loginResult.SlotData["trap_link"];
                        trapLinkEnabled = Convert.ToInt32(value) != 0;
                        _log.LogInfo($"[PeakPelago] Trap Link from slot data: {trapLinkEnabled}");
                    }

                    if (trapLinkEnabled)
                    {
                        var enabledTraps = new HashSet<string>
                        {
                            "Spawn Bee Swarm",
                            "Destroy Held Item",
                            "Minor Poison Trap",
                            "Poison Trap",
                            "Deadly Poison Trap",
                            "Tornado Trap",
                            "Nap Time Trap",
                            "Balloon Trap",
                            "Dynamite",
                            "Hungry Hungry Camper Trap",
                            "Blue Berrynana Peel",
                            "Yeet Item Trap"
                        };
                        _trapLinkService.Initialize(
                            _session,
                            trapLinkEnabled,
                            cfgSlot.Value,
                            enabledTraps,
                            ApplyItemEffect
                        );
                    }

                    if (loginResult.SlotData.ContainsKey("death_link"))
                    {
                        var value = loginResult.SlotData["death_link"];
                        deathLinkEnabled = Convert.ToInt32(value) != 0;
                        _log.LogInfo($"[PeakPelago] Death Link from slot data: {deathLinkEnabled}");
                    }
                    if (deathLinkEnabled)
                    {
                        _deathLinkService.EnableDeathLink();

                        var updatePacket = new ConnectUpdatePacket
                        {
                            Tags = new[] { "DeathLink" }
                        };

                        _session.Socket.SendPacket(updatePacket);
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
                // Print the simplified text of server messages
                _log.LogInfo("[AP] " + msg.ToString());
                _notifications.ShowSimpleMessage(msg.ToString(), true);
            }
            catch { /* ignore formatting errors */ }
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
            catch (System.Exception ex)
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
                _itemAcquisitionCounts.Clear();
                _itemAcquisitionCountsThisRun.Clear();
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
            catch (System.Exception ex)
            {
                _log.LogError("[PeakPelago] Error clearing cache for port change: " + ex.Message);
            }
        }

        private void LoadState()
        {
            try
            {
                // Check for port changes before loading state
                CheckAndHandlePortChange();

                if (!File.Exists(StateFilePath))
                {
                    _log.LogInfo("[PeakPelago] No state file found for port " + _currentPort + " - starting fresh");
                    return;
                }
                string[] lines = File.ReadAllLines(StateFilePath);

                // Load item index
                if (lines.Length >= 1)
                {
                    int idx;
                    if (int.TryParse(lines[0].Trim(), out idx))
                        _lastProcessedItemIndex = idx;
                }

                // Load reported checks
                if (lines.Length >= 2 && !string.IsNullOrEmpty(lines[1]))
                {
                    var parts = lines[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in parts)
                    {
                        long id;
                        if (long.TryParse(p.Trim(), out id))
                            _reportedChecks.Add(id);
                    }
                }

                // Load total luggage count
                if (lines.Length >= 3)
                {
                    int total;
                    if (int.TryParse(lines[2].Trim(), out total))
                        _totalLuggageOpened = total;
                }

                // Load item acquisition counts
                if (lines.Length >= 4 && !string.IsNullOrEmpty(lines[3]))
                {
                    var parts = lines[3].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        var colonIndex = part.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            string itemName = part.Substring(0, colonIndex);
                            string countStr = part.Substring(colonIndex + 1);
                            if (int.TryParse(countStr, out int count))
                            {
                                _itemAcquisitionCounts[itemName] = count;
                            }
                        }
                    }
                }

                if (lines.Length >= 5 && !string.IsNullOrEmpty(lines[4]))
                {
                    _staminaManager?.LoadState(lines[4]);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("[PeakPelago] Failed to load state file: " + ex.Message);
            }
        }

        private void SaveState()
        {
            try
            {
                string line1 = _lastProcessedItemIndex.ToString();
                string line2 = string.Join(",", _reportedChecks.Select(x => x.ToString()).ToArray());
                string line3 = _totalLuggageOpened.ToString();
                string line4 = string.Join(",", _itemAcquisitionCounts.Select(kvp => kvp.Key + ":" + kvp.Value).ToArray());
                string line5 = _staminaManager?.SaveState() ?? "0,1.00";
                File.WriteAllLines(StateFilePath, new[] { line1, line2, line3, line4, line5});
                _log.LogDebug("[PeakPelago] Saved state to port-specific file: " + _currentPort);
            }
            catch (Exception ex)
            {
                _log.LogWarning("[PeakPelago] Failed to save state file: " + ex.Message);
            }
        }

        // ===== Data Reset Methods =====

        private void ResetAllCachedData()
        {
            try
            {
                // Reset all counters
                _luggageOpenedCount = 0;
                _luggageOpenedThisRun = 0;
                _totalLuggageOpened = 0;
                _hasOpenedLuggageThisSession = false;

                // Reset item acquisition tracking
                _itemAcquisitionCounts.Clear();
                _itemAcquisitionCountsThisRun.Clear();
                _lastAcquiredItemName = "None";
                _lastAcquiredItemId = 0;
                _lastAcquiredItemTime = 0f;

                // Reset ascent badge tracking
                _awardedAscentBadges.Clear();

                // Reset badge management
                _originalUnlockedBadges.Clear();
                _badgesHidden = false;
                _hasHiddenBadges = false;

                // Reset ascent management
                _originalMaxAscent = 0;
                _unlockedAscents.Clear();

                // Clear reported checks
                _reportedChecks.Clear();

                // Reset item index
                _lastProcessedItemIndex = 0;

                // Delete the state file
                if (File.Exists(StateFilePath))
                {
                    File.Delete(StateFilePath);
                }

                // Clear current port to force re-initialization
                _currentPort = "";

            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to reset cached data: " + ex.Message);
            }
        }

        // ===== Achievement Event Handling =====

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
                // Only track acquisitions by the local character
                if (character != null && character.IsLocal && item != null)
                {
                    TrackItemAcquisition(item.UIData.itemName, item.itemID);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error handling item request event: " + ex.Message);
            }
        }
    }
}