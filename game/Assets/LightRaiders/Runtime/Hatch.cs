using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// A Hatch (#39): a networked, Layer-scoped solo exit. A Raider holding a
    /// Hatch Key interacts in reach to start a 5 s channel, tracked on the server.
    /// Leaving the channel radius, changing Layer, or taking any damage cancels it
    /// and keeps the key; completing it consumes the key and extracts the Raider
    /// through <see cref="ExtractionService"/> (the same path as Exfil). One
    /// channel per Hatch at a time. The channeller and the channel's start and end
    /// ticks are SyncVars so the HUD can show progress.
    /// </summary>
    public sealed class Hatch : NetworkBehaviour
    {
        public const float ChannelSeconds = 5f;
        /// <summary>Planar reach for starting a channel (same as doors and gates).</summary>
        public const float UseRadius = 1.5f;
        /// <summary>Planar distance beyond which a running channel cancels: a little slack over UseRadius.</summary>
        public const float ChannelRadius = 2f;
        public const float UseHeightTolerance = 2f;

        private static readonly List<Hatch> _active = new List<Hatch>();

        /// <summary>Object id of the channelling Raider, or -1.</summary>
        private readonly SyncVar<int> _channellerId = new(-1);
        private readonly SyncVar<uint> _channelStartTick = new();
        private readonly SyncVar<uint> _channelEndTick = new();

        /// <summary>Server only.</summary>
        private NetworkObject _channeller;
        private Health _channellerHealth;
        private bool _cancelRequested;

        public static IReadOnlyList<Hatch> Active => _active;

        /// <summary>
        /// Server: a channel completed and its key was consumed; carries the Raider
        /// and its keys left, just before it extracts.
        /// </summary>
        public event System.Action<NetworkObject, int> ServerChannelCompleted;

        public bool IsChannelling => _channellerId.Value >= 0;
        public int ChannellerId => _channellerId.Value;

        /// <summary>0..1 by this peer's view of the server tick; 0 when idle.</summary>
        public float ChannelProgress
        {
            get
            {
                if (!IsChannelling || base.TimeManager == null)
                    return 0f;
                uint start = _channelStartTick.Value;
                uint end = _channelEndTick.Value;
                if (end <= start)
                    return 1f;
                uint tick = base.TimeManager.Tick;
                return tick <= start ? 0f : Mathf.Clamp01((float)(tick - start) / (end - start));
            }
        }

        public override void OnStartNetwork()
        {
            if (!_active.Contains(this))
                _active.Add(this);
        }

        public override void OnStopNetwork() => _active.Remove(this);

        /* PostTick, not Tick: every hit of tick T (projectiles resolve in OnTick)
         * has landed before the channel is judged, so a hit on the final tick
         * cancels instead of losing to completion by subscription order. */
        public override void OnStartServer() => base.TimeManager.OnPostTick += ServerTick;

        public override void OnStopServer()
        {
            if (base.TimeManager != null)
                base.TimeManager.OnPostTick -= ServerTick;
            ClearChannel();
        }

        /// <summary>
        /// Server: starts a channel for <paramref name="raider"/> if the Hatch is
        /// free, the Raider is in reach and in the Hatch's Layer, and holds a key.
        /// The key is NOT consumed here - only on completion.
        /// </summary>
        public bool ServerTryStartChannel(NetworkObject raider)
        {
            if (!IsServerInitialized || raider == null || IsChannelling)
                return false;
            if (LayerMember.ServerLayerOf(raider) != LayerMember.ServerLayerOf(this))
                return false;
            if (PlanarDistance(raider.transform.position) > UseRadius)
                return false;
            HatchKeys keys = raider.GetComponent<HatchKeys>();
            if (keys == null || keys.Count <= 0)
                return false;

            _channeller = raider;
            _channellerHealth = raider.GetComponent<Health>();
            _cancelRequested = false;
            if (_channellerHealth != null)
                _channellerHealth.Damaged += OnChannellerDamaged;

            uint tick = base.TimeManager.Tick;
            _channelStartTick.Value = tick;
            _channelEndTick.Value = tick + (uint)Mathf.CeilToInt(ChannelSeconds * base.TimeManager.TickRate);
            _channellerId.Value = raider.ObjectId;
            return true;
        }

        /* Damage can land mid-tick from a projectile; flag it and resolve in the
         * Hatch's PostTick, where cancel is checked before completion. */
        private void OnChannellerDamaged() => _cancelRequested = true;

        private void ServerTick()
        {
            if (!IsChannelling)
                return;

            NetworkObject raider = _channeller;
            bool gone = raider == null || !raider.IsSpawned;
            if (gone || _cancelRequested
                || LayerMember.ServerLayerOf(raider) != LayerMember.ServerLayerOf(this)
                || PlanarDistance(raider.transform.position) > ChannelRadius)
            {
                ClearChannel();
                return;
            }

            if (base.TimeManager.Tick < _channelEndTick.Value)
                return;

            HatchKeys keys = raider.GetComponent<HatchKeys>();
            ClearChannel();
            if (keys == null || !keys.ServerTryConsume())
                return;

            ServerChannelCompleted?.Invoke(raider, keys.Count);

            ExtractionService extraction = ExtractionService.Of(base.NetworkManager);
            if (extraction == null)
            {
                Debug.LogError("Hatch completed with no ExtractionService beside the NetworkManager.", this);
                return;
            }

            extraction.ServerExtract(raider, ExtractionRoute.Hatch);
        }

        private void ClearChannel()
        {
            if (_channellerHealth != null)
                _channellerHealth.Damaged -= OnChannellerDamaged;
            _channeller = null;
            _channellerHealth = null;
            _cancelRequested = false;
            if (IsServerInitialized)
                _channellerId.Value = -1;
        }

        private float PlanarDistance(Vector3 position)
        {
            Vector3 offset = transform.position - position;
            if (Mathf.Abs(offset.y) > UseHeightTolerance)
                return float.PositiveInfinity;
            offset.y = 0f;
            return offset.magnitude;
        }

        /// <summary>The nearest Hatch within <paramref name="radius"/> belonging to <paramref name="networkManager"/>.</summary>
        public static Hatch FindNearest(Vector3 position, NetworkManager networkManager, float radius)
        {
            Hatch best = null;
            float bestDistance = radius;
            for (int i = 0; i < _active.Count; i++)
            {
                Hatch hatch = _active[i];
                if (hatch == null || hatch.NetworkManager != networkManager)
                    continue;
                float distance = hatch.PlanarDistance(position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = hatch;
                }
            }

            return best;
        }
    }
}
