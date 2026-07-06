using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-side Raider death: when the sibling Health reaches zero, the Raider is
    /// removed from play and the session's RaiderRespawner is told where it fell and
    /// who owned it - it drops a loot bag and respawns the owner's Raider after a
    /// delay. Server-authoritative per ADR-0005: OnStartServer runs on the server
    /// alone, so death is decided nowhere else.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class RaiderDeathHandler : NetworkBehaviour
    {
        private Health _health;

        public override void OnStartServer()
        {
            _health = GetComponent<Health>();
            _health.Died += OnDied;
        }

        public override void OnStopServer()
        {
            if (_health != null)
                _health.Died -= OnDied;
        }

        private void OnDied()
        {
            /* Capture the death pose and owner BEFORE despawning: this object is
             * about to be destroyed and cannot schedule its own respawn, so the
             * persistent session respawner carries it out. */
            Vector3 deathPosition = transform.position;
            NetworkConnection owner = base.Owner;

            RaiderRespawner respawner = base.NetworkManager != null
                ? base.NetworkManager.GetComponent<RaiderRespawner>()
                : null;
            if (respawner != null)
                respawner.HandleRaiderDeath(owner, deathPosition);
            else
                // Misconfiguration: surface it loudly rather than silently vanishing a player.
                Debug.LogWarning("RaiderDeathHandler found no RaiderRespawner on the session NetworkManager - the Raider despawns with no loot bag and no respawn. Wire a RaiderRespawner beside the NetworkManager.");

            // Removed from play; the respawn brings a fresh Raider, not this one.
            Despawn();
        }
    }
}
