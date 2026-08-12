using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using BepInEx.Logging;
using Newtonsoft.Json.Linq;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public class BreathLinkService
    {
        private readonly ManualLogSource _log;
        private ArchipelagoSession _session;
        private bool _isEnabled;
        private int _connectionId;
        private ArchipelagoNotificationManager _notifications;
        private DateTime _lastBreathSent = DateTime.MinValue;
        private DateTime _lastBreathReceived = DateTime.MinValue;
        private bool _isDepleting = false;

        private const float COOLDOWN_SECONDS = 10f;

        public BreathLinkService(ManualLogSource log, ArchipelagoNotificationManager notifications)
        {
            _log = log;
            _connectionId = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            _notifications = notifications;
        }

        public void Initialize(ArchipelagoSession session, bool enabled)
        {
            _session = session;
            _isEnabled = enabled;

            if (_isEnabled && _session != null)
            {
                _session.Socket.PacketReceived += OnPacketReceived;
                _log.LogInfo($"[PeakPelago] Breath Link service initialized (Connection ID: {_connectionId})");
            }
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            _log.LogInfo($"[PeakPelago] Breath Link {(enabled ? "enabled" : "disabled")}");
        }

        public bool IsEnabled() => _isEnabled;
        public bool IsDepleting() => _isDepleting;
        public void SetDepleting(bool value) => _isDepleting = value;

        public void SendBreathLink(string source)
        {
            if (_session == null || !_isEnabled) return;

            try
            {
                if (DateTime.Now - _lastBreathSent < TimeSpan.FromSeconds(COOLDOWN_SECONDS))
                {
                    _log.LogDebug("[PeakPelago] Breath Link throttled - too recent");
                    return;
                }

                if (_isDepleting)
                {
                    _log.LogDebug("[PeakPelago] Stamina depletion was caused by BreathLink, not sending another");
                    return;
                }

                _lastBreathSent = DateTime.Now;

                var data = new Dictionary<string, JToken>
                {
                    { "time", JToken.FromObject(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) },
                    { "source", JToken.FromObject(_connectionId) },
                    { "cause", JToken.FromObject(source) }
                };

                var bouncePacket = new BouncePacket
                {
                    Tags = new List<string> { "BreathLink" },
                    Data = data
                };

                _session.Socket.SendPacket(bouncePacket);
                _log.LogInfo($"[PeakPelago] Breath Link sent: {source}");
                _notifications.ShowDeathLink("BreathLink", source);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to send Breath Link: {ex.Message}");
            }
        }

        private void OnPacketReceived(ArchipelagoPacketBase packet)
        {
            try
            {
                if (packet is BouncePacket bounce)
                {
                    if (bounce.Tags != null && bounce.Tags.Contains("BreathLink") && _isEnabled)
                    {
                        var data = bounce.Data;
                        PeakArchipelagoPlugin._instance?.EnqueueMainThread(() => HandleBreathLinkReceived(data));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error handling Breath Link packet: {ex.Message}");
            }
        }

        private void HandleBreathLinkReceived(Dictionary<string, JToken> data)
        {
            try
            {
                string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (!currentScene.StartsWith("Level_")) return;

                // Don't process our own Breath Links
                if (data.ContainsKey("source"))
                {
                    int source = data["source"].ToObject<int>();
                    if (source == _connectionId)
                    {
                        _log.LogDebug("[PeakPelago] Ignoring own Breath Link");
                        return;
                    }
                }

                if (DateTime.Now - _lastBreathReceived < TimeSpan.FromSeconds(COOLDOWN_SECONDS))
                {
                    _log.LogDebug("[PeakPelago] Breath Link receive throttled - too recent");
                    return;
                }

                _lastBreathReceived = DateTime.Now;

                string cause = data.ContainsKey("cause") ? data["cause"].ToObject<string>() : "Unknown";
                _log.LogInfo($"[PeakPelago] Breath Link received from: {cause}");

                _notifications.ShowDeathLink("BreathLink: Out of breath!", cause);

                // Deplete stamina for all players via RPC
                DepleteAllPlayersStamina();
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error handling Breath Link: {ex.Message}");
            }
        }

        private void DepleteAllPlayersStamina()
        {
            _isDepleting = true;

            try
            {
                if (PeakArchipelagoPlugin._instance?.PhotonView != null && PhotonNetwork.IsConnected)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("BreathLinkDepleteStaminaRPC", RpcTarget.All);
                }
                else
                {
                    DepleteLocalStamina();
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error depleting stamina: {ex.Message}");
                _isDepleting = false;
            }
        }

        public void DepleteLocalStamina()
        {
            try
            {
                foreach (var character in Character.AllCharacters)
                {
                    if (character != null && !character.data.dead && !character.data.fullyPassedOut)
                    {
                        character.data.currentStamina = 0f;
                        character.data.extraStamina = 0f;
                        _log.LogInfo($"[PeakPelago] Breath Link: Depleted stamina for {character.characterName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error depleting local stamina: {ex.Message}");
            }
            finally
            {
                _isDepleting = false;
            }
        }

        public void Cleanup()
        {
            if (_session != null)
            {
                _session.Socket.PacketReceived -= OnPacketReceived;
            }
        }
    }
}