using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Planar intent per ADR-0001: Move.x maps to world X, Move.y to world Z.
    /// FirePressed is carried but unconsumed until issue #6.
    /// </summary>
    public struct RaiderIntent
    {
        // Public FIELDS only: FishNet codegen serializes public fields of RPC structs; do not convert to properties.
        public Vector2 Move;
        public Vector3 AimPoint;
        public bool FirePressed;
    }
}
