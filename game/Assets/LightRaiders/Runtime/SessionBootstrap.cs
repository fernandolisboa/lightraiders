using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Starts the networked session automatically: the main editor instance (and builds)
    /// hosts server+client, while MPPM virtual-player clones connect as client only.
    /// Lives next to the NetworkManager in the generated arena scene.
    /// </summary>
    public sealed class SessionBootstrap : MonoBehaviour
    {
        private NetworkManager _networkManager;
        private bool _subscribedToServerState;

        /// <summary>
        /// True when running inside an MPPM virtual-player clone. Mirrors FishNet
        /// CloneChecker's MPPM detection (clone projects live under Library/VP/).
        /// Builds always take the host-capable path.
        /// </summary>
        private static bool IsMppmVirtualPlayer
        {
            get
            {
#if UNITY_EDITOR
                return Application.dataPath.ToLowerInvariant().Contains("library/vp/");
#else
                return false;
#endif
            }
        }

        private void Start()
        {
            _networkManager = GetComponent<NetworkManager>();

            // Null-guard keeps the console clean; NetworkManager logs its own init errors.
            if (_networkManager == null || !_networkManager.Initialized)
                return;

            if (IsMppmVirtualPlayer)
            {
                _networkManager.ClientManager.StartConnection();
                return;
            }

            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            _subscribedToServerState = true;
            _networkManager.ServerManager.StartConnection();
        }

        private void OnDestroy()
        {
            if (_subscribedToServerState && _networkManager != null)
            {
                _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
                _subscribedToServerState = false;
            }
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                _networkManager.ClientManager.StartConnection();
        }
    }
}
