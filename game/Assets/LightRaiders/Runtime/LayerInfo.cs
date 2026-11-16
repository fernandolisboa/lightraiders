using System.Collections.Generic;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>Interior size (CONTEXT.md "Layer"): AI enters Large interiors only. Data in Phase 2; enforced in Phase 3.</summary>
    public enum LayerSize
    {
        Open = 0,   // the Surface and the Undercroft - not an interior
        Small = 1,
        Large = 2,
    }

    /// <summary>
    /// Static, non-networked description of one Layer in a map scene: its id,
    /// display name (HUD) and size. Generated into map scenes; identical on every
    /// peer because the scene is. Looked up by id through <see cref="TryGet"/>.
    /// </summary>
    public sealed class LayerInfo : MonoBehaviour
    {
        private static readonly Dictionary<int, LayerInfo> _byId = new Dictionary<int, LayerInfo>();

        [SerializeField]
        private int _layerId;
        [SerializeField]
        private string _displayName = "Surface";
        [SerializeField]
        private LayerSize _size = LayerSize.Open;

        public int LayerId => _layerId;
        public string DisplayName => _displayName;
        public LayerSize Size => _size;

        public void Configure(int layerId, string displayName, LayerSize size)
        {
            /* Play mode only: edit-time generation must not fill the static registry. */
            bool registered = Application.isPlaying && isActiveAndEnabled;
            if (registered)
                OnDisable();
            _layerId = layerId;
            _displayName = displayName;
            _size = size;
            if (registered)
                OnEnable();
        }

        public static bool TryGet(int layerId, out LayerInfo info) => _byId.TryGetValue(layerId, out info) && info != null;

        /// <summary>Display name for a Layer id, falling back to "Layer N" when no LayerInfo is loaded.</summary>
        public static string NameOf(int layerId)
            => TryGet(layerId, out LayerInfo info) ? info.DisplayName : (layerId == LayerMember.Surface ? "Surface" : "Layer " + layerId);

        private void OnEnable() => _byId[_layerId] = this;

        private void OnDisable()
        {
            if (_byId.TryGetValue(_layerId, out LayerInfo current) && current == this)
                _byId.Remove(_layerId);
        }
    }
}
