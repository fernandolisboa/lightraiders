using System.Collections.Generic;
using FishNet.Object;
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

        /// <summary>
        /// Where a Raider arriving through the linked door lands: the exit point,
        /// or the first free slot around it (<see cref="ArrivalSlots"/>).
        /// </summary>
        public Vector3 FindArrivalPosition(float capsuleRadius, float capsuleHeight, NetworkObject mover)
            => ArrivalSlots.Find(ExitPosition, transform.right, transform.forward, capsuleRadius, capsuleHeight, mover);

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
