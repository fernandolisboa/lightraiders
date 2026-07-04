using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Planar intent per ADR-0001: Move.x maps to world X, Move.y to world Z.
    /// AimPoint and FirePressed are carried but unconsumed until issues #5/#6.
    /// </summary>
    public struct RaiderIntent
    {
        // Public FIELDS only: FishNet codegen serializes public fields of RPC structs; do not convert to properties.
        public Vector2 Move;
        public Vector3 AimPoint;
        public bool FirePressed;
    }
}
