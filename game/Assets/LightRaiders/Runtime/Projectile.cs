using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-simulated projectile: travels a flat straight line along its
    /// spawn facing at constant speed and despawns when either its per-tick
    /// segment sweep first hits a consuming surface - world geometry (arena
    /// floor, walls, obstacles) or an opposite-side damageable, which it damages
    /// on the way out - or its lifetime expires, whichever comes first. Pure
    /// clients never simulate it - OnStartServer never runs there, so the tick
    /// handler is never subscribed; NetworkTransform replicates the server
    /// transform and FishNet's despawn message destroys it everywhere.
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

        /* Server-only combat stamp, set by the firer via ServerInitCombat before
         * the object is spawned. Not networked: only the server runs hit
         * detection and damage (ADR-0005), so clients never need these. */
        private Side _side;
        private int _damage;

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

        /// <summary>
        /// Stamps which side fired this shot and how hard, server-side, before it
        /// is spawned. Side drives friendly fire (damage crosses sides only). Must
        /// be called on the server instance prior to ServerManager.Spawn. (Kill
        /// attribution - who fired - arrives with #18, when something consumes it.)
        /// </summary>
        public void ServerInitCombat(Side side, int damage)
        {
            _side = side;
            _damage = damage;
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
            if (TrySweepHit(previous, forward, distance, out Vector3 hitPoint, out Health damageable))
            {
                /* Damage the target (if any) before despawning, so it reaches
                 * zero on the same tick the shot lands; world-geometry hits carry
                 * no target. */
                if (damageable != null)
                    damageable.ApplyDamageOnServer(_damage);

                /* Place the server object at the surface/target for its final
                 * tick so the server-truth stop pose is the hit point. Observers
                 * despawn within their interpolation delay of it rather than
                 * rendering this exact pose - the despawn message follows in the
                 * same tick, before NetworkTransform would send it. */
                transform.position = hitPoint;
                Despawn();
                return;
            }

            transform.position = previous + forward * distance;
        }

        /// <summary>
        /// Nearest CONSUMING hit along the segment - the first surface that stops
        /// the shot. World geometry (non-networked colliders: floor, walls,
        /// obstacles) stops it with no target. A Health of the OPPOSITE side (a
        /// hostile-side damageable) stops it AND is returned via <paramref
        /// name="damageable"/> for damage. Same-side Health and every other
        /// networked body (Raiders without Health, other projectiles) are skipped,
        /// so a shot passes through its own side - friendly fire off - and still
        /// sees a target standing behind a friendly Raider within the segment.
        /// Server-only. Non-static because it reads the instance's _side.
        /// </summary>
        private bool TrySweepHit(Vector3 origin, Vector3 direction, float distance, out Vector3 hitPoint, out Health damageable)
        {
            hitPoint = default;
            damageable = null;
            if (distance <= 0f)
                return false;

            int count = Physics.RaycastNonAlloc(
                origin, direction, _sweepHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _sweepHits[i];
                // RaycastNonAlloc fills the buffer unsorted; select the closest consuming hit ourselves.
                if (hit.distance >= nearest)
                    continue;

                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                {
                    if (health.Side == _side)
                        continue;   // same side - pass through (friendly fire off)

                    // Opposite-side damageable: consumes the shot AND takes damage.
                    nearest = hit.distance;
                    hitPoint = hit.point;
                    damageable = health;
                    found = true;
                    continue;
                }

                if (hit.collider.GetComponentInParent<NetworkObject>() != null)
                    continue;   // networked but not damageable (Raider, projectile) - pass through

                // Non-networked collider: world geometry - consumes the shot, no damage.
                nearest = hit.distance;
                hitPoint = hit.point;
                damageable = null;
                found = true;
            }

            return found;
        }
    }
}
