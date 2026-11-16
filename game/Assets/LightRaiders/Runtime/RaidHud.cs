using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Graybox raid-state HUD (Phase 2): the local Raider's current Layer, plus
    /// a fade on every Layer change. Client-local and read-only - it reads the
    /// replicated <see cref="LayerMember"/> of the owned Raider. IMGUI on purpose:
    /// Phase 6 replaces all of it. Later Phase 2 slices add Waygate cooldown,
    /// Exfil countdown and Hatch channel lines here.
    /// </summary>
    public sealed class RaidHud : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager _networkManager;
        [SerializeField]
        private ScreenFade _fade;

        private NetworkObject _owned;
        private int _lastLayer = int.MinValue;

        public void SetNetworkManager(NetworkManager networkManager) => _networkManager = networkManager;

        public void SetFade(ScreenFade fade) => _fade = fade;

        /// <summary>The locally owned Raider, or null. Re-acquired after death/respawn.</summary>
        public NetworkObject OwnedRaider => _owned;

        private void Update()
        {
            if (_owned == null || !_owned.IsSpawned)
            {
                _owned = FindOwnedRaider();
                _lastLayer = int.MinValue;
            }

            if (_owned == null)
                return;

            LayerMember member = _owned.GetComponent<LayerMember>();
            int layer = member != null ? member.LayerId : LayerMember.Surface;
            // Fade on a CHANGE only - not on first acquisition (spawn/respawn).
            if (_lastLayer != int.MinValue && layer != _lastLayer && _fade != null)
                _fade.Trigger();
            _lastLayer = layer;
        }

        private NetworkObject FindOwnedRaider()
        {
            if (_networkManager == null || !_networkManager.Initialized)
                return null;

            foreach (NetworkObject networkObject in _networkManager.ClientManager.Objects.Spawned.Values)
            {
                if (networkObject != null && networkObject.IsOwner && networkObject.GetComponent<Raider>() != null)
                    return networkObject;
            }

            return null;
        }

        private void OnGUI()
        {
            if (_owned == null)
                return;

            float y = 40f;
            GUI.color = Color.white;
            GUI.Label(new Rect(10f, y, 400f, 22f), "Layer: " + LayerInfo.NameOf(_lastLayer == int.MinValue ? LayerMember.Surface : _lastLayer));
            y += 22f;
        }
    }
}
