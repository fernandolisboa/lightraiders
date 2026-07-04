using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Planar intent per ADR-0001: Move.x maps to world X, Move.y to world Z.
    /// FirePressed is level-state: true on every tick the fire control is held;
    /// the server's per-Raider cooldown (RaiderWeapon) owns the fire rate.
    /// </summary>
    public struct RaiderIntent
    {
        // Public FIELDS only: FishNet codegen serializes public fields of RPC structs; do not convert to properties.
        public Vector2 Move;
        public Vector3 AimPoint;
        public bool FirePressed;
    }
}
