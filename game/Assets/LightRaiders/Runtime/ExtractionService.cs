using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>How a Raider got out (for the client's overlay and later stats).</summary>
    public enum ExtractionRoute : byte
    {
        Exfil = 0,
        Hatch = 1,
    }

    /// <summary>Server → owner: your Raider extracted and was despawned.</summary>
    public struct ExtractedBroadcast : IBroadcast
    {
        public ExtractionRoute Route;
    }

    /// <summary>Client → server (debug, Phase 2): respawn my extracted Raider.</summary>
    public struct RejoinRequestBroadcast : IBroadcast
    {
        public byte Unused;
    }

    /// <summary>
    /// Server-side extraction path shared by Exfil (#38) and Hatches (#39):
    /// despawns the Raider everywhere, tells its owner with a broadcast, and
    /// remembers the connection so a debug rejoin request can bring it back at a
    /// Surface spawn point (PRD #31 Q4). Real raid-end flow arrives in Phase 5.
    /// Lives beside the NetworkManager, like <see cref="LayerService"/>.
    /// </summary>
    public sealed class ExtractionService : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager _networkManager;

        private readonly HashSet<NetworkConnection> _extracted = new HashSet<NetworkConnection>();
        private bool _hooked;
        private bool _broadcastRegistered;

        public void SetNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            TryHook();
        }

        private void Start() => TryHook();

        private void TryHook()
        {
            if (_hooked || _networkManager == null || !_networkManager.Initialized)
                return;
            _hooked = true;
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            if (_networkManager.ServerManager.Started)
                RegisterBroadcast();
        }

        private void OnDestroy()
        {
            if (!_hooked || _networkManager == null)
                return;
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            if (_broadcastRegistered)
                _networkManager.ServerManager.UnregisterBroadcast<RejoinRequestBroadcast>(OnRejoinRequest);
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                RegisterBroadcast();
            else if (args.ConnectionState == LocalConnectionState.Stopped)
                _extracted.Clear();
        }

        private void RegisterBroadcast()
        {
            if (_broadcastRegistered)
                return;
            _broadcastRegistered = true;
            _networkManager.ServerManager.RegisterBroadcast<RejoinRequestBroadcast>(OnRejoinRequest);
        }

        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
                _extracted.Remove(connection);
        }

        public bool IsExtracted(NetworkConnection connection) => connection != null && _extracted.Contains(connection);

        /// <summary>
        /// Server: extracts one Raider. Despawns it (every observer drops it) and,
        /// if it had a live owner, sends that owner the Extracted notice.
        /// </summary>
        public void ServerExtract(NetworkObject raider, ExtractionRoute route)
        {
            if (_networkManager == null || raider == null || !raider.IsSpawned)
                return;

            NetworkConnection owner = raider.Owner;
            _networkManager.ServerManager.Despawn(raider);

            if (owner != null && owner.IsActive)
            {
                _extracted.Add(owner);
                _networkManager.ServerManager.Broadcast(owner, new ExtractedBroadcast { Route = route });
            }
        }

        private void OnRejoinRequest(NetworkConnection connection, RejoinRequestBroadcast message, Channel channel)
        {
            // Only an extracted connection may rejoin; anything else is ignored.
            if (!_extracted.Remove(connection))
                return;

            RaiderRespawner respawner = _networkManager.GetComponent<RaiderRespawner>();
            if (respawner != null)
                respawner.RejoinRaider(connection);
        }

        /// <summary>The service beside a NetworkManager, or null.</summary>
        public static ExtractionService Of(NetworkManager networkManager)
            => networkManager != null ? networkManager.GetComponent<ExtractionService>() : null;
    }
}
