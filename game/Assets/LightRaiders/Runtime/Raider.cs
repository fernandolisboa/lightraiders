using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Marks the Raider stand-in capsule so tests and future gameplay code can identify it.
    /// </summary>
    public sealed class Raider : MonoBehaviour
    {
        [Tooltip("Smoothed graphical child (NetworkTickSmoother). The root moves only on ticks; follow this for per-frame visuals.")]
        [SerializeField]
        private Transform _graphics;

        /// <summary>
        /// Transform to follow for anything visual (camera, world-space bars).
        /// Under client-side prediction (ADR-0007) the root moves in tick steps
        /// and snaps on reconcile; the graphical child is smoothed per frame.
        /// Falls back to the root on prefabs generated before the child existed.
        /// </summary>
        public Transform VisualTransform => _graphics != null ? _graphics : transform;
    }
}
