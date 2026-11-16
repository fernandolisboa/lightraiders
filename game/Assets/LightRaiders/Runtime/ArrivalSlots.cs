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
    /// </summary>
    public static class ArrivalSlots
    {
        /// <summary>Spacing between slots: a capsule diameter plus slack.</summary>
        public const float Spacing = 1.2f;
        private const float GroundClearance = 0.05f;

        /* Offsets in the exit's frame (x = right, y = forward). Never back toward
         * the source, so no slot comes closer to it than the exit point itself. */
        private static readonly Vector2[] Offsets =
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-1f, 1f),
        };

        public static Vector3 Find(Vector3 exit, Vector3 right, Vector3 forward, float capsuleRadius, float capsuleHeight)
        {
            float bottom = capsuleRadius + GroundClearance;
            float top = Mathf.Max(bottom, capsuleHeight - capsuleRadius);
            for (int i = 0; i < Offsets.Length; i++)
            {
                Vector3 candidate = exit + (right * Offsets[i].x + forward * Offsets[i].y) * Spacing;
                if (!Physics.CheckCapsule(candidate + Vector3.up * bottom, candidate + Vector3.up * top, capsuleRadius,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    return candidate;
            }

            return exit;
        }
    }
}
