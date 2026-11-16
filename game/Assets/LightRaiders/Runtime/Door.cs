using System.Collections.Generic;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// One side of a doorway between two Layers (CONTEXT.md "Layer", ADR-0004).
    /// A static, non-networked scene object - identical on every peer because the
    /// scene is - so the owner and the server resolve the same door inside the
    /// predicted replicate (RaiderMovement). Using a door teleports the Raider to
    /// the LINKED door's exit point and moves it into the linked door's Layer.
    /// Doors never lock: players can always follow players.
    /// </summary>
    public sealed class Door : MonoBehaviour
    {
        /// <summary>Planar reach for using a door (PRD #31 tuning).</summary>
        public const float UseRadius = 1.5f;
        /// <summary>Vertical slack: a Raider's root sits at the floor, the door marker may not.</summary>
        public const float UseHeightTolerance = 2f;
        /// <summary>Default exit offset in front of a door - beyond UseRadius so arriving never re-triggers.</summary>
        public const float DefaultExitDistance = 2.5f;

        private static readonly List<Door> _all = new List<Door>();

        [SerializeField]
        private int _layerId;
        [SerializeField]
        private Door _linked;
        [SerializeField]
        private Transform _exitPoint;

        public int LayerId => _layerId;
        public Door Linked => _linked;

        /// <summary>Where a Raider arrives when coming through the linked door into this door's Layer.</summary>
        public Vector3 ExitPosition => _exitPoint != null
            ? _exitPoint.position
            : transform.position + transform.forward * DefaultExitDistance;

        public static IReadOnlyList<Door> All => _all;

        public void Configure(int layerId, Door linked, Transform exitPoint)
        {
            _layerId = layerId;
            _linked = linked;
            _exitPoint = exitPoint;
        }

        /// <summary>Links two doors both ways.</summary>
        public static void Link(Door a, Door b)
        {
            a._linked = b;
            b._linked = a;
        }

        /// <summary>Spacing between arrival slots: a capsule diameter plus slack.</summary>
        public const float ArrivalSlotSpacing = 1.2f;
        private const float ArrivalGroundClearance = 0.05f;

        /* Slot offsets in the door's frame (x = right, y = forward from the exit
         * point). Never back toward the door, so no slot comes closer to it than
         * the exit point itself. */
        private static readonly Vector2[] ArrivalSlots =
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-1f, 1f),
        };

        /// <summary>
        /// Where a Raider arriving through the linked door lands: the exit point,
        /// or the first free slot around it when someone already stands there, so
        /// a follower never spawns inside the player ahead. Physics-only and
        /// deterministic for a given world state; if every slot is taken the exit
        /// point is used anyway. The owner cannot see Raiders in the destination
        /// Layer before arriving, so it may predict the plain exit point where the
        /// server picks a slot; the next reconcile corrects that small offset.
        /// </summary>
        public Vector3 FindArrivalPosition(float capsuleRadius, float capsuleHeight)
        {
            Vector3 exit = ExitPosition;
            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            float bottom = capsuleRadius + ArrivalGroundClearance;
            float top = Mathf.Max(bottom, capsuleHeight - capsuleRadius);
            for (int i = 0; i < ArrivalSlots.Length; i++)
            {
                Vector3 candidate = exit + (right * ArrivalSlots[i].x + forward * ArrivalSlots[i].y) * ArrivalSlotSpacing;
                if (!Physics.CheckCapsule(candidate + Vector3.up * bottom, candidate + Vector3.up * top, capsuleRadius,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    return candidate;
            }

            return exit;
        }

        /// <summary>
        /// The nearest linked door within reach of <paramref name="position"/>, or
        /// null. Deterministic for a given scene and position, which prediction needs.
        /// </summary>
        public static Door FindUsable(Vector3 position)
        {
            Door best = null;
            float bestSqr = UseRadius * UseRadius;
            for (int i = 0; i < _all.Count; i++)
            {
                Door door = _all[i];
                if (door == null || door._linked == null)
                    continue;

                Vector3 offset = door.transform.position - position;
                if (Mathf.Abs(offset.y) > UseHeightTolerance)
                    continue;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = door;
                }
            }

            return best;
        }

        private void OnEnable() => _all.Add(this);

        private void OnDisable() => _all.Remove(this);
    }
}
