using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Graybox loot bag: a server-spawned marker dropped where a Raider died. It
    /// holds nothing yet (contents arrive with the Phase 4 economy) and simply
    /// despawns everywhere after a generous lifetime. Server-simulated only, like
    /// Projectile: OnStartServer never runs on pure clients, so the tick handler is
    /// never subscribed there; FishNet's despawn destroys it on every view.
    /// </summary>
    public sealed class LootBag : NetworkBehaviour
    {
        // ~60s: long enough for a returning player to find a fallen bag, short enough to keep the graybox arena clear.
        public const float DefaultLifetimeSeconds = 60f;

        private float _lifetimeSeconds = DefaultLifetimeSeconds;
        private uint _despawnAtTick;

        /// <summary>Lifetime-in-ticks for a seconds value; tests use the same conversion.</summary>
        public static uint LifetimeTicks(TimeManager timeManager, float seconds)
            => (uint)Mathf.CeilToInt(seconds * timeManager.TickRate);

        /// <summary>
        /// Overrides the despawn lifetime, server-side, before the bag is spawned:
        /// the death path leaves the default (~60s); a test passes a short value to
        /// observe the despawn quickly. Must be called prior to ServerManager.Spawn.
        /// </summary>
        public void ServerInitLifetime(float seconds) => _lifetimeSeconds = seconds;

        public override void OnStartServer()
        {
            _despawnAtTick = base.TimeManager.Tick + LifetimeTicks(base.TimeManager, _lifetimeSeconds);
            base.TimeManager.OnTick += TimeManager_OnTick;
        }

        public override void OnStopServer()
        {
            if (base.TimeManager != null)
                base.TimeManager.OnTick -= TimeManager_OnTick;
        }

        private void TimeManager_OnTick()
        {
            if (!IsServerInitialized)
                return;

            if (base.TimeManager.Tick >= _despawnAtTick)
                Despawn();
        }
    }
}
