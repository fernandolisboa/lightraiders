using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// The single damage model: one replicated Shield-over-Health bar carried by
    /// every damageable. Server-authoritative per ADR-0005 - only the server
    /// mutates the bars, through ApplyDamageOnServer; clients read the replicated
    /// SyncVar values. Shield fully depletes before Health, and neither bar
    /// self-regenerates (nothing here ever raises them - Shield refills are a
    /// later slice's Shield Cell). Death is signalled, not enacted: the owner of
    /// the damageable decides what zero Health means (a target respawns, a Raider
    /// drops a loot bag in #18).
    /// </summary>
    public sealed class Health : NetworkBehaviour
    {
        public const int MaxShield = 50;
        public const int MaxHealth = 100;

        [SerializeField]
        private Side _side = Side.Hostile;   // per-prefab; dummy targets are Hostile

        /* First SyncVars in the project. Default settings: server-writable,
         * readable by every observer, so a connected client sees Shield/Health
         * with no explicit RPC. FishNet's required shape is a readonly field
         * initialised with new() (the code generator wires the rest). */
        private readonly SyncVar<int> _shield = new();
        private readonly SyncVar<int> _health = new();

        public Side Side => _side;
        public int Shield => _shield.Value;
        public int CurrentHealth => _health.Value;
        public bool IsAlive => _health.Value > 0;

        /// <summary>
        /// Raised on the server the tick Health first reaches zero (never on
        /// clients, never twice - see the dead guard in ApplyDamageOnServer).
        /// </summary>
        public event Action Died;

        public override void OnStartServer()
        {
            /* Full bars from the server, the source of truth; the values
             * replicate to clients as the SyncVars' initial state. */
            _shield.Value = MaxShield;
            _health.Value = MaxHealth;
        }

        /// <summary>
        /// Applies flat damage server-side: Shield absorbs first, any overflow
        /// spills into Health, both clamped at zero. A no-op when already dead, so
        /// Died fires exactly once on the transition to zero.
        /// </summary>
        public void ApplyDamageOnServer(int amount)
        {
            if (!IsServerInitialized || amount <= 0 || _health.Value <= 0)
                return;

            int remaining = amount;
            if (_shield.Value > 0)
            {
                int absorbed = Mathf.Min(_shield.Value, remaining);
                _shield.Value -= absorbed;
                remaining -= absorbed;
            }

            if (remaining > 0)
                _health.Value = Mathf.Max(0, _health.Value - remaining);

            if (_health.Value == 0)
                Died?.Invoke();
        }
    }
}
