using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-side manager for the graybox dummy targets: spawns one damageable
    /// target at each post when the server starts, and respawns a post's target at
    /// full Health a fixed delay after it dies. Server-only per ADR-0005 (clients
    /// receive the spawns/despawns through FishNet); deliberately a plain
    /// MonoBehaviour wired to a NetworkManager, the same shape as RaiderCameraRig
    /// and SessionBootstrap, so it is not itself a spawned network object.
    /// </summary>
    public sealed class DummyTargetSpawner : MonoBehaviour
    {
        // ~3s respawn: long enough to read as a reset, short enough for fast iteration. Named PRD tuning.
        public const float RespawnSeconds = 3f;

        [SerializeField]
        private NetworkManager _networkManager;
        [SerializeField]
        private NetworkObject _targetPrefab;
        [SerializeField]
        private Transform[] _posts;

        private uint _respawnTicks;
        private bool _subscribedToServerState;
        private bool _serverBegun;
        // Post index -> server tick at which to respawn that post's target.
        private readonly Dictionary<int, uint> _pendingRespawns = new Dictionary<int, uint>();
        private readonly List<int> _dueBuffer = new List<int>();

        /// <summary>Single source of truth for respawn-delay-in-ticks; tests use the same conversion.</summary>
        public static uint RespawnTicks(TimeManager timeManager)
            => (uint)Mathf.CeilToInt(RespawnSeconds * timeManager.TickRate);

        /// <summary>
        /// Seam mirroring RaiderCameraRig.SetNetworkManager: the scene generator
        /// wires the session manager at authoring time; PlayMode tests inject a
        /// per-test manager (several coexist in-process). Re-attempts the server
        /// hook, so a manager whose server is already started begins immediately.
        /// </summary>
        public void SetNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            TryHookServer();
        }

        public void SetTargetPrefab(NetworkObject targetPrefab) => _targetPrefab = targetPrefab;

        public void SetPosts(Transform[] posts) => _posts = posts;

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

            // The server may already be running when a test wires us in after StartConnection.
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
            // Idempotent: OnServerConnectionState and the already-started path can both call in.
            if (_serverBegun)
                return;
            _serverBegun = true;

            _respawnTicks = RespawnTicks(_networkManager.TimeManager);
            _networkManager.TimeManager.OnTick += TimeManager_OnTick;

            for (int i = 0; i < _posts.Length; i++)
                SpawnTarget(i);
        }

        private void SpawnTarget(int postIndex)
        {
            Transform post = _posts[postIndex];
            NetworkObject nob = _networkManager.GetPooledInstantiated(_targetPrefab, post.position, post.rotation, true);

            /* Subscribe to THIS instance's death carrying its post index. No
             * unsubscribe is needed BECAUSE despawn destroys the object (the
             * default DespawnType.Destroy): the closure and the Health die with
             * it. If targets ever switch to pooled despawn, a reused Health would
             * accumulate handlers - unsubscribe in OnTargetDied then. */
            Health health = nob.GetComponent<Health>();
            health.Died += () => OnTargetDied(postIndex, nob);

            _networkManager.ServerManager.Spawn(nob);
        }

        private void OnTargetDied(int postIndex, NetworkObject target)
        {
            // Schedule the respawn first, then despawn: OnTick reads the schedule.
            _pendingRespawns[postIndex] = _networkManager.TimeManager.Tick + _respawnTicks;

            if (target != null && target.IsSpawned)
                _networkManager.ServerManager.Despawn(target);
        }

        private void TimeManager_OnTick()
        {
            if (_pendingRespawns.Count == 0)
                return;

            uint tick = _networkManager.TimeManager.Tick;

            /* Collect due posts before spawning: SpawnTarget does not touch
             * _pendingRespawns, but removing while enumerating the dictionary
             * would still throw. */
            _dueBuffer.Clear();
            foreach (KeyValuePair<int, uint> pending in _pendingRespawns)
            {
                if (tick >= pending.Value)
                    _dueBuffer.Add(pending.Key);
            }

            for (int i = 0; i < _dueBuffer.Count; i++)
            {
                int postIndex = _dueBuffer[i];
                _pendingRespawns.Remove(postIndex);
                SpawnTarget(postIndex);
            }
        }
    }
}
