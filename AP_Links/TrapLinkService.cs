using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using BepInEx.Logging;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Peak.AP
{
    public class TrapLinkService
    {
        private readonly ManualLogSource _log;
        private ArchipelagoSession _session;
        private bool _isEnabled;
        private string _playerName;
        
        // Queue of traps to activate
        private LinkedList<string> _trapQueue = new LinkedList<string>();
        private string _priorityTrap = null;
        private float _lastTrapActivation = 0f;
        private const float TRAP_ACTIVATION_COOLDOWN = 2f;

        // Callback to activate traps through the main plugin
        private System.Action<string, bool> _applyTrapEffect;
        
        // Mapping of PEAK trap names to standardized cross-game names (for SENDING)
        private Dictionary<string, string> _peakToStandardMapping;
        
        // Mapping of standardized/external trap names to PEAK trap names (for RECEIVING)
        private Dictionary<string, string> _standardToPeakMapping;

        // List of traps that can be sent/received
        private HashSet<string> _enabledTraps;
        private ArchipelagoNotificationManager _notifications;

        public TrapLinkService(ManualLogSource log, ArchipelagoNotificationManager notifications)
        {
            _log = log;
            _notifications = notifications;
            InitializeTrapMappings();
        }

        /// <summary>
        /// Initialize bidirectional trap name mappings
        /// </summary>
        private void InitializeTrapMappings()
        {
            // PEAK internal name -> Standardized cross-game name (for SENDING)
            _peakToStandardMapping = new Dictionary<string, string>
            {
                // Basic traps
                { "Spawn Bee Swarm", "Bee Trap" },
                { "Dynamite", "Bomb" },
                { "Banana Peel Trap", "Banana Peel Trap" },
                { "Minor Poison Trap", "Poison Mushroom" },
                { "Poison Trap", "Poison Trap" },
                { "Deadly Poison Trap", "Poison Trap" },
                { "Tornado Trap", "Meteor Trap" },
                { "Swap Trap", "Swap Trap" },
                { "Nap Time Trap", "Stun Trap" },
                { "Hungry Hungry Camper Trap", "Depletion Trap" },
                { "Balloon Trap", "Gravity Trap" },
                { "Slip Trap", "Slip Trap" },
                { "Freeze Trap", "Freeze Trap" },
                { "Cold Trap", "Ice Trap" },
                { "Hot Trap", "Fire Trap" },
                { "Injury Trap", "Damage Trap" },
                { "Cactus Ball Trap", "Spike Ball Trap" },
                { "Instant Death Trap", "Instant Death Trap" },
                { "Yeet Trap", "Whoops! Trap" },
                { "Tumbleweed Trap", "Tip Trap"},
                { "Zombie Horde Trap", "Spooky Time"},
                { "Gust Trap", "Get Out Trap"},
                { "Mandrake Trap", "OmoTrap"},
                { "Fungal Infection Trap", "Posession Trap"}
            };

            // Standardized/External trap name -> PEAK internal name (for RECEIVING)
            _standardToPeakMapping = new Dictionary<string, string>
            {
                // ===== TrapLink standard traps that map to PEAK traps =====

                { "Bee Trap", "Spawn Bee Swarm" },
                { "Eject Ability", "Yeet Trap" },
                { "Whoops! Trap", "Yeet Trap" },
                { "Banana Peel Trap", "Banana Peel Trap" },
                { "Banana Trap", "Banana Peel Trap" },
                { "Slip Trap", "Slip Trap" },
                { "Poison Mushroom", "Minor Poison Trap" },
                { "Poison Trap", "Poison Trap" },
                { "TNT Barrel Trap", "Dynamite" },
                { "Swap Trap", "Swap Trap" },
                { "Flip Trap", "Slip Trap" },
                { "Stun Trap", "Nap Time Trap" },
                { "Paralyze Trap", "Nap Time Trap" },
                { "Slowness Trap", "Nap Time Trap" },
                { "Slow Trap", "Nap Time Trap" },
                { "Depletion Trap", "Hungry Hungry Camper Trap" },
                { "Dry Trap", "Hungry Hungry Camper Trap" },
                { "Gravity Trap", "Balloon Trap" },
                { "Bubble Trap", "Balloon Trap" },
                { "Spring Trap", "Balloon Trap" },
                { "Freeze Trap", "Freeze Trap" },
                { "Ice Trap", "Cold Trap" },
                { "Ice Floor Trap", "Cold Trap" },
                { "Frozen Trap", "Freeze Trap" },
                { "Fire Trap", "Hot Trap" },
                { "Icy Hot Pants Trap", "Hot Trap" },
                { "Damage Trap", "Injury Trap" },
                { "Double Damage", "Injury Trap" },
                { "Electrocution Trap", "Injury Trap" },
                { "Spike Ball Trap", "Cactus Ball Trap" },
                { "Cursed Ball Trap", "Cactus Ball Trap" },
                { "Instant Death Trap", "Instant Death Trap" },
                { "One Hit KO", "Instant Death Trap" },
                { "Tip Trap", "Tumbleweed Trap"},
                { "Items to Bombs", "Items to Bombs"},
                { "Spooky Time", "Zombie Horde Trap"},
                { "Army Trap", "Zombie Horde Trap"},
                { "Police Trap", "Zombie Horde Trap"},
                { "Meteor Trap", "Tornado Trap" },
                { "Get Out Trap", "Gust Trap"},
                { "Resistance Trap", "Gust Trap"},
                { "OmoTrap", "Mandrake Trap"},
                { "Posession Trap", "Fungal Infection Trap"}
            };

            _log.LogInfo($"[PeakPelago] Initialized trap mappings: {_peakToStandardMapping.Count} outgoing, {_standardToPeakMapping.Count} incoming");
        }

        /// <summary>
        /// Initialize the Trap Link service
        /// </summary>
        public void Initialize(
            ArchipelagoSession session, 
            bool enabled, 
            string playerName, 
            HashSet<string> enabledTraps,
            System.Action<string, bool> applyTrapEffectCallback)
        {
            _session = session;
            _isEnabled = enabled;
            _playerName = playerName;
            _enabledTraps = enabledTraps ?? new HashSet<string>();
            _applyTrapEffect = applyTrapEffectCallback;

            if (_session != null && _isEnabled)
            {
                _session.Socket.PacketReceived += OnPacketReceived;
                _log.LogInfo($"[PeakPelago] Trap Link service initialized for player: {_playerName}");
                _log.LogInfo($"[PeakPelago] Enabled traps: {string.Join(", ", _enabledTraps)}");
            }
        }

        /// <summary>
        /// Enable or disable Trap Link
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            _log.LogInfo($"[PeakPelago] Trap Link {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Send a Trap Link packet when receiving a trap item
        /// </summary>
        public void SendTrapLink(string peakTrapName, bool fromTrapLink = false)
        {
            if (_session == null || !_isEnabled || fromTrapLink)
            {
                return;
            }

            try
            {
                // Convert my trap names to standard TrapLink names
                string standardizedTrapName = peakTrapName;
                if (_peakToStandardMapping.TryGetValue(peakTrapName, out string mappedName))
                {
                    standardizedTrapName = mappedName;
                    _log.LogDebug($"[PeakPelago] Mapped '{peakTrapName}' to '{standardizedTrapName}' for sending");
                    
                }
                else
                {
                    _log.LogWarning($"[PeakPelago] No standardized mapping for '{peakTrapName}', sending as-is");
                    return;
                }

                var trapLinkData = new Dictionary<string, JToken>
                {
                    { "time", JToken.FromObject(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) },
                    { "source", JToken.FromObject(_playerName) },
                    { "trap_name", JToken.FromObject(standardizedTrapName) }
                };

                var bouncePacket = new BouncePacket
                {
                    Tags = new List<string> { "TrapLink" },
                    Data = trapLinkData
                };

                _session.Socket.SendPacket(bouncePacket);
                _log.LogInfo($"[PeakPelago] Sent Trap Link: '{standardizedTrapName}' (from '{peakTrapName}')");
                _notifications.ShowTrapLinkNotification($"TrapLink: sent {standardizedTrapName}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to send Trap Link: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle incoming Archipelago packets
        /// </summary>
        private void OnPacketReceived(ArchipelagoPacketBase packet)
        {
            try
            {
                if (packet is BouncePacket bounce)
                {
                    if (bounce.Tags != null && bounce.Tags.Contains("TrapLink") && _isEnabled)
                    {
                        HandleTrapLinkReceived(bounce.Data);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error handling Trap Link packet: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle incoming Trap Link packets
        /// </summary>
        private void HandleTrapLinkReceived(Dictionary<string, JToken> data)
        {
            try
            {
                // Don't process our own Trap Links
                if (data.ContainsKey("source"))
                {
                    string source = data["source"].ToObject<string>();
                    if (source == _playerName)
                    {
                        _log.LogDebug("[PeakPelago] Ignoring own Trap Link");
                        return;
                    }
                }

                if (data.ContainsKey("trap_name"))
                {
                    string externalTrapName = data["trap_name"].ToObject<string>();
                    string source = data.ContainsKey("source") ? data["source"].ToObject<string>() : "Unknown";

                    _log.LogInfo($"[PeakPelago] Trap Link received: '{externalTrapName}'");
                    
                    
                    // Map TrapLink trap names to my trap names
                    string peakTrapName = MapToPeakTrap(externalTrapName);
                    
                    if (peakTrapName != null && _enabledTraps.Contains(peakTrapName))
                    {
                        _priorityTrap = peakTrapName;
                        _notifications.ShowTrapLinkNotification($"TrapLink: received {externalTrapName} from {source}");
                        _log.LogInfo($"[PeakPelago] Set priority trap: '{peakTrapName}' (from '{externalTrapName}')");
                    }
                    else if (peakTrapName == null)
                    {
                        _log.LogDebug($"[PeakPelago] No PEAK mapping for trap '{externalTrapName}'");
                    }
                    else
                    {
                        _log.LogDebug($"[PeakPelago] Trap '{peakTrapName}' not enabled");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to handle Trap Link: {ex.Message}");
            }
        }

        private string MapToPeakTrap(string externalTrapName)
        {
            if (_standardToPeakMapping.TryGetValue(externalTrapName, out string peakTrapName))
            {
                return peakTrapName;
            }
            
            // No mapping found
            return null;
        }

        /// <summary>
        /// Update trap queue processing
        /// </summary>
        public void Update()
        {
            if (!_isEnabled || _session == null) return;

            // Cooldown between trap activations
            if (Time.time - _lastTrapActivation < TRAP_ACTIVATION_COOLDOWN) return;

            if (_priorityTrap != null)
            {
                if (CanActivateTrap(_priorityTrap))
                {
                    ActivateTrap(_priorityTrap, fromTrapLink: true);
                    _lastTrapActivation = Time.time;
                }
                else
                {
                    _log.LogDebug($"[PeakPelago] Priority trap '{_priorityTrap}' not activatable, discarding");
                }
                
                _priorityTrap = null;
                return;
            }

            if (_trapQueue.Count > 0)
            {
                string trap = _trapQueue.First.Value;
                _trapQueue.RemoveFirst();
                
                if (CanActivateTrap(trap))
                {
                    ActivateTrap(trap, fromTrapLink: false);
                    _lastTrapActivation = Time.time;
                }
            }
        }

        /// <summary>
        /// Check if a trap can be activated right now
        /// </summary>
        private bool CanActivateTrap(string trapName)
        {
            if (Character.localCharacter == null) return false;
            if (Character.localCharacter.data.dead) return false;
            if (Character.localCharacter.data.fullyPassedOut) return false;

            return true;
        }

        private void ActivateTrap(string trapName, bool fromTrapLink)
        {
            try
            {
                _log.LogInfo($"[PeakPelago] Activating trap: '{trapName}' (fromTrapLink: {fromTrapLink})");
                
                _applyTrapEffect?.Invoke(trapName, fromTrapLink);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to activate trap '{trapName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Queue a trap for activation (for traps received from Archipelago)
        /// </summary>
        public void QueueTrap(string trapName)
        {
            if (_isEnabled && _enabledTraps.Contains(trapName))
            {
                _trapQueue.AddLast(trapName);
                _log.LogInfo($"[PeakPelago] Queued trap: '{trapName}'");
            }
        }

        public void Cleanup()
        {
            if (_session != null)
            {
                _session.Socket.PacketReceived -= OnPacketReceived;
            }
            
            _session = null;
            _isEnabled = false;
            _trapQueue.Clear();
            _priorityTrap = null;
        }
    }
}