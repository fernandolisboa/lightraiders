using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Shared landing rule for every instant relocation (Doors, Waygates): the
    /// exit point, or the first free slot around it when someone already stands
    /// there, so a follower never lands inside the player ahead. Physics-only and
    /// deterministic for a given world state; if every slot is taken the exit
    /// point is used anyway. The owner cannot see Raiders in the destination Layer
    /// before arriving, so it may predict the plain exit point where the server
    /// picks a slot; the next reconcile corrects that small offset.
    /// Only colliders of the mover's own NetworkManager (or non-networked
    /// geometry) block a slot: tests run several managers in one physics scene,
    /// where the owner's copy of the mover - already teleported by prediction -
    /// must not count as "someone standing there" on the server.
    /// </summary>
    public static class ArrivalSlots
    {
        /// <summary>Spacing between slots: a capsule diameter plus slack.</summary>
        public const float Spacing = 1.2f;
        private const float GroundClearance = 0.05f;
        private static readonly Collider[] _overlaps = new Collider[16];

        /* Offsets in the exit's frame (x = right, y = forward). Never back toward
         * the source, so no slot comes closer to it than the exit point itself. */
        private static readonly Vector2[] Offsets =
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-1f, 1f),
        };

        public static Vector3 Find(Vector3 exit, Vector3 right, Vector3 forward, float capsuleRadius, float capsuleHeight, NetworkObject mover)
        {
            float bottom = capsuleRadius + GroundClearance;
            float top = Mathf.Max(bottom, capsuleHeight - capsuleRadius);
            for (int i = 0; i < Offsets.Length; i++)
            {
                Vector3 candidate = exit + (right * Offsets[i].x + forward * Offsets[i].y) * Spacing;
                if (!Blocked(candidate + Vector3.up * bottom, candidate + Vector3.up * top, capsuleRadius, mover))
                    return candidate;
            }

            return exit;
        }

        private static bool Blocked(Vector3 a, Vector3 b, float radius, NetworkObject mover)
        {
            int count = Physics.OverlapCapsuleNonAlloc(a, b, radius, _overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = _overlaps[i];
                NetworkObject owner = hit != null ? hit.GetComponentInParent<NetworkObject>() : null;
                if (owner == null)
                    return true; // static geometry
                if (owner == mover)
                    continue;
                if (mover != null && owner.NetworkManager != mover.NetworkManager)
                    continue; // another in-process peer's copy (test harness)
                return true;
            }

            return false;
        }
    }
}
