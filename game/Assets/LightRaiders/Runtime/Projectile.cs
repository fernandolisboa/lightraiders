using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-simulated projectile: travels a flat straight line along its
    /// spawn facing at constant speed and despawns when either its per-tick
    /// segment sweep first hits world geometry (arena floor, walls, obstacles)
    /// or its lifetime expires - whichever comes first. Pure clients never
    /// simulate it - OnStartServer never runs there, so the tick handler is
    /// never subscribed; NetworkTransform replicates the server transform and
    /// FishNet's despawn message destroys it everywhere.
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

        /* Reused across every projectile's per-tick cast. The sweep runs only in
         * the server OnTick handler (single-threaded, one instance at a time
         * per invocation), so one shared buffer is safe and keeps the hot path
         * allocation-free. 16 is far above the collider count any 0.4 m segment
         * ever crosses in this arena, so the buffer never saturates - which is
         * what keeps the nearest-hit selection sound: RaycastNonAlloc gives no
         * nearest-first ordering, so a saturated buffer could drop the nearest
         * hit, not merely farther ones. */
        private static readonly RaycastHit[] _sweepHits = new RaycastHit[16];

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
            Vector3 previous = transform.position;
            Vector3 forward = transform.forward;
            float distance = Speed * (float)base.TimeManager.TickDelta;

            /* Segment sweep across the whole tick step (previous -> next), not a
             * sample of the endpoint: casting the segment is what stops the shot
             * tunneling through a collider thinner than 0.4 m of travel. Detection
             * is server-only (ADR-0005); a hit consumes the shot at the surface. */
            if (TrySweepWorldHit(previous, forward, distance, out Vector3 hitPoint))
            {
                /* Place the server object at the surface for its final tick so
                 * the server-truth stop pose is the hit point. Observers despawn
                 * within their interpolation delay of it rather than rendering
                 * this exact pose - the despawn message follows in the same tick,
                 * before NetworkTransform would send it. Good enough for the
                 * slice; the surface pose matters for #16's impact location. */
                transform.position = hitPoint;
                Despawn();
                return;
            }

            transform.position = previous + forward * distance;
        }

        /// <summary>
        /// Nearest WORLD-geometry hit along the segment, if any. World geometry is
        /// the arena's non-networked colliders (floor, walls, obstacles); Raiders
        /// and projectiles carry a NetworkObject and are skipped, so a shot passes
        /// through them (no damage yet - issue #15 is consumption by geometry only).
        /// Skipping networked bodies also lets the cast see a wall standing behind
        /// another Raider within the same segment. Server-only.
        ///
        /// LANDMINE for #16: this rule is "stop on non-networked", NOT "stop on
        /// world geometry". The breakable targets #16 adds will be networked yet
        /// hittable, so they cannot simply be dropped in here - detection must be
        /// reshaped to pass through Raiders/projectiles while stopping on world
        /// geometry AND damageables (e.g. a physics layer, or an IDamageable lookup).
        /// </summary>
        private static bool TrySweepWorldHit(Vector3 origin, Vector3 direction, float distance, out Vector3 hitPoint)
        {
            hitPoint = default;
            if (distance <= 0f)
                return false;

            int count = Physics.RaycastNonAlloc(
                origin, direction, _sweepHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _sweepHits[i];
                // RaycastNonAlloc fills the buffer unsorted; select the closest world hit ourselves.
                if (hit.distance >= nearest)
                    continue;
                if (hit.collider.GetComponentInParent<NetworkObject>() != null)
                    continue;   // a Raider / projectile - shots are consumed only by world geometry
                nearest = hit.distance;
                hitPoint = hit.point;
                found = true;
            }

            return found;
        }
    }
}
