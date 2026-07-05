using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-authoritative hostile emitter per ADR-0005: a stationary graybox
    /// that, on a fixed cadence, fires the shared Projectile at the NEAREST Raider
    /// within range and holds fire when none is in range. It carries the Hostile
    /// side of the #16 team rule, so its shots strip a Raider's Shield then Health
    /// and never touch the emitter itself or any other Hostile-side body (friendly
    /// fire off). Deliberately NOT a Choir unit (Phase 3): no movement, no AI
    /// states, no Choir naming - pure scaffolding so damage is feelable in
    /// playtests. It carries a Health component only so #19 can draw a world-space
    /// bar over it; it deliberately has NO zero-Health behavior this slice, so a
    /// Raider can deplete it yet it keeps firing (breakability/death are out of
    /// scope - #18). Pure clients never simulate it: OnStartServer never runs
    /// there, so the tick handler is never subscribed and no shot ever originates
    /// client-side.
    /// </summary>
    public sealed class HostileEmitter : NetworkBehaviour
    {
        // ~1.5s between shots (PRD tuning): slow enough to read as a threat, 45 ticks at 30Hz leaves a comfortably testable window.
        public const float FirePeriodSeconds = 1.5f;
        // 15 m acquisition radius (PRD tuning), measured on the ground plane like the Raider aim - shots are Y-locked.
        public const float RangeMeters = 15f;
        // Flat damage per hit. 25 vs Shield 50 / Health 100 mirrors RaiderWeapon: two hits through the Shield, then four into Health.
        public const int ProjectileDamage = 25;
        // Clears the emitter body (the graybox pillar spans radius ~0.5) so the bolt visibly emerges past it; same intent as RaiderWeapon.MuzzleForwardOffset.
        public const float MuzzleForwardOffset = 1.4f;
        // Matches the Raider muzzle height and the target capsule center: shots leave at chest height and fly flat at y=1 over flat ground.
        public const float MuzzleHeight = 1f;

        /* Degenerate-direction guard, shared shape with RaiderMovement.AimEpsilonSqr:
         * a Raider standing (nearly) on top of the emitter yields a ~zero flat
         * direction; hold fire that tick rather than feed LookRotation a zero
         * vector. A fresh acquisition arrives next tick anyway. */
        private const float AimEpsilonSqr = 1e-4f;

        [SerializeField]
        private NetworkObject _projectilePrefab;   // wired by HostileEmitterPrefabGenerator; generation-time fail-fast is the null contract (same as RaiderWeapon._projectilePrefab)

        /* Default 0 is provably safe (same reasoning as RaiderWeapon): TimeManager.Tick
         * is a uint, so "_nextAllowedFireTick == 0" reads as "may fire now" - a freshly
         * spawned emitter fires on the first tick a Raider is in range, and the field
         * only advances past the current tick after a successful shot. */
        private uint _nextAllowedFireTick;
        private uint _firePeriodTicks;

        /// <summary>Single source of truth for fire-period-in-ticks; tests use the same conversion.</summary>
        public static uint FirePeriodTicks(TimeManager timeManager)
            => (uint)Mathf.CeilToInt(FirePeriodSeconds * timeManager.TickRate);

        public override void OnStartServer()
        {
            /* Assign the period BEFORE subscribing so the handler never observes a
             * default(uint) period. Server-only: OnStartServer runs on the server
             * alone, so the emitter simulates nowhere else. */
            _firePeriodTicks = FirePeriodTicks(base.TimeManager);
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

            /* Cadence gate first: while the cooldown is pending, spend no effort
             * scanning for a target. When the gate opens and a Raider is in range,
             * the shot fires this tick and the cooldown re-arms; when none is in
             * range the gate stays open, so the emitter fires the instant a Raider
             * enters range rather than waiting out a phantom cooldown. */
            uint tick = base.TimeManager.Tick;
            if (tick < _nextAllowedFireTick)
                return;

            Health target = FindNearestRaiderInRange();
            if (target == null)
                return;   // hold fire: no Raider in range

            Vector3 flatDirection = target.transform.position - transform.position;
            flatDirection.y = 0f;   // Y-locked: shots fly flat at muzzle height, matching the Raider aim.
            if (flatDirection.sqrMagnitude < AimEpsilonSqr)
                return;   // degenerate (Raider on top of the emitter) - hold this tick

            _nextAllowedFireTick = tick + _firePeriodTicks;
            FireAt(flatDirection);
        }

        /// <summary>
        /// Nearest Raider-side Health within range on the ground plane, or null when
        /// none is in range. The team rule lives in the Side check: only Side.Raider
        /// bodies are candidates, so the emitter never targets itself, another
        /// emitter, or a Hostile dummy target. Dead Raiders are not filtered out
        /// (Raider death is #18); a Raider at zero Health simply absorbs no more
        /// damage. Server-only, called from the tick handler.
        /// </summary>
        private Health FindNearestRaiderInRange()
        {
            Vector3 origin = transform.position;
            Health nearest = null;
            // Seed with the range threshold (squared): a candidate must be strictly nearer to be selected, so anything beyond range is rejected.
            float nearestSqr = RangeMeters * RangeMeters;

            foreach (NetworkObject networkObject in base.ServerManager.Objects.Spawned.Values)
            {
                if (networkObject == null)
                    continue;

                Health health = networkObject.GetComponent<Health>();
                if (health == null || health.Side != Side.Raider)
                    continue;

                Vector3 delta = health.transform.position - origin;
                delta.y = 0f;   // planar distance, matching the Y-locked shot.
                float sqr = delta.sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = health;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Spawns a server-owned Projectile at the muzzle, rotated so its forward
        /// points along the (flat) aim direction, stamped Side.Hostile so it damages
        /// Raiders only. Mirrors RaiderWeapon's instantiate-then-spawn path.
        /// </summary>
        private void FireAt(Vector3 flatDirection)
        {
            Quaternion rotation = Quaternion.LookRotation(flatDirection, Vector3.up);

            Vector3 origin = transform.position
                + rotation * Vector3.forward * MuzzleForwardOffset
                + Vector3.up * MuzzleHeight;

            /* Canonical FishNet instantiate-then-spawn (same as RaiderWeapon).
             * asServer: true - the server creates it. No owner connection: the
             * projectile is server-owned, no client has authority, no RPCs exist. */
            NetworkObject nob = base.NetworkManager.GetPooledInstantiated(_projectilePrefab, origin, rotation, true);

            /* Stamp the shot BEFORE spawning so hit detection has its side/damage from
             * the first tick: Side.Hostile damages Raider-side Health only - never the
             * emitter, never another emitter or a dummy target. */
            nob.GetComponent<Projectile>().ServerInitCombat(Side.Hostile, ProjectileDamage);
            base.ServerManager.Spawn(nob);
        }
    }
}
