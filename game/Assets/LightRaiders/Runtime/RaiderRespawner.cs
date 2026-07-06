using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-side Raider death consequences: drops a loot bag where a Raider died
    /// and respawns that Raider at a spawn point after a fixed delay - crucially,
    /// OWNED by the same connection, so the player's input authority and per-client
    /// camera reattach to the fresh body. A plain MonoBehaviour wired to a
    /// NetworkManager (RaiderCameraRig mould); it does NOT spawn Raiders on connect
    /// (PlayerSpawner owns the initial spawn), only in response to death. Server-only
    /// per ADR-0005; clients receive the bag/despawn/respawn through FishNet.
    /// </summary>
    public sealed class RaiderRespawner : MonoBehaviour
    {
        // ~3s down before returning to play: a felt cost without stalling the session. Named PRD tuning.
        public const float RespawnSeconds = 3f;

        [SerializeField]
        private NetworkManager _networkManager;
        [SerializeField]
        private NetworkObject _raiderPrefab;
        [SerializeField]
        private NetworkObject _lootBagPrefab;
        [SerializeField]
        private Transform[] _spawns;

        private uint _respawnTicks;
        private bool _subscribedToServerState;
        private bool _serverBegun;
        private int _nextSpawn;
        private readonly List<PendingRespawn> _pending = new List<PendingRespawn>();

        private struct PendingRespawn
        {
            public NetworkConnection Owner;
            public uint Tick;
        }

        /// <summary>Single source of truth for respawn-delay-in-ticks; tests use the same conversion.</summary>
        public static uint RespawnTicks(TimeManager timeManager)
            => (uint)Mathf.CeilToInt(RespawnSeconds * timeManager.TickRate);

        /// <summary>Seam mirroring the other session spawners; re-hooks so an already-started server begins immediately.</summary>
        public void SetNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            TryHookServer();
        }

        public void SetRaiderPrefab(NetworkObject raiderPrefab) => _raiderPrefab = raiderPrefab;

        public void SetLootBagPrefab(NetworkObject lootBagPrefab) => _lootBagPrefab = lootBagPrefab;

        public void SetSpawns(Transform[] spawns) => _spawns = spawns;

        private void Start() => TryHookServer();

        private void OnDestroy()
        {
            if (_subscribedToServerState && _networkManager != null)
                _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;

            if (_serverBegun && _networkManager != null && _networkManager.TimeManager != null)
                _networkManager.TimeManager.OnTick -= TimeManager_OnTick;
        }

        private void TryHookServer()
        {
            if (_networkManager == null || !_networkManager.Initialized || _subscribedToServerState)
                return;

            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            _subscribedToServerState = true;

            if (_networkManager.ServerManager.Started)
                BeginServer();
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                BeginServer();
        }

        private void BeginServer()
        {
            if (_serverBegun)
                return;
            _serverBegun = true;

            _respawnTicks = RespawnTicks(_networkManager.TimeManager);
            _networkManager.TimeManager.OnTick += TimeManager_OnTick;
        }

        /// <summary>
        /// Called server-side by a dying Raider's RaiderDeathHandler. Drops a loot
        /// bag at the death position and schedules the owner's respawn. A null or
        /// disconnected owner still gets a bag but no respawn (no connection to give
        /// a fresh Raider to).
        /// </summary>
        public void HandleRaiderDeath(NetworkConnection owner, Vector3 deathPosition)
        {
            if (_networkManager == null || !_networkManager.ServerManager.Started)
                return;

            DropLootBag(deathPosition);

            if (owner != null && owner.IsActive)
                _pending.Add(new PendingRespawn { Owner = owner, Tick = _networkManager.TimeManager.Tick + _respawnTicks });
        }

        private void DropLootBag(Vector3 position)
        {
            if (_lootBagPrefab == null)
                return;

            NetworkObject nob = _networkManager.GetPooledInstantiated(_lootBagPrefab, position, Quaternion.identity, true);
            _networkManager.ServerManager.Spawn(nob);
        }

        private void TimeManager_OnTick()
        {
            if (_pending.Count == 0)
                return;

            uint tick = _networkManager.TimeManager.Tick;
            // Iterate backwards so due entries can be removed in place.
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (tick < _pending[i].Tick)
                    continue;
                RespawnRaider(_pending[i].Owner);
                _pending.RemoveAt(i);
            }
        }

        private void RespawnRaider(NetworkConnection owner)
        {
            // The owner may have disconnected during the down time; drop the respawn if so.
            if (owner == null || !owner.IsActive)
                return;

            Transform spawn = NextSpawn();
            Vector3 position = spawn != null ? spawn.position : Vector3.zero;
            Quaternion rotation = spawn != null ? spawn.rotation : Quaternion.identity;

            NetworkObject nob = _networkManager.GetPooledInstantiated(_raiderPrefab, position, rotation, true);
            /* Spawn OWNED by the original connection: FishNet gives that client input
             * authority, and its RaiderCameraRig re-acquires the owned Raider on the
             * next frame - camera and control survive death with no extra wiring. */
            _networkManager.ServerManager.Spawn(nob, owner);
        }

        private Transform NextSpawn()
        {
            if (_spawns == null || _spawns.Length == 0)
                return null;

            Transform spawn = _spawns[_nextSpawn];
            _nextSpawn = (_nextSpawn + 1) % _spawns.Length;
            return spawn;
        }
    }
}
