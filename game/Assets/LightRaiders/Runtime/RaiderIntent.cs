using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Planar intent per ADR-0001: Move.x maps to world X, Move.y to world Z.
    /// FirePressed is level-state: true on every tick the fire control is held;
    /// the server's per-Raider cooldown (RaiderWeapon) owns the fire rate.
    /// Interact is level-state too; RaiderMovement's interact cooldown owns its rate.
    /// </summary>
    public struct RaiderIntent
    {
        // Public FIELDS only: FishNet codegen serializes public fields of RPC structs; do not convert to properties.
        public Vector2 Move;
        public Vector3 AimPoint;
        public bool FirePressed;
        /// <summary>Level-state "use" (doors, and later Exfil/Hatch/Waygate): true on every tick the control is held.</summary>
        public bool Interact;
    }
}
