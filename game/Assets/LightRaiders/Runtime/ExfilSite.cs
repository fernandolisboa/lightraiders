using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace LightRaiders
{
    public enum ExfilState : byte
    {
        Idle = 0,
        Called = 1,
        Departing = 2,
    }

    /// <summary>
    /// An Exfil site (#38): a networked, Layer-scoped point on the Surface running
    /// a server state machine Idle → Called (countdown) → Departing → Idle. Any
    /// Raider interacting in reach of an Idle site calls it; the state and the
    /// departure tick are SyncVars, so every observer in the site's Layer (every
    /// Surface Raider) sees the call. At departure every Raider still inside the
    /// zone, in the site's Layer, extracts through <see cref="ExtractionService"/>.
    /// </summary>
    public sealed class ExfilSite : NetworkBehaviour
    {
        public const float DefaultCountdownSeconds = 45f;
        /// <summary>Extraction zone radius (PRD #31 tuning).</summary>
        public const float ZoneRadius = 8f;
        /// <summary>Planar reach for calling the site.</summary>
        public const float UseRadius = 2f;
        public const float UseHeightTolerance = 2f;
        /// <summary>How long the site shows Departing before it is callable again.</summary>
        public const float DepartingSeconds = 3f;

        private static readonly List<ExfilSite> _active = new List<ExfilSite>();

        private readonly SyncVar<ExfilState> _state = new();
        private readonly SyncVar<uint> _departTick = new();

        [SerializeField]
        private float _countdownSeconds = DefaultCountdownSeconds;

        private uint _idleTick;
        private readonly List<NetworkObject> _departing = new List<NetworkObject>();

        public static IReadOnlyList<ExfilSite> Active => _active;
        public ExfilState State => _state.Value;
        public uint DepartTick => _departTick.Value;

        public float CountdownRemainingSeconds
        {
            get
            {
                if (_state.Value != ExfilState.Called || base.TimeManager == null)
                    return 0f;
                uint tick = base.TimeManager.Tick;
                uint end = _departTick.Value;
                return tick >= end ? 0f : (float)((end - tick) * base.TimeManager.TickDelta);
            }
        }

        /// <summary>Server, tests and tuning: the countdown used by the NEXT call.</summary>
        public void ServerSetCountdownSeconds(float seconds) => _countdownSeconds = Mathf.Max(0f, seconds);

        public override void OnStartNetwork()
        {
            if (!_active.Contains(this))
                _active.Add(this);
        }

        public override void OnStopNetwork() => _active.Remove(this);

        public override void OnStartServer() => base.TimeManager.OnTick += ServerTick;

        public override void OnStopServer()
        {
            if (base.TimeManager != null)
                base.TimeManager.OnTick -= ServerTick;
        }

        /// <summary>Server: calls the site if it is Idle. Calling a busy site does nothing.</summary>
        public bool ServerCall()
        {
            if (!IsServerInitialized || _state.Value != ExfilState.Idle)
                return false;

            _departTick.Value = base.TimeManager.Tick + (uint)Mathf.CeilToInt(_countdownSeconds * base.TimeManager.TickRate);
            _state.Value = ExfilState.Called;
            return true;
        }

        private void ServerTick()
        {
            uint tick = base.TimeManager.Tick;
            if (_state.Value == ExfilState.Called && tick >= _departTick.Value)
            {
                Depart();
                _state.Value = ExfilState.Departing;
                _idleTick = tick + (uint)Mathf.CeilToInt(DepartingSeconds * base.TimeManager.TickRate);
            }
            else if (_state.Value == ExfilState.Departing && tick >= _idleTick)
            {
                _state.Value = ExfilState.Idle;
            }
        }

        private void Depart()
        {
            ExtractionService extraction = ExtractionService.Of(base.NetworkManager);
            if (extraction == null)
            {
                Debug.LogError("ExfilSite departed with no ExtractionService beside the NetworkManager.", this);
                return;
            }

            int layer = LayerMember.ServerLayerOf(this);
            Vector3 centre = transform.position;

            // Collect first: extracting despawns, which mutates the spawned collection.
            _departing.Clear();
            foreach (NetworkObject nob in base.ServerManager.Objects.Spawned.Values)
            {
                if (nob == null || nob.GetComponent<Raider>() == null || LayerMember.ServerLayerOf(nob) != layer)
                    continue;
                Vector3 offset = nob.transform.position - centre;
                if (Mathf.Abs(offset.y) > UseHeightTolerance)
                    continue;
                offset.y = 0f;
                if (offset.sqrMagnitude <= ZoneRadius * ZoneRadius)
                    _departing.Add(nob);
            }

            for (int i = 0; i < _departing.Count; i++)
                extraction.ServerExtract(_departing[i], ExtractionRoute.Exfil);
            _departing.Clear();
        }

        /// <summary>
        /// The nearest site within <paramref name="radius"/> belonging to
        /// <paramref name="networkManager"/> (tests run several managers in-process).
        /// </summary>
        public static ExfilSite FindNearest(Vector3 position, NetworkManager networkManager, float radius)
        {
            ExfilSite best = null;
            float bestSqr = radius * radius;
            for (int i = 0; i < _active.Count; i++)
            {
                ExfilSite site = _active[i];
                if (site == null || site.NetworkManager != networkManager)
                    continue;
                Vector3 offset = site.transform.position - position;
                if (Mathf.Abs(offset.y) > UseHeightTolerance)
                    continue;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = site;
                }
            }

            return best;
        }
    }
}
