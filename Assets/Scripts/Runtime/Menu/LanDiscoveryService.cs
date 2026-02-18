using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Risiko3D.Runtime.Menu
{
    public sealed class LanDiscoveryService : MonoBehaviour
    {
        private const int DiscoveryPort = 47777;
        private const string DiscoveryMagic = "RISIKO3D_DISCOVERY_V1";
        private const float BroadcastInterval = 1.0f;
        private const float EntryTtlSeconds = 4.0f;

        private readonly Dictionary<string, LanLobbyAnnouncement> _announcements = new();

        private UdpClient _sender;
        private UdpClient _receiver;
        private bool _broadcasting;
        private bool _discovering;
        private float _nextBroadcastAt;
        private string _broadcastRoomCode = string.Empty;
        private ulong _broadcastLobbyId;
        private string _broadcastHostName = string.Empty;

        public IReadOnlyList<LanLobbyAnnouncement> GetAnnouncements()
        {
            PurgeExpired();
            return new List<LanLobbyAnnouncement>(_announcements.Values);
        }

        public void StartHostBroadcast(string roomCode, ulong lobbyId, string hostName)
        {
            _broadcastRoomCode = roomCode ?? string.Empty;
            _broadcastLobbyId = lobbyId;
            _broadcastHostName = string.IsNullOrWhiteSpace(hostName) ? Environment.MachineName : hostName;
            if (string.IsNullOrWhiteSpace(_broadcastRoomCode))
            {
                _broadcasting = false;
                return;
            }

            EnsureSender();
            _broadcasting = _sender != null;
            _nextBroadcastAt = 0f;
        }

        public void StopHostBroadcast()
        {
            _broadcasting = false;
        }

        public void StartDiscovery()
        {
            EnsureReceiver();
            _discovering = _receiver != null;
        }

        public void StopDiscovery()
        {
            _discovering = false;
        }

        private void Update()
        {
            if (_broadcasting && Time.unscaledTime >= _nextBroadcastAt)
            {
                BroadcastOnce();
                _nextBroadcastAt = Time.unscaledTime + BroadcastInterval;
            }

            if (_discovering)
            {
                ReceivePending();
                PurgeExpired();
            }
        }

        private void EnsureSender()
        {
            if (_sender != null)
            {
                return;
            }

            try
            {
                _sender = new UdpClient();
                _sender.EnableBroadcast = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Risiko3D][LAN] Sender init failed: {ex.Message}");
                _sender = null;
            }
        }

        private void EnsureReceiver()
        {
            if (_receiver != null)
            {
                return;
            }

            try
            {
                _receiver = new UdpClient(DiscoveryPort);
                _receiver.Client.Blocking = false;
                _receiver.EnableBroadcast = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Risiko3D][LAN] Receiver init failed: {ex.Message}");
                _receiver = null;
            }
        }

        private void BroadcastOnce()
        {
            if (_sender == null || string.IsNullOrWhiteSpace(_broadcastRoomCode))
            {
                return;
            }

            var payload = $"{DiscoveryMagic}|{_broadcastRoomCode}|{_broadcastLobbyId}|{_broadcastHostName}";
            var bytes = Encoding.UTF8.GetBytes(payload);
            try
            {
                _sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
            }
            catch
            {
                // Keep service alive; next tick may recover.
            }
        }

        private void ReceivePending()
        {
            if (_receiver == null)
            {
                return;
            }

            try
            {
                while (_receiver.Available > 0)
                {
                    var endpoint = new IPEndPoint(IPAddress.Any, 0);
                    var bytes = _receiver.Receive(ref endpoint);
                    var payload = Encoding.UTF8.GetString(bytes);
                    ParseAndStore(payload, endpoint);
                }
            }
            catch
            {
                // Non-fatal.
            }
        }

        private void ParseAndStore(string payload, IPEndPoint endpoint)
        {
            if (string.IsNullOrWhiteSpace(payload) || endpoint == null)
            {
                return;
            }

            var parts = payload.Split('|');
            if (parts.Length < 4 || parts[0] != DiscoveryMagic)
            {
                return;
            }

            var roomCode = parts[1];
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                return;
            }

            _ = ulong.TryParse(parts[2], out var lobbyId);
            var hostName = parts[3];
            var key = $"{endpoint.Address}|{roomCode}";
            _announcements[key] = new LanLobbyAnnouncement(
                hostName,
                endpoint.Address.ToString(),
                roomCode,
                lobbyId,
                Time.unscaledTime);
        }

        private void PurgeExpired()
        {
            if (_announcements.Count == 0)
            {
                return;
            }

            var threshold = Time.unscaledTime - EntryTtlSeconds;
            var dead = new List<string>();
            foreach (var kv in _announcements)
            {
                if (kv.Value.LastSeenAt < threshold)
                {
                    dead.Add(kv.Key);
                }
            }

            foreach (var k in dead)
            {
                _announcements.Remove(k);
            }
        }

        private void OnDestroy()
        {
            try { _sender?.Dispose(); } catch { }
            try { _receiver?.Dispose(); } catch { }
            _sender = null;
            _receiver = null;
            _announcements.Clear();
        }
    }

    public readonly struct LanLobbyAnnouncement
    {
        public string HostName { get; }
        public string Address { get; }
        public string RoomCode { get; }
        public ulong LobbyId { get; }
        public float LastSeenAt { get; }

        public LanLobbyAnnouncement(string hostName, string address, string roomCode, ulong lobbyId, float lastSeenAt)
        {
            HostName = hostName ?? string.Empty;
            Address = address ?? string.Empty;
            RoomCode = roomCode ?? string.Empty;
            LobbyId = lobbyId;
            LastSeenAt = lastSeenAt;
        }
    }
}
