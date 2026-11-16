using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Graybox raid-state HUD (Phase 2): the local Raider's current Layer, plus
    /// a fade on every door or Waygate transit, and the nearby Waygate cooldown. Client-local and read-only - it reads the
    /// replicated <see cref="LayerMember"/> of the owned Raider, and fades on the
    /// owner's predicted transit (<see cref="RaiderMovement.LocalTransit"/>)
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

        /// <summary>The Waygate line shows while the Raider is this close to a gate.</summary>
        public const float WaygateHudRadius = 6f;
        /// <summary>The Hatch prompt shows while the Raider is this close to a Hatch.</summary>
        public const float HatchHudRadius = 4f;

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
                _ownedMovement.LocalTransit -= OnLocalTransit;

            _owned = owned;
            _ownedMovement = owned != null ? owned.GetComponent<RaiderMovement>() : null;
            _lastLayer = int.MinValue;

            if (_ownedMovement != null)
                _ownedMovement.LocalTransit += OnLocalTransit;
        }

        private void OnLocalTransit()
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

            Waygate gate = Waygate.FindNearest(_owned.transform.position, _networkManager, WaygateHudRadius);
            if (gate != null)
            {
                float remaining = gate.CooldownRemainingSeconds;
                GUI.Label(new Rect(10f, y, 400f, 22f), remaining > 0f
                    ? "Waygate: cooling down " + Mathf.CeilToInt(remaining) + "s"
                    : "Waygate: ready (E)");
                y += 22f;
            }

            // Hatch (#39): keys held, and the channel progress or a prompt at a nearby Hatch.
            HatchKeys keys = _owned.GetComponent<HatchKeys>();
            int keyCount = keys != null ? keys.Count : 0;
            Hatch hatch = Hatch.FindNearest(_owned.transform.position, _networkManager, HatchHudRadius);
            string hatchLine = "Hatch keys: " + keyCount;
            if (hatch != null && hatch.IsChannelling && hatch.ChannellerId == _owned.ObjectId)
                hatchLine = "Hatch: opening " + Mathf.RoundToInt(hatch.ChannelProgress * 100f) + "% (stay close, don't get hit)";
            else if (hatch != null)
                hatchLine += keyCount > 0 ? " - Hatch nearby (E to open)" : " - Hatch nearby (needs a key)";
            GUI.Label(new Rect(10f, y, 500f, 22f), hatchLine);
            y += 22f;

            // Exfil (#38): every observed site that is counting down or departing.
            Vector3 position = _owned.transform.position;
            foreach (ExfilSite site in ExfilSite.Active)
            {
                if (site == null || site.NetworkManager != _networkManager || site.State == ExfilState.Idle)
                    continue;
                Vector3 offset = site.transform.position - position;
                offset.y = 0f;
                bool inZone = offset.sqrMagnitude <= ExfilSite.ZoneRadius * ExfilSite.ZoneRadius;
                string text = site.State == ExfilState.Called
                    ? "Exfil called: " + Mathf.CeilToInt(site.CountdownRemainingSeconds) + "s"
                    : "Exfil departing";
                GUI.Label(new Rect(10f, y, 400f, 22f), text + (inZone ? " (in zone)" : " (" + Mathf.RoundToInt(offset.magnitude) + " m)"));
                y += 22f;
            }
            GUI.color = previous;
        }
    }
}
