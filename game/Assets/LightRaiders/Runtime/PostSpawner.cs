using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-side spawner for static networked map fixtures (Exfil sites #38,
    /// Hatches #39): one instance of a prefab per post when the server starts,
    /// each stamped with its post's Layer before spawn (ADR-0008). Same shape as
    /// <see cref="DummyTargetSpawner"/>; one component per fixture type.
    /// </summary>
    public sealed class PostSpawner : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager _networkManager;
        [SerializeField]
        private NetworkObject _prefab;
        [SerializeField]
        private Transform[] _posts = new Transform[0];
        [SerializeField]
        private int[] _postLayers = new int[0];

        private bool _subscribedToServerState;
        private bool _serverBegun;

        public void SetNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            TryHookServer();
        }

        public void SetPrefab(NetworkObject prefab) => _prefab = prefab;

        public void SetPosts(Transform[] posts, int[] layers)
        {
            _posts = posts;
            _postLayers = layers;
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

            if (_prefab == null || _posts == null)
            {
                Debug.LogError("PostSpawner needs a prefab and posts.", this);
                return;
            }

            for (int i = 0; i < _posts.Length; i++)
            {
                Transform post = _posts[i];
                if (post == null)
                    continue;
                NetworkObject nob = _networkManager.GetPooledInstantiated(_prefab, post.position, post.rotation, true);
                int layer = _postLayers != null && i < _postLayers.Length ? _postLayers[i] : LayerMember.Surface;
                LayerMember.StampBeforeSpawn(nob, layer);
                _networkManager.ServerManager.Spawn(nob);
            }
        }
    }
}
