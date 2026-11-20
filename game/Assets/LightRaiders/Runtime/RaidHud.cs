using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Graybox raid-state HUD (Phase 2): the local Raider's current Layer, plus
    /// a fade on every door transit. Client-local and read-only - it reads the
    /// replicated <see cref="LayerMember"/> of the owned Raider, and fades on the
    /// owner's predicted transit (<see cref="RaiderMovement.LocalDoorTransit"/>)
    /// so the fade hides the jump rather than trailing the Layer SyncVar. IMGUI on purpose:
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
        private RaiderMovement _ownedMovement;
        private int _lastLayer = int.MinValue;

        public void SetNetworkManager(NetworkManager networkManager) => _networkManager = networkManager;

        public void SetFade(ScreenFade fade) => _fade = fade;

        /// <summary>The locally owned Raider, or null. Re-acquired after death/respawn.</summary>
        public NetworkObject OwnedRaider => _owned;

        private void Update()
        {
            if (_owned == null || !_owned.IsSpawned)
                Acquire(FindOwnedRaider());

            if (_owned == null)
                return;

            LayerMember member = _owned.GetComponent<LayerMember>();
            _lastLayer = member != null ? member.LayerId : LayerMember.Surface;
        }

        private void Acquire(NetworkObject owned)
        {
            if (_ownedMovement != null)
                _ownedMovement.LocalDoorTransit -= OnLocalDoorTransit;

            _owned = owned;
            _ownedMovement = owned != null ? owned.GetComponent<RaiderMovement>() : null;
            _lastLayer = int.MinValue;

            if (_ownedMovement != null)
                _ownedMovement.LocalDoorTransit += OnLocalDoorTransit;
        }

        private void OnLocalDoorTransit()
        {
            if (_fade != null)
                _fade.Trigger();
        }

        private void OnDisable() => Acquire(null);

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
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.Label(new Rect(10f, y, 400f, 22f), "Layer: " + LayerInfo.NameOf(_lastLayer == int.MinValue ? LayerMember.Surface : _lastLayer));
            y += 22f;
            GUI.color = previous;
        }
    }
}
