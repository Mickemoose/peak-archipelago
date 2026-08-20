using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using BepInEx.Logging;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Peak.AP
{
    public class DamageLinkService
    {
        private readonly ManualLogSource _log;
        private readonly ArchipelagoNotificationManager _notifications;
        private ArchipelagoSession _session;
        private bool _isEnabled;
        private string _group = "";
        private string _playerName;
        private double _lastSentTime;
        private float _pendingSendPoints;

        private const float POINTS_PER_FULL_BAR = 100f;
        private const int MAX_INCOMING_POINTS = 50;

        public DamageLinkService(ManualLogSource log, ArchipelagoNotificationManager notifications)
        {
            _log = log;
            _notifications = notifications;
        }

        public string Tag => "SharedDamage" + _group;
        public bool IsEnabled() => _isEnabled;

        public void Initialize(ArchipelagoSession session, bool enabled, string group, string playerName)
        {
            if (_session != null)
            {
                _session.Socket.PacketReceived -= OnPacketReceived;
            }

            _session = session;
            _isEnabled = enabled;
            _group = group ?? "";
            _playerName = playerName;

            if (_isEnabled && _session != null)
            {
                _session.Socket.PacketReceived += OnPacketReceived;
                _log.LogInfo($"[PeakPelago] Damage Link service initialized (tag: {Tag})");
            }
        }

        private void OnPacketReceived(ArchipelagoPacketBase packet)
        {
            try
            {
                if (!_isEnabled) return;
                if (packet is BouncePacket bounce && bounce.Tags != null && bounce.Tags.Contains(Tag))
                {
                    HandleDamageLinkReceived(bounce.Data);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error handling Damage Link packet: {ex.Message}");
            }
        }

        private void HandleDamageLinkReceived(Dictionary<string, JToken> data)
        {
            if (data == null) return;

            string source = data.ContainsKey("source") ? data["source"].ToObject<string>() : "Unknown";
            if (source == _playerName)
            {
                _log.LogDebug("[PeakPelago] Ignoring own Damage Link");
                return;
            }

            if (data.ContainsKey("time"))
            {
                double time = data["time"].ToObject<double>();
                if (System.Math.Abs(time - _lastSentTime) < 0.0001) return;
            }

            int points = data.ContainsKey("damage_points") ? data["damage_points"].ToObject<int>() : 0;
            if (points <= 0) return;
            if (points > MAX_INCOMING_POINTS) points = MAX_INCOMING_POINTS;

            float amount = points / POINTS_PER_FULL_BAR;
            string cause = data.ContainsKey("cause") ? data["cause"].ToObject<string>() : null;
            _log.LogInfo($"[PeakPelago] Damage Link received: {points} points from {source}");

            PeakArchipelagoPlugin._instance?.EnqueueMainThread(() =>
            {
                string message = string.IsNullOrEmpty(cause)
                    ? $"DamageLink: {points} damage from {source}"
                    : $"DamageLink: {cause}";
                _notifications?.ShowTrapLinkNotification(message);
                PeakArchipelagoPlugin._instance.BroadcastDamageLinkDamage(amount, source);
            });
        }

        public void ReportLocalDamage(float amount, string characterName = null)
        {
            if (!_isEnabled || _session == null) return;
            if (amount <= 0f) return;

            _pendingSendPoints += amount * POINTS_PER_FULL_BAR;
            int points = (int)_pendingSendPoints;
            if (points < 1) return;
            _pendingSendPoints -= points;

            SendDamage(points, characterName);
        }

        private void SendDamage(int points, string characterName)
        {
            try
            {
                _lastSentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                string cause = string.IsNullOrEmpty(characterName)
                    ? $"{_playerName} took a hit"
                    : $"{characterName} ({_playerName}) took a hit";

                var data = new Dictionary<string, JToken>
                {
                    { "time", JToken.FromObject(_lastSentTime) },
                    { "uuid", JToken.FromObject(SystemInfo.deviceUniqueIdentifier) },
                    { "source", JToken.FromObject(_playerName) },
                    { "damage_points", JToken.FromObject(points) },
                    { "cause", JToken.FromObject(cause) }
                };

                var bouncePacket = new BouncePacket
                {
                    Tags = new List<string> { Tag },
                    Data = data
                };

                _session.Socket.SendPacket(bouncePacket);
                _log.LogInfo($"[PeakPelago] Sent Damage Link: {points} points");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to send Damage Link: {ex.Message}");
            }
        }

        public void Shutdown()
        {
            if (_session != null)
            {
                _session.Socket.PacketReceived -= OnPacketReceived;
            }
            _session = null;
            _isEnabled = false;
        }
    }
}
