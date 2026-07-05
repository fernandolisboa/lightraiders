using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-authoritative fire per ADR-0005: consumes the level-state
    /// FirePressed from the Raider's single intent pipe (RaiderMovement's
    /// server tick branch calls in), validates a per-Raider cooldown in server
    /// ticks, and spawns a server-owned Projectile from the muzzle along the
    /// server-authoritative facing.
    /// </summary>
    public sealed class RaiderWeapon : NetworkBehaviour
    {
        // 4 shots/s: responsive, and 8 ticks at 30Hz leaves a comfortably testable window.
        public const float FireCooldownSeconds = 0.25f;
        // Flat damage per hit (PRD tuning). 25 vs Shield 50 / Health 100 = two hits through the Shield, then four into Health.
        public const int ProjectileDamage = 25;
        // Clears the CharacterController (radius 0.5) AND the AimIndicator tip (local z 0.9+0.4=1.3), so the bolt visibly emerges past the indicator.
        public const float MuzzleForwardOffset = 1.4f;
        // Matches the CC center / Visual capsule center / AimIndicator height: shots leave at chest height and fly flat at y=1 over flat ground.
        public const float MuzzleHeight = 1f;

        [SerializeField]
        private NetworkObject _projectilePrefab;   // wired by RaiderPrefabGenerator; generation-time fail-fast is the null contract (same as _actions)

        /* Default 0 is PROVABLY safe here - no sentinel needed, unlike AimPoint:
         * TimeManager.Tick is a uint (always >= 0), so "_nextAllowedFireTick == 0"
         * encodes exactly the correct domain statement "no cooldown pending".
         * A freshly spawned Raider can therefore fire immediately, and the field
         * only ever advances past the current tick after a successful shot, so
         * rapid re-fire is blocked. (uint wrap at 30Hz = ~4.5 years of uptime.) */
        private uint _nextAllowedFireTick;
        private uint _cooldownTicks;

        /// <summary>Single source of truth for cooldown-in-ticks; tests use the same conversion.</summary>
        public static uint CooldownTicks(TimeManager timeManager)
            => (uint)Mathf.CeilToInt(FireCooldownSeconds * timeManager.TickRate);

        public override void OnStartServer()
        {
            _cooldownTicks = CooldownTicks(base.TimeManager);
        }

        /// <summary>
        /// Called by RaiderMovement's server tick branch - the single intent
        /// pipe. Level semantics: firePressed is true on every tick the control
        /// is held; the cooldown owns the fire rate (held button = autofire).
        /// </summary>
        internal void TryFireOnServer(bool firePressed)
        {
            if (!IsServerInitialized || !firePressed)
                return;

            uint tick = base.TimeManager.Tick;
            if (tick < _nextAllowedFireTick)
                return;
            _nextAllowedFireTick = tick + _cooldownTicks;

            Vector3 origin = transform.position
                + transform.forward * MuzzleForwardOffset
                + Vector3.up * MuzzleHeight;

            /* Canonical FishNet instantiate-then-spawn (PlayerSpawner pattern).
             * asServer: true - the server is creating it. No owner connection:
             * the projectile is server-owned; no client has authority and no
             * RPCs exist on it. */
            NetworkObject nob = base.NetworkManager.GetPooledInstantiated(_projectilePrefab, origin, transform.rotation, true);

            /* Stamp the shot BEFORE spawning so hit detection has its side/damage
             * from the first tick: on the Raider side, so it damages hostile-side
             * targets only - never the shooter, never other Raiders. */
            nob.GetComponent<Projectile>().ServerInitCombat(Side.Raider, ProjectileDamage);
            base.ServerManager.Spawn(nob);
        }
    }
}
