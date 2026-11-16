using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// One end of a Waygate pair (CONTEXT.md "Waygate"): a fixed teleporter to its
    /// partner. Any use puts BOTH gates on one shared cooldown. Networked and
    /// Layer-scoped (ADR-0008), spawned in pairs by <see cref="WaygateSpawner"/>.
    ///
    /// Everything a client needs to predict a use is replicated on THIS gate - the
    /// cooldown's end (server tick) and the partner's arrival pose and Layer - so
    /// the owner can predict even when the partner sits in a Layer it does not
    /// observe. The server decides; reconcile corrects a wrong prediction.
    /// </summary>
    public sealed class Waygate : NetworkBehaviour
    {
        /// <summary>Planar reach for using a gate (same as a door).</summary>
        public const float UseRadius = 1.5f;
        public const float UseHeightTolerance = 2f;
        /// <summary>Shared cooldown after any use (PRD #31 tuning).</summary>
        public const float CooldownSeconds = 90f;
        /// <summary>Arrival distance in front of the partner gate, beyond UseRadius.</summary>
        public const float ExitDistance = 2.5f;

        private static readonly List<Waygate> _active = new List<Waygate>();

        private readonly SyncVar<uint> _cooldownEndTick = new();
        private readonly SyncVar<Vector3> _destination = new();
        private readonly SyncVar<float> _destinationYaw = new();
        private readonly SyncVar<int> _destinationLayer = new();

        /// <summary>Server only: the partner whose cooldown is shared.</summary>
        private Waygate _partner;

        public static IReadOnlyList<Waygate> Active => _active;

        /// <summary>Server tick at which the pair is usable again (0 = never used).</summary>
        public uint CooldownEndTick => _cooldownEndTick.Value;

        /// <summary>The Layer a use moves the Raider into (the partner's).</summary>
        public int DestinationLayer => _destinationLayer.Value;

        /// <summary>
        /// Ready by this peer's view. On a client, <c>TimeManager.Tick</c> estimates
        /// the server tick, so it is comparable with the replicated end tick.
        /// </summary>
        public bool IsReady => base.TimeManager != null && base.TimeManager.Tick >= _cooldownEndTick.Value;

        public float CooldownRemainingSeconds
        {
            get
            {
                if (base.TimeManager == null)
                    return 0f;
                uint tick = base.TimeManager.Tick;
                uint end = _cooldownEndTick.Value;
                return tick >= end ? 0f : (float)((end - tick) * base.TimeManager.TickDelta);
            }
        }

        public static uint CooldownTicks(FishNet.Managing.Timing.TimeManager timeManager)
            => (uint)Mathf.CeilToInt(CooldownSeconds * timeManager.TickRate);

        public override void OnStartNetwork()
        {
            if (!_active.Contains(this))
                _active.Add(this);
        }

        public override void OnStopNetwork()
        {
            _active.Remove(this);
            _partner = null;
        }

        /// <summary>Server: pairs two spawned gates both ways and publishes each one's destination.</summary>
        public static void ServerPair(Waygate a, Waygate b)
        {
            a.ServerSetPartner(b);
            b.ServerSetPartner(a);
        }

        private void ServerSetPartner(Waygate partner)
        {
            _partner = partner;
            Transform t = partner.transform;
            _destination.Value = t.position + t.forward * ExitDistance;
            _destinationYaw.Value = t.eulerAngles.y;
            _destinationLayer.Value = LayerMember.ServerLayerOf(partner);
        }

        /// <summary>
        /// Server: consumes a use if the pair is ready, starting the shared
        /// cooldown on both gates. Returns false (and changes nothing) otherwise.
        /// </summary>
        public bool ServerTryUse()
        {
            if (!IsServerInitialized || _partner == null || !IsReady)
                return false;

            uint end = base.TimeManager.Tick + CooldownTicks(base.TimeManager);
            _cooldownEndTick.Value = end;
            _partner._cooldownEndTick.Value = end;
            return true;
        }

        /// <summary>Where a Raider using this gate lands, by the shared arrival-slot rule.</summary>
        public Vector3 FindArrivalPosition(float capsuleRadius, float capsuleHeight)
        {
            Quaternion facing = Quaternion.Euler(0f, _destinationYaw.Value, 0f);
            return ArrivalSlots.Find(_destination.Value, facing * Vector3.right, facing * Vector3.forward, capsuleRadius, capsuleHeight);
        }

        /// <summary>
        /// The nearest gate within <paramref name="radius"/> of <paramref name="position"/>
        /// that belongs to <paramref name="networkManager"/> (several managers coexist
        /// in-process in tests, each with its own copy of every gate).
        /// </summary>
        public static Waygate FindNearest(Vector3 position, NetworkManager networkManager, float radius)
        {
            Waygate best = null;
            float bestSqr = radius * radius;
            for (int i = 0; i < _active.Count; i++)
            {
                Waygate gate = _active[i];
                if (gate == null || gate.NetworkManager != networkManager)
                    continue;

                Vector3 offset = gate.transform.position - position;
                if (Mathf.Abs(offset.y) > UseHeightTolerance)
                    continue;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = gate;
                }
            }

            return best;
        }

        public static Waygate FindUsable(Vector3 position, NetworkManager networkManager)
            => FindNearest(position, networkManager, UseRadius);
    }
}
