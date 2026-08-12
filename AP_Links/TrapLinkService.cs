using System;
using System.Collections.Generic;
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
        private LinkedList<string> _trapQueue = new LinkedList<string>();
        private volatile string _priorityTrap = null;
        private float _lastTrapActivation = 0f;
        private const float TRAP_ACTIVATION_COOLDOWN = 5f;
        private string _activeTrap = null;
        private float _activeTrapTime = 0f;
        private const float ACTIVE_TRAP_DISPLAY_DURATION = 5f;
        private bool _trapInProgress = false;
        private const float TRAP_TIMEOUT = 120f;

        /// <summary>
        /// Call this when a trap effect finishes so the queue can proceed.
        /// </summary>
        public void NotifyTrapComplete()
        {
            _trapInProgress = false;
            _log.LogInfo("[PeakPelago] Trap completed, queue can proceed");
        }

        public bool IsTrapInProgress => _trapInProgress;
        private Action<string, bool> _applyTrapEffect;
        private Dictionary<string, string> _peakToStandardMapping;
        private Dictionary<string, string> _standardToPeakMapping;
        private HashSet<string> _enabledTraps;
        private ArchipelagoNotificationManager _notifications;

        /// <summary>
        /// All known standard TrapLink trap names from the cross-game ecosystem.
        /// Use these when registering traps to ensure correct spelling.
        /// </summary>
        public static class StandardTraps
        {
            public const string Trap144p = "144p Trap";
            public const string Aaa = "Aaa Trap";
            public const string Animal = "Animal Trap";
            public const string AnimalBonus = "Animal Bonus Trap";
            public const string Army = "Army Trap";
            public const string Bald = "Bald Trap";
            public const string BananaPeel = "Banana Peel Trap";
            public const string Banana = "Banana Trap";
            public const string Banner = "Banner Trap";
            public const string Bee = "Bee Trap";
            public const string BlueBalls = "Blue Balls Curse";
            public const string Bomb = "Bomb";
            public const string BombTrap = "Bomb Trap";
            public const string Bonk = "Bonk Trap";
            public const string Breakout = "Breakout Trap";
            public const string Bubble = "Bubble Trap";
            public const string BulletTime = "Bullet Time Trap";
            public const string Burn = "Burn Trap";
            public const string Buyon = "Buyon Trap";
            public const string CameraRotate = "Camera Rotate Trap";
            public const string ChaosControl = "Chaos Control Trap";
            public const string ChartModifier = "Chart Modifier Trap";
            public const string Chaser = "Chaser Trap";
            public const string ClearImage = "Clear Image Trap";
            public const string Confound = "Confound Trap";
            public const string Confuse = "Confuse Trap";
            public const string Confusion = "Confusion Trap";
            public const string ControlBall = "Control Ball Trap";
            public const string ControllerDrift = "Controller Drift Trap";
            public const string CursedBall = "Cursed Ball Trap";
            public const string Cutscene = "Cutscene Trap";
            public const string Damage = "Damage Trap";
            public const string Deisometric = "Deisometric Trap";
            public const string Depletion = "Depletion Trap";
            public const string DisableA = "Disable A Trap";
            public const string DisableB = "Disable B Trap";
            public const string DisableCUp = "Disable C Up Trap";
            public const string DisableTag = "Disable Tag Trap";
            public const string DisableZ = "Disable Z Trap";
            public const string Disarm = "Disarm Trap";
            public const string DoubleDamage = "Double Damage";
            public const string Dry = "Dry Trap";
            public const string EjectAbility = "Eject Ability";
            public const string Electrocution = "Electrocution Trap";
            public const string EmptyItemBox = "Empty Item Box Trap";
            public const string EnemyBall = "Enemy Ball Trap";
            public const string EnergyDrain = "Energy Drain Trap";
            public const string ExpensiveStocks = "Expensive Stocks";
            public const string Explosion = "Explosion Trap";
            public const string Exposition = "Exposition Trap";
            public const string FakeTransition = "Fake Transition";
            public const string Fast = "Fast Trap";
            public const string Fear = "Fear Trap";
            public const string Fire = "Fire Trap";
            public const string FishEye = "Fish Eye Trap";
            public const string Fishing = "Fishing Trap";
            public const string FishinBoo = "Fishin' Boo Trap";
            public const string FlipHorizontal = "Flip Horizontal Trap";
            public const string Flip = "Flip Trap";
            public const string FlipVertical = "Flip Vertical Trap";
            public const string FrameSlime = "Frame Slime Trap";
            public const string Freeze = "Freeze Trap";
            public const string Frog = "Frog Trap";
            public const string Frost = "Frost Trap";
            public const string Frozen = "Frozen Trap";
            public const string Fuzzy = "Fuzzy Trap";
            public const string GadgetShuffle = "Gadget Shuffle Trap";
            public const string GetOut = "Get Out Trap";
            public const string Ghost = "Ghost";
            public const string GhostChat = "Ghost Chat";
            public const string GooeyBag = "Gooey Bag";
            public const string Gravity = "Gravity Trap";
            public const string Help = "Help Trap";
            public const string Hey = "Hey! Trap";
            public const string Hiccup = "Hiccup Trap";
            public const string Home = "Home Trap";
            public const string Honey = "Honey Trap";
            public const string IceFloor = "Ice Floor Trap";
            public const string Ice = "Ice Trap";
            public const string IcyHotPants = "Icy Hot Pants Trap";
            public const string InputSequence = "Input Sequence Trap";
            public const string InstantCrystal = "Instant Crystal Trap";
            public const string InstantDeath = "Instant Death Trap";
            public const string InvertColors = "Invert Colors Trap";
            public const string InvertedMouse = "Inverted Mouse Trap";
            public const string Invisiball = "Invisiball Trap";
            public const string Invisible = "Invisible Trap";
            public const string Invisibility = "Invisibility Trap";
            public const string IronBoots = "Iron Boots Trap";
            public const string ItemsToBombs = "Items to Bombs";
            public const string Jump = "Jump Trap";
            public const string JumpingJacks = "Jumping Jacks Trap";
            public const string Laughter = "Laughter Trap";
            public const string LightUpPath = "Light Up Path Trap";
            public const string Literature = "Literature Trap";
            public const string ManaDrain = "Mana Drain Trap";
            public const string MarketCrash = "Market Crash Trap";
            public const string MathQuiz = "Math Quiz Trap";
            public const string Meteor = "Meteor Trap";
            public const string Metronome = "Metronome Trap";
            public const string Mirror = "Mirror Trap";
            public const string MonkeyMash = "Monkey Mash Trap";
            public const string MyTurn = "My Turn! Trap";
            public const string Ninja = "Ninja Trap";
            public const string NoGuarding = "No Guarding";
            public const string NoPetals = "No Petals";
            public const string NoRevivals = "No Revivals";
            public const string NoStocks = "No Stocks";
            public const string NoVac = "No Vac Trap";
            public const string NumberSequence = "Number Sequence Trap";
            public const string Nut = "Nut Trap";
            public const string Omo = "OmoTrap";
            public const string OneHitKO = "One Hit KO";
            public const string Paper = "Paper Trap";
            public const string Paralyze = "Paralyze Trap";
            public const string Paralysis = "Paralysis Trap";
            public const string Phone = "Phone Trap";
            public const string Pie = "Pie Trap";
            public const string Pinball = "Pinball Trap";
            public const string Pixelate = "Pixelate Trap";
            public const string Pixellation = "Pixellation Trap";
            public const string PoisonMushroom = "Poison Mushroom";
            public const string Poison = "Poison Trap";
            public const string PokemonCount = "Pokemon Count Trap";
            public const string PokemonTrivia = "Pokemon Trivia Trap";
            public const string Police = "Police Trap";
            public const string PONGChallenge = "PONG Challenge";
            public const string Pong = "Pong Trap";
            public const string Posession = "Posession Trap";
            public const string PowerPoint = "PowerPoint Trap";
            public const string Push = "Push Trap";
            public const string Radiation = "Radiation Trap";
            public const string Rail = "Rail Trap";
            public const string Ranch = "Ranch Trap";
            public const string RandomStatus = "Random Status Trap";
            public const string Resistance = "Resistance Trap";
            public const string Reversal = "Reversal Trap";
            public const string ReverseControls = "Reverse Controls Trap";
            public const string Reverse = "Reverse Trap";
            public const string Rockfall = "Rockfall Trap";
            public const string Sandstorm = "Sandstorm Trap";
            public const string ScreenFlip = "Screen Flip Trap";
            public const string Shake = "Shake Trap";
            public const string Sleep = "Sleep Trap";
            public const string Slip = "Slip Trap";
            public const string Slow = "Slow Trap";
            public const string Slowness = "Slowness Trap";
            public const string Snake = "Snake Trap";
            public const string Spam = "Spam Trap";
            public const string SpikeBall = "Spike Ball Trap";
            public const string Spooky = "Spooky Time";
            public const string Spotlight = "Spotlight Trap";
            public const string Spring = "Spring Trap";
            public const string Squash = "Squash Trap";
            public const string StickyFloor = "Sticky Floor Trap";
            public const string StickyHands = "Sticky Hands Trap";
            public const string Stun = "Stun Trap";
            public const string SvCEffect = "SvC Effect";
            public const string Swap = "Swap Trap";
            public const string Tarr = "Tarr Trap";
            public const string Teleport = "Teleport Trap";
            public const string Text = "Text Trap";
            public const string Thwimp = "Thwimp Trap";
            public const string Thwomp = "Thwomp Trap";
            public const string TimeLimit = "Time Limit";
            public const string TimeWarp = "Time Warp Trap";
            public const string Timer = "Timer Trap";
            public const string Tiny = "Tiny Trap";
            public const string Tip = "Tip Trap";
            public const string TNTBarrel = "TNT Barrel Trap";
            public const string TNT = "TNT Trap";
            public const string ToolSwap = "Tool Swap Trap";
            public const string Trivia = "Trivia Trap";
            public const string Tutorial = "Tutorial Trap";
            public const string Underwater = "Underwater Trap";
            public const string Undo = "Undo Trap";
            public const string UNOChallenge = "UNO Challenge";
            public const string Void = "Void Trap";
            public const string WarpBall = "Warp Ball Trap";
            public const string WarpPipe = "Warp Pipe Trap";
            public const string Whirlpool = "Whirlpool Trap";
            public const string Whoops = "Whoops! Trap";
            public const string WIDE = "W I D E Trap";
            public const string Wind = "Wind Trap";
            public const string Wither = "Wither Trap";
            public const string ZoomIn = "Zoom In Trap";
            public const string ZoomOut = "Zoom Out Trap";
            public const string Zoom = "Zoom Trap";
        }

        public TrapLinkService(ManualLogSource log, ArchipelagoNotificationManager notifications)
        {
            _log = log;
            _notifications = notifications;
            _peakToStandardMapping = new Dictionary<string, string>();
            _standardToPeakMapping = new Dictionary<string, string>();
            RegisterAllTraps();
        }

        /// <summary>
        /// Register a PEAK trap's TrapLink mapping.
        /// sendsAs: the standard name to broadcast (null = don't send).
        /// receivesAs: standard names that trigger this trap (empty = don't receive).
        /// </summary>
        private void Register(string peakTrapName, string sendsAs, params string[] receivesAs)
        {
            if (sendsAs != null)
            {
                _peakToStandardMapping[peakTrapName] = sendsAs;
            }

            foreach (var standardName in receivesAs)
            {
                _standardToPeakMapping[standardName] = peakTrapName;
            }
        }

        /// <summary>
        /// Register all trap mappings between PEAK and the standard TrapLink ecosystem.
        /// Add new mappings here as new traps are added to PEAK, or as new standard trap names are introduced.
        /// Try to use existing standard trap names where possible to maintain cross-game consistency. (ie DONT FOLLOW THE TREND IM NOTICING WHERE SOMEONE MAKES 3 INVISIBLE TRAPS WITH DIFFERENT NAMES COME ON PEOPLE LMAO)
        /// </summary>
        private void RegisterAllTraps()
        {
            Register("Spawn Bee Swarm", StandardTraps.Bee,
                StandardTraps.Bee, StandardTraps.Honey);

            Register("Dynamite", StandardTraps.Bomb,
                StandardTraps.TNTBarrel, StandardTraps.TNT, StandardTraps.BombTrap, StandardTraps.Bomb);

            Register("Banana Peel Trap", StandardTraps.BananaPeel,
                StandardTraps.BananaPeel, StandardTraps.Banana);

            Register("Minor Poison Trap", StandardTraps.PoisonMushroom,
                StandardTraps.PoisonMushroom);

            Register("Poison Trap", StandardTraps.Poison,
                StandardTraps.Poison);

            Register("Deadly Poison Trap", StandardTraps.Poison);

            Register("Tornado Trap", StandardTraps.Meteor,
                StandardTraps.Meteor, StandardTraps.Whirlpool, StandardTraps.Sandstorm);

            Register("Swap Trap", StandardTraps.Swap,
                StandardTraps.Swap);

            Register("Nap Time Trap", StandardTraps.Stun,
                StandardTraps.Stun, StandardTraps.Paralyze, StandardTraps.Slowness, StandardTraps.Slow);

            Register("Hungry Hungry Camper Trap", StandardTraps.Depletion,
                StandardTraps.Depletion, StandardTraps.Dry);

            Register("Balloon Trap", StandardTraps.Gravity,
                StandardTraps.Gravity, StandardTraps.Bubble, StandardTraps.Spring);

            Register("Slip Trap", StandardTraps.Slip,
                StandardTraps.Slip, StandardTraps.Flip, StandardTraps.FlipVertical, StandardTraps.FlipHorizontal);

            Register("Freeze Trap", StandardTraps.Freeze,
                StandardTraps.Freeze, StandardTraps.Frozen);

            Register("Cold Trap", StandardTraps.Ice,
                StandardTraps.Ice, StandardTraps.IceFloor);

            Register("Hot Trap", StandardTraps.Fire,
                StandardTraps.Fire, StandardTraps.IcyHotPants, StandardTraps.Burn);

            Register("Injury Trap", StandardTraps.Damage,
                StandardTraps.Damage, StandardTraps.DoubleDamage, StandardTraps.Electrocution);

            Register("Cactus Ball Trap", StandardTraps.SpikeBall,
                StandardTraps.SpikeBall, StandardTraps.CursedBall, StandardTraps.StickyHands);

            Register("Instant Death Trap", StandardTraps.InstantDeath,
                StandardTraps.InstantDeath, StandardTraps.OneHitKO);

            Register("Yeet Trap", StandardTraps.Whoops,
                StandardTraps.EjectAbility, StandardTraps.Whoops);

            Register("Tumbleweed Trap", StandardTraps.Tip,
                StandardTraps.Tip);

            Register("Zombie Horde Trap", StandardTraps.Spooky,
                StandardTraps.Spooky, StandardTraps.Army, StandardTraps.Police);

            Register("Gust Trap", StandardTraps.GetOut,
                StandardTraps.GetOut, StandardTraps.Resistance);

            Register("Mandrake Trap", StandardTraps.Omo,
                StandardTraps.Omo);

            Register("Fungal Infection Trap", StandardTraps.Posession,
                StandardTraps.Posession);

            Register("Items to Bombs", StandardTraps.ItemsToBombs,
                StandardTraps.ItemsToBombs);

            Register("Pokemon Trivia Trap", StandardTraps.PokemonTrivia,
                StandardTraps.PokemonTrivia);

            Register("Pokemon Count Trap", StandardTraps.PokemonCount,
                StandardTraps.PokemonCount);

            Register("Blackout Trap", StandardTraps.Confuse,
                StandardTraps.Confuse, StandardTraps.Confusion, StandardTraps.Confound, StandardTraps.InvertColors);

            Register("Fear Trap", StandardTraps.Fear,
                StandardTraps.Fear);

            Register("Scoutmaster Trap", StandardTraps.MyTurn,
                StandardTraps.MyTurn, StandardTraps.FishinBoo, StandardTraps.DisableTag, StandardTraps.Chaser);

            Register("Zoom Trap", StandardTraps.Zoom,
                StandardTraps.Zoom, StandardTraps.WIDE, StandardTraps.ZoomIn, StandardTraps.ZoomOut);

            Register("Screen Flip Trap", StandardTraps.ScreenFlip,
                StandardTraps.ScreenFlip, StandardTraps.CameraRotate);

            Register("Drop Everything Trap", StandardTraps.EmptyItemBox,
                StandardTraps.EmptyItemBox, StandardTraps.GadgetShuffle, StandardTraps.Disarm);

            Register("Pixel Trap", StandardTraps.Trap144p,
                StandardTraps.Trap144p, StandardTraps.Pixellation, StandardTraps.Pixelate);

            Register("Eruption Trap", StandardTraps.LightUpPath,
                StandardTraps.LightUpPath);

            Register("Beetle Horde Trap", StandardTraps.Buyon,
                StandardTraps.Buyon, StandardTraps.Animal);

            Register("Custom Trivia Trap", StandardTraps.Trivia,
                StandardTraps.Trivia);

            Register("Stamina Drain Trap", StandardTraps.EnergyDrain,
                StandardTraps.EnergyDrain, StandardTraps.ManaDrain);

            Register("Chaos Control Trap", StandardTraps.ChaosControl,
                StandardTraps.ChaosControl);

            Register("Inverted Mouse Trap", StandardTraps.InvertedMouse,
                StandardTraps.ReverseControls, StandardTraps.Reverse, StandardTraps.Reversal, StandardTraps.InvertedMouse);

            Register("Emergency Rescue Trap", StandardTraps.Tutorial,
                StandardTraps.Tutorial, StandardTraps.Home);

            Register("Explosion Trap", StandardTraps.Explosion,
                StandardTraps.Explosion, StandardTraps.EnemyBall);

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
            Action<string, bool> applyTrapEffectCallback)
        {
            _session = session;
            _isEnabled = enabled;
            _playerName = playerName;
            _enabledTraps = enabledTraps ?? new HashSet<string>();
            _applyTrapEffect = applyTrapEffectCallback;

            if (_isEnabled)
            {
                // Only subscribe to packets if we have a session (host only)
                if (_session != null)
                {
                    _session.Socket.PacketReceived += OnPacketReceived;
                    _log.LogInfo($"[PeakPelago] Trap Link service initialized with session for player: {_playerName}");
                }
                else
                {
                    _log.LogInfo($"[PeakPelago] Trap Link service enabled for client: {_playerName} (no session, effects only)");
                }

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
                if (!_peakToStandardMapping.TryGetValue(peakTrapName, out string standardizedTrapName))
                {
                    _log.LogDebug($"[PeakPelago] No outgoing TrapLink mapping for '{peakTrapName}', not sending");
                    return;
                }

                _log.LogDebug($"[PeakPelago] Mapped '{peakTrapName}' to '{standardizedTrapName}' for sending");

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
                        PeakArchipelagoPlugin._instance?.EnqueueMainThread(() =>
                            _notifications.ShowTrapLinkNotification($"TrapLink: received {externalTrapName} from {source}"));
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
        /// Update trap queue processing (always runs - queue works with or without TrapLink)
        /// </summary>
        public void Update()
        {
            // Wait for the current trap to finish before activating the next
            if (_trapInProgress)
            {
                // Safety timeout so the queue can never get permanently stuck
                if (Time.time - _activeTrapTime > TRAP_TIMEOUT)
                {
                    _log.LogWarning($"[PeakPelago] Trap '{_activeTrap}' timed out after {TRAP_TIMEOUT}s, forcing completion");
                    _trapInProgress = false;
                }
                else
                {
                    return;
                }
            }

            // Cooldown between trap activations
            if (Time.time - _lastTrapActivation < TRAP_ACTIVATION_COOLDOWN) return;

            if (_priorityTrap != null)
            {
                if (CanActivateTrap(_priorityTrap))
                {
                    ActivateTrap(_priorityTrap, fromTrapLink: true);
                    _lastTrapActivation = Time.time;
                    _priorityTrap = null;
                }
                // If not activatable yet, keep it as priority and wait
                return;
            }

            if (_trapQueue.Count > 0)
            {
                string trap = _trapQueue.First.Value;

                if (CanActivateTrap(trap))
                {
                    _trapQueue.RemoveFirst();
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
            if (Character.localCharacter.data.passedOutOnTheBeach > 0f) return false;
            if (LoadingScreenHandler.loading) return false;

            return true;
        }

        /// <summary>
        /// Set of trap names that use coroutines and will call NotifyTrapComplete() themselves.
        /// All other traps are considered instant and complete immediately.
        /// </summary>
        /// <summary>
        /// Traps that use coroutines and will call NotifyTrapComplete() when done.
        /// All other traps are instant and complete immediately.
        /// </summary>
        private static readonly HashSet<string> _coroutineTraps = new HashSet<string>
        {
            "Pokemon Trivia Trap",
            "Custom Trivia Trap",
            "Pokemon Count Trap",
            "Chaos Control Trap",
            "Inverted Mouse Trap",
            "Screen Flip Trap",
            "Zoom Trap",
            "Pixel Trap",
            "Blackout Trap",
            "Fear Trap",
            "Stamina Drain Trap",
            "Fungal Infection Trap",
        };

        private void ActivateTrap(string trapName, bool fromTrapLink)
        {
            try
            {
                _log.LogInfo($"[PeakPelago] Activating trap: '{trapName}' (fromTrapLink: {fromTrapLink})");
                _activeTrap = trapName;
                _activeTrapTime = Time.time;
                _trapInProgress = true;

                _applyTrapEffect?.Invoke(trapName, fromTrapLink);

                // Instant traps complete immediately
                if (!_coroutineTraps.Contains(trapName))
                {
                    _trapInProgress = false;
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to activate trap '{trapName}': {ex.Message}");
                _trapInProgress = false;
            }
        }

        /// <summary>
        /// Queue a trap for activation (works with or without TrapLink)
        /// </summary>
        public void QueueTrap(string trapName)
        {
            _trapQueue.AddLast(trapName);
            _log.LogInfo($"[PeakPelago] Queued trap: '{trapName}'");
        }

        /// <summary>
        /// Get the current trap queue state for UI display.
        /// Returns the active trap (or null) and up to maxVisible queued traps.
        /// </summary>
        public (string activeTrap, List<string> queued) GetQueueState(int maxVisible = 5)
        {
            string active = null;
            if (_activeTrap != null && Time.time - _activeTrapTime < ACTIVE_TRAP_DISPLAY_DURATION)
            {
                active = _activeTrap;
            }
            else
            {
                _activeTrap = null;
            }

            var queued = new List<string>();
            int count = 0;
            foreach (var trap in _trapQueue)
            {
                if (count >= maxVisible) break;
                queued.Add(trap);
                count++;
            }

            return (active, queued);
        }

        public int QueueCount => _trapQueue.Count;

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
