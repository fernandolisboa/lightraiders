using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace LightRaiders
{
    /// <summary>
    /// The Layer a networked object belongs to (CONTEXT.md "Layer", ADR-0004,
    /// ADR-0008). Server-written: <see cref="ServerLayer"/> is the truth the
    /// <see cref="LayerCondition"/> reads on the server, and a SyncVar mirrors it
    /// to clients (HUD, presentation). Layer 0 is the Surface.
    /// </summary>
    public sealed class LayerMember : NetworkBehaviour
    {
        public const int Surface = 0;

        private readonly SyncVar<int> _layer = new();

        /* Plain field, not the SyncVar: the observer condition runs during
         * ServerManager.Spawn - before OnStartServer - so a Layer stamped by the
         * spawner via ServerSetLayer must be readable before the SyncVar is live. */
        private int _serverLayer = Surface;

        /// <summary>Server-side truth. Only meaningful on the server.</summary>
        public int ServerLayer => _serverLayer;

        /// <summary>The Layer as seen by whoever asks: server truth on the server, replicated value on clients.</summary>
        public int LayerId => IsServerInitialized ? _serverLayer : _layer.Value;

        /// <summary>
        /// Sets the Layer server-side. Safe both before spawn (spawners stamp it
        /// so the first observer evaluation is already correct) and after; does
        /// NOT rebuild observers - movement between Layers goes through
        /// <see cref="LayerService"/>, which does.
        /// </summary>
        public void ServerSetLayer(int layer)
        {
            _serverLayer = layer;
            if (IsServerInitialized)
                _layer.Value = layer;
        }

        /// <summary>Server Layer of the object that owns <paramref name="component"/>; Surface when it has no LayerMember.</summary>
        public static int ServerLayerOf(UnityEngine.Component component)
        {
            LayerMember member = component != null ? component.GetComponent<LayerMember>() : null;
            return member != null ? member.ServerLayer : Surface;
        }

        /// <summary>Stamps a not-yet-spawned object's Layer, if it carries a LayerMember.</summary>
        public static void StampBeforeSpawn(NetworkObject nob, int layer)
        {
            LayerMember member = nob != null ? nob.GetComponent<LayerMember>() : null;
            if (member != null)
                member.ServerSetLayer(layer);
        }

        public override void OnStartServer()
        {
            _layer.Value = _serverLayer;
        }

        public override void OnStopServer()
        {
            // Pooled instances are reused: never let a previous life's Layer leak into the next spawn.
            _serverLayer = Surface;
        }
    }
}
