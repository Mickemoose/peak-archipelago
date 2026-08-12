using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace Peak.AP
{
    public abstract class RingLinkServiceBase
    {
        protected readonly ManualLogSource _log;
        protected ArchipelagoSession _session;
        protected bool _isEnabled;
        protected int _connectionId;
        protected Harmony _harmony;
        protected ArchipelagoNotificationManager _notifications;

        protected abstract string Tag { get; }
        protected abstract string DisplayName { get; }
        protected abstract string HarmonyId { get; }
        protected abstract string SendRpcName { get; }

        protected abstract void ApplyHarmonyPatches();
        protected abstract void SetPatchInstance();
        protected abstract void ClearPatchInstance();
        protected abstract void ApplyRingLinkEffect(int amount);

        protected RingLinkServiceBase(ManualLogSource log, ArchipelagoNotificationManager notifications)
        {
            _log = log;
            _connectionId = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            _notifications = notifications;
        }

        protected static string FormatRingAmount(int amount)
        {
            return amount > 0 ? $"+{amount}" : $"{amount}";
        }

        public void Initialize(ArchipelagoSession session, bool enabled)
        {
            _session = session;
            _isEnabled = enabled;

            if (_isEnabled)
            {
                _harmony = new Harmony(HarmonyId);
                ApplyHarmonyPatches();
                SetPatchInstance();

                if (_session != null)
                {
                    _session.Socket.PacketReceived += OnPacketReceived;
                    _log.LogInfo($"[PeakPelago] {DisplayName} service initialized with session (Connection ID: {_connectionId})");
                }
                else
                {
                    _log.LogInfo($"[PeakPelago] {DisplayName} service enabled for client (no session, effects only)");
                }
            }
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            _log.LogInfo($"[PeakPelago] {DisplayName} {(enabled ? "enabled" : "disabled")}");
        }

        protected void SendRingLinkInternal(int amount)
        {
            if (!_isEnabled) return;

            if (_session == null)
            {
                try
                {
                    var plugin = PeakArchipelagoPlugin._instance;
                    if (plugin != null && plugin.PhotonView != null && Photon.Pun.PhotonNetwork.IsConnected)
                    {
                        plugin.PhotonView.RPC(SendRpcName, Photon.Pun.RpcTarget.MasterClient, amount);
                        string ringType = amount > 0 ? "positive" : "negative";
                        _log.LogInfo($"[PeakPelago] Client forwarded {DisplayName} to host: {amount} rings ({ringType})");
                        _notifications.ShowRingLinkNotification($"{Tag}: Sent {FormatRingAmount(amount)} ring(s)");
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError($"[PeakPelago] Failed to forward {DisplayName} to host: {ex.Message}");
                }
                return;
            }

            try
            {
                var ringLinkData = new Dictionary<string, JToken>
                {
                    { "time", JToken.FromObject(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) },
                    { "source", JToken.FromObject(_connectionId) },
                    { "amount", JToken.FromObject(amount) }
                };

                var bouncePacket = new BouncePacket
                {
                    Tags = new List<string> { Tag },
                    Data = ringLinkData
                };

                _session.Socket.SendPacket(bouncePacket);

                string ringType = amount > 0 ? "positive" : "negative";
                _log.LogInfo($"[PeakPelago] Sent {DisplayName}: {amount} rings ({ringType})");
                _notifications.ShowRingLinkNotification($"{Tag}: Sent {FormatRingAmount(amount)} ring(s)");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to send Ring Link: {ex.Message}");
            }
        }

        private void OnPacketReceived(ArchipelagoPacketBase packet)
        {
            try
            {
                if (packet is BouncePacket bounce)
                {
                    if (bounce.Tags != null && bounce.Tags.Contains(Tag) && _isEnabled)
                    {
                        var data = bounce.Data;
                        PeakArchipelagoPlugin._instance?.EnqueueMainThread(() => HandleReceived(data));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error handling Ring Link packet: {ex.Message}");
            }
        }

        private void HandleReceived(Dictionary<string, JToken> data)
        {
            try
            {
                string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (!currentScene.StartsWith("Level_"))
                {
                    return;
                }

                if (data.ContainsKey("source"))
                {
                    int source = data["source"].ToObject<int>();
                    if (source == _connectionId)
                    {
                        _log.LogDebug($"[PeakPelago] Ignoring own {DisplayName}");
                        return;
                    }
                }

                if (data.ContainsKey("amount"))
                {
                    int amount = data["amount"].ToObject<int>();
                    string ringType = amount > 0 ? "positive" : "negative";
                    _log.LogInfo($"[PeakPelago] {DisplayName} received: {amount} rings ({ringType})");
                    _notifications.ShowRingLinkNotification($"{Tag}: {FormatRingAmount(amount)} ring(s)!");
                    ApplyRingLinkEffect(amount);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Failed to handle {DisplayName}: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            if (_session != null)
            {
                _session.Socket.PacketReceived -= OnPacketReceived;
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            ClearPatchInstance();

            _session = null;
            _isEnabled = false;
        }
    }
}
