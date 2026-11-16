using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LightRaiders
{
    /// <summary>
    /// Client side of extraction (#38, PRD #31 Q4): on the server's Extracted
    /// notice it shows a full-screen "Extracted" overlay, and F8 (debug) sends a
    /// rejoin request. The overlay clears once the client owns a Raider again.
    /// Client-local presentation on the camera rig; IMGUI until Phase 6.
    /// </summary>
    public sealed class ExtractionClient : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager _networkManager;

        private bool _registered;
        private bool _rejoinRequested;
        private Texture2D _backdrop;

        public bool IsExtracted { get; private set; }
        public ExtractionRoute LastRoute { get; private set; }

        public void SetNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            TryRegister();
        }

        private void Start() => TryRegister();

        private void TryRegister()
        {
            if (_registered || _networkManager == null || !_networkManager.Initialized)
                return;
            _registered = true;
            _networkManager.ClientManager.RegisterBroadcast<ExtractedBroadcast>(OnExtracted);
        }

        private void OnDestroy()
        {
            if (_registered && _networkManager != null)
                _networkManager.ClientManager.UnregisterBroadcast<ExtractedBroadcast>(OnExtracted);
            if (_backdrop != null)
                Destroy(_backdrop);
        }

        private void OnExtracted(ExtractedBroadcast message, Channel channel)
        {
            IsExtracted = true;
            LastRoute = message.Route;
            _rejoinRequested = false;
        }

        /// <summary>Debug rejoin: asks the server for a fresh Raider at a spawn point.</summary>
        public void RequestRejoin()
        {
            if (!IsExtracted || _rejoinRequested || _networkManager == null || !_networkManager.ClientManager.Started)
                return;
            _rejoinRequested = true;
            _networkManager.ClientManager.Broadcast(new RejoinRequestBroadcast());
        }

        private void Update()
        {
            TryRegister();
            if (!IsExtracted)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
                RequestRejoin();

            if (HasOwnedRaider())
            {
                IsExtracted = false;
                _rejoinRequested = false;
            }
        }

        private bool HasOwnedRaider()
        {
            if (_networkManager == null || !_networkManager.ClientManager.Started)
                return false;
            foreach (NetworkObject networkObject in _networkManager.ClientManager.Objects.Spawned.Values)
            {
                if (networkObject != null && networkObject.IsOwner && networkObject.GetComponent<Raider>() != null)
                    return true;
            }

            return false;
        }

        private void OnGUI()
        {
            if (!IsExtracted)
                return;

            if (_backdrop == null)
            {
                _backdrop = new Texture2D(1, 1);
                _backdrop.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.85f));
                _backdrop.Apply();
            }

            GUI.depth = -50;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _backdrop);
            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
            GUIStyle hint = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            float cy = Screen.height * 0.5f;
            GUI.Label(new Rect(0f, cy - 60f, Screen.width, 60f), "EXTRACTED", title);
            GUI.Label(new Rect(0f, cy + 4f, Screen.width, 26f), "via " + (LastRoute == ExtractionRoute.Hatch ? "Hatch" : "Exfil"), hint);
            GUI.Label(new Rect(0f, cy + 34f, Screen.width, 26f), _rejoinRequested ? "Rejoining..." : "F8: rejoin (debug)", hint);
        }
    }
}
