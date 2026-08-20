using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using BepInEx.Logging;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Peak.AP
{
    public class KnockbackLinkService
    {
        private readonly ManualLogSource _log;
        private readonly ArchipelagoNotificationManager _notifications;
        private ArchipelagoSession _session;
        private bool _isEnabled;
        private string _playerName;
        private string _localUuid;
        private double _lastSentTime;
        private float _lastSendRealtime;

        private const string TAG = "KnockbackLink";
        private const float MAX_INCOMING_MAGNITUDE = 25f;
        private const float SEND_COOLDOWN_SECONDS = 2f;

        public KnockbackLinkService(ManualLogSource log, ArchipelagoNotificationManager notifications)
        {
            _log = log;
            _notifications = notifications;
            _localUuid = SystemInfo.deviceUniqueIdentifier;
        }

        public bool IsEnabled() => _isEnabled;

        public void Initialize(ArchipelagoSession session, bool enabled, string playerName)
        {
            if (_session != null)
            {
                _session.Socket.PacketReceived -= OnPacketReceived;
            }

            _session = session;
            _isEnabled = enabled;
            _playerName = playerName;

            if (_isEnabled && _session != null)
            {
                _session.Socket.PacketReceived += OnPacketReceived;
                _log.LogInfo("[PeakPelago] Knockback Link service initialized");
            }
        }

        private void OnPacketReceived(ArchipelagoPacketBase packet)
        {
            try
            {
                if (!_isEnabled) return;
                if (packet is BouncePacket bounce && bounce.Tags != null && bounce.Tags.Contains(TAG))
                {
                    HandleKnockbackLinkReceived(bounce.Data);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error handling Knockback Link packet: {ex.Message}");
            }
        }

        private void HandleKnockbackLinkReceived(Dictionary<string, JToken> data)
        {
            if (data == null) return;

            if (data.ContainsKey("uuid"))
            {
                string uuid = data["uuid"]?.ToObject<string>();
                if (!string.IsNullOrEmpty(uuid) && uuid == _localUuid)
                {
                    _log.LogDebug("[PeakPelago] Ignoring own Knockback Link (uuid)");
                    return;
                }
            }

            string source = data.ContainsKey("source") ? data["source"].ToObject<string>() : "Unknown";
            if (source == _playerName)
            {
                _log.LogDebug("[PeakPelago] Ignoring own Knockback Link (source)");
                return;
            }

            if (data.ContainsKey("time"))
            {
                double time = data["time"].ToObject<double>();
                if (System.Math.Abs(time - _lastSentTime) < 0.0001) return;
            }

            Vector3 force = Vector3.zero;
            if (data.ContainsKey("value") && data["value"] is JObject value)
            {
                force = new Vector3(
                    value["x"]?.ToObject<float>() ?? 0f,
                    value["y"]?.ToObject<float>() ?? 0f,
                    value["z"]?.ToObject<float>() ?? 0f
                );
            }

            if (force.sqrMagnitude <= 0.001f) return;
            force = Vector3.ClampMagnitude(force, MAX_INCOMING_MAGNITUDE);

            string cause = data.ContainsKey("cause") ? data["cause"].ToObject<string>() : null;
            _log.LogInfo($"[PeakPelago] Knockback Link received: {force.magnitude:F1} force from {source}");

            Vector3 finalForce = force;
            PeakArchipelagoPlugin._instance?.EnqueueMainThread(() =>
            {
                string message = string.IsNullOrEmpty(cause)
                    ? $"KnockbackLink: knockback from {source}"
                    : $"KnockbackLink: {cause}";
                _notifications?.ShowTrapLinkNotification(message);
                PeakArchipelagoPlugin._instance.BroadcastKnockbackLink(finalForce, source);
            });
        }

        public void ReportLocalKnockback(Vector3 force, string characterName)
        {
            if (!_isEnabled || _session == null) return;
            if (Time.realtimeSinceStartup - _lastSendRealtime < SEND_COOLDOWN_SECONDS) return;
            _lastSendRealtime = Time.realtimeSinceStartup;

            try
            {
                _lastSentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                string cause = string.IsNullOrEmpty(characterName)
                    ? $"{_playerName} got blasted"
                    : $"{characterName} ({_playerName}) got blasted";

                var data = new Dictionary<string, JToken>
                {
                    { "time", JToken.FromObject(_lastSentTime) },
                    { "uuid", JToken.FromObject(_localUuid) },
                    { "source", JToken.FromObject(_playerName) },
                    { "cause", JToken.FromObject(cause) },
                    { "value", JToken.FromObject(new Dictionary<string, float>
                        {
                            { "x", force.x },
                            { "y", force.y },
                            { "z", force.z }
                        })
                    }
                };

                var bouncePacket = new BouncePacket
                {
                    Tags = new List<string> { TAG },
                    Data = data
                };

                _session.Socket.SendPacket(bouncePacket);
                _log.LogInfo($"[PeakPelago] Sent Knockback Link: {force.magnitude:F1} force");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to send Knockback Link: {ex.Message}");
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
