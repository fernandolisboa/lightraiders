using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-side manager for the graybox hostile emitters: spawns one emitter at
    /// each post when the server starts, and never respawns them - an emitter is
    /// stationary scaffolding, not a target that resets (contrast
    /// DummyTargetSpawner, which schedules respawns and therefore handles ticks and
    /// deaths). Server-only per ADR-0005 (clients receive the spawns through
    /// FishNet); a plain MonoBehaviour wired to a NetworkManager, the same shape as
    /// DummyTargetSpawner and SessionBootstrap, so it is not itself a spawned
    /// network object.
    /// </summary>
    public sealed class HostileEmitterSpawner : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager _networkManager;
        [SerializeField]
        private NetworkObject _emitterPrefab;
        [SerializeField]
        private Transform[] _posts;

        private bool _subscribedToServerState;
        private bool _serverBegun;

        /// <summary>
        /// Seam mirroring DummyTargetSpawner.SetNetworkManager: the scene generator
        /// wires the session manager at authoring time; PlayMode tests inject a
        /// per-test manager. Re-attempts the server hook, so a manager whose server
        /// is already started spawns immediately.
        /// </summary>
        public void SetNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            TryHookServer();
        }

        public void SetEmitterPrefab(NetworkObject emitterPrefab) => _emitterPrefab = emitterPrefab;

        public void SetPosts(Transform[] posts) => _posts = posts;

        private void Start() => TryHookServer();

        private void OnDestroy()
        {
            /* No OnTick subscription to release (there is no respawn to tick),
             * unlike DummyTargetSpawner - only the server-state hook. */
            if (_subscribedToServerState && _networkManager != null)
                _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
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

            for (int i = 0; i < _posts.Length; i++)
                SpawnEmitter(i);
        }

        private void SpawnEmitter(int postIndex)
        {
            Transform post = _posts[postIndex];
            NetworkObject nob = _networkManager.GetPooledInstantiated(_emitterPrefab, post.position, post.rotation, true);
            _networkManager.ServerManager.Spawn(nob);
        }
    }
}
