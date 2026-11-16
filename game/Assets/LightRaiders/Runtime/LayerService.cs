using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-side Layer bookkeeping for one NetworkManager (ADR-0008): which
    /// Layer each connection is in, and the single entry point for moving a
    /// Raider between Layers. Lives on the NetworkManager GameObject (beside
    /// RaiderRespawner) so <see cref="LayerCondition"/> finds it from any
    /// connection. Every Layer change rebuilds observers so clients spawn what
    /// entered their Layer and despawn what left it.
    /// </summary>
    public sealed class LayerService : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager _networkManager;

        private readonly Dictionary<NetworkConnection, int> _connectionLayers = new Dictionary<NetworkConnection, int>();
        private bool _subscribed;

        public void SetNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            TrySubscribe();
        }

        private void Start() => TrySubscribe();

        private void OnDestroy()
        {
            if (_subscribed && _networkManager != null)
                _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        private void TrySubscribe()
        {
            if (_subscribed || _networkManager == null || !_networkManager.Initialized)
                return;
            _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            _subscribed = true;
        }

        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
                _connectionLayers.Remove(connection);
        }

        /// <summary>The Layer a connection currently views. Unknown connections are on the Surface.</summary>
        public int GetConnectionLayer(NetworkConnection connection)
        {
            if (connection != null && _connectionLayers.TryGetValue(connection, out int layer))
                return layer;
            return LayerMember.Surface;
        }

        /// <summary>
        /// Moves a Raider (and the connection that owns it) into a Layer, server
        /// side, and rebuilds observers for both. The Raider's own owner always
        /// stays an observer because the two Layers change together.
        /// </summary>
        public void ServerMoveRaider(NetworkObject raider, int layer)
        {
            if (raider == null || _networkManager == null || !_networkManager.IsServerStarted)
                return;

            LayerMember member = raider.GetComponent<LayerMember>();
            if (member != null)
                member.ServerSetLayer(layer);

            NetworkConnection owner = raider.Owner;
            if (owner != null && owner.IsValid)
                _connectionLayers[owner] = layer;

            RebuildFor(raider, owner);
        }

        /// <summary>
        /// Sets which Layer a connection views without moving any object - used
        /// when a connection has no Raider (respawn delay, after extraction).
        /// </summary>
        public void ServerSetConnectionLayer(NetworkConnection connection, int layer)
        {
            if (connection == null || !connection.IsValid || _networkManager == null || !_networkManager.IsServerStarted)
                return;
            _connectionLayers[connection] = layer;
            _networkManager.ServerManager.Objects.RebuildObservers(connection);
        }

        private void RebuildFor(NetworkObject raider, NetworkConnection owner)
        {
            // The moved object: who may see it now.
            _networkManager.ServerManager.Objects.RebuildObservers(raider);
            // The owner: what it may see now.
            if (owner != null && owner.IsValid)
                _networkManager.ServerManager.Objects.RebuildObservers(owner);
        }
    }
}
