using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-simulated projectile: travels a flat straight line along its
    /// spawn facing at constant speed and despawns when its lifetime expires.
    /// Pure clients never simulate it - OnStartServer never runs there, so the
    /// tick handler is never subscribed; NetworkTransform replicates the
    /// server transform and FishNet's despawn message destroys it everywhere.
    /// </summary>
    public sealed class Projectile : NetworkBehaviour
    {
        // 2.4x Raider MoveSpeed (5): reads as a shot, yet 0.4 m/tick at 30Hz stays smooth under the default 2-tick interpolation.
        public const float Speed = 12f;
        // 18 m range (Speed * 1.5s) ~ crosses half the 40x40 arena; 45 ticks - short enough for a fast despawn test.
        public const float LifetimeSeconds = 1.5f;

        /// <summary>Server tick at spawn. Server-side value only (0 on client instances); tests read it from the server view.</summary>
        public uint SpawnTick { get; private set; }

        private uint _despawnAtTick;

        /// <summary>Single source of truth for lifetime-in-ticks; tests use the same conversion.</summary>
        public static uint LifetimeTicks(TimeManager timeManager)
            => (uint)Mathf.CeilToInt(LifetimeSeconds * timeManager.TickRate);

        public override void OnStartServer()
        {
            /* Assign lifetime state BEFORE subscribing: the handler can then
             * never observe the default(uint) despawn tick (which would read
             * as "despawn immediately"). Unlike RaiderMovement's AimPoint
             * sentinel there is no window where a default is consumable. */
            SpawnTick = base.TimeManager.Tick;
            _despawnAtTick = SpawnTick + LifetimeTicks(base.TimeManager);
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
            {
                /* NetworkBehaviour.Despawn() despawns own NetworkObject with the
                 * default DespawnType.Destroy: destroyed on the server and every
                 * client. No pooling in issue #6 scope. */
                Despawn();
                return;
            }

            /* Flat straight line: RaiderMovement Y-locks facing, so forward.y
             * is 0 and y stays at muzzle height. No finite/magnitude guards are
             * introduced here on purpose - every input is server-authoritative
             * state already sanitized upstream (ADR-0005), never client data.
             * If a guard is ever added, use float.IsFinite (RaiderMovement
             * precedent: Infinity passes bare epsilon comparisons). */
            transform.position += transform.forward * (Speed * (float)base.TimeManager.TickDelta);
        }
    }
}
