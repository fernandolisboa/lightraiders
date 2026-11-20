using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-side spawner for one Waygate pair: when the server starts it spawns
    /// a gate at each of its two posts, stamps each gate's Layer before spawn
    /// (ADR-0008) and pairs them. Same shape as <see cref="DummyTargetSpawner"/>:
    /// a plain MonoBehaviour beside the session NetworkManager.
    /// </summary>
    public sealed class WaygateSpawner : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager _networkManager;
        [SerializeField]
        private NetworkObject _waygatePrefab;
        [SerializeField]
        private Transform[] _posts = new Transform[2];
        [SerializeField]
        private int[] _postLayers = new int[2];

        private bool _subscribedToServerState;
        private bool _serverBegun;

        public void SetNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            TryHookServer();
        }

        public void SetWaygatePrefab(NetworkObject prefab) => _waygatePrefab = prefab;

        /// <summary>The pair's two posts and the Layer each one stands in.</summary>
        public void SetPosts(Transform postA, int layerA, Transform postB, int layerB)
        {
            _posts = new[] { postA, postB };
            _postLayers = new[] { layerA, layerB };
        }

        private void Start() => TryHookServer();

        private void OnDestroy()
        {
            if (_subscribedToServerState && _networkManager != null)
                _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
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
            else if (args.ConnectionState == LocalConnectionState.Stopped)
                _serverBegun = false;
        }

        private void BeginServer()
        {
            if (_serverBegun)
                return;
            _serverBegun = true;

            if (_waygatePrefab == null || _posts == null || _posts.Length != 2 || _posts[0] == null || _posts[1] == null)
            {
                Debug.LogError("WaygateSpawner needs a prefab and exactly two posts.", this);
                return;
            }

            Waygate a = Spawn(0);
            Waygate b = Spawn(1);
            Waygate.ServerPair(a, b);
        }

        private Waygate Spawn(int index)
        {
            Transform post = _posts[index];
            NetworkObject nob = _networkManager.GetPooledInstantiated(_waygatePrefab, post.position, post.rotation, true);
            LayerMember.StampBeforeSpawn(nob, _postLayers[index]);
            _networkManager.ServerManager.Spawn(nob);
            return nob.GetComponent<Waygate>();
        }
    }
}
