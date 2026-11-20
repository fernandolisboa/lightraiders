using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Observer condition for Layer scoping (ADR-0004, ADR-0008): a connection
    /// observes an object only when both are in the same Layer - players in other
    /// Layers never replicate (the anti-wallhack freebie). Reads the object's
    /// <see cref="LayerMember.ServerLayer"/> and the connection's Layer from the
    /// session <see cref="LayerService"/>. FishNet clones the asset per object.
    /// A missing LayerMember or LayerService degrades to "visible" so a
    /// misconfigured setup shows too much rather than hiding players.
    /// </summary>
    [CreateAssetMenu(menuName = "Light Raiders/Layer Condition", fileName = "LayerCondition")]
    public sealed class LayerCondition : ObserverCondition
    {
        private LayerMember _member;
        private LayerService _service;

        public override void Initialize(FishNet.Object.NetworkObject networkObject)
        {
            base.Initialize(networkObject);
            /* Cached once: ConditionMet runs per object per connection on every
             * rebuild, so a GetComponent there is a hot-path cost. Conditions are
             * evaluated on the server, so the object's manager is the server's. */
            _service = networkObject != null && networkObject.NetworkManager != null
                ? networkObject.NetworkManager.GetComponent<LayerService>()
                : null;
        }

        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;
            if (NetworkObject == null || connection == null)
                return false;

            if (_member == null)
                _member = NetworkObject.GetComponent<LayerMember>();
            if (_member == null)
                return true;

            int connectionLayer = _service != null ? _service.GetConnectionLayer(connection) : LayerMember.Surface;

            return _member.ServerLayer == connectionLayer;
        }

        public override void Deinitialize(bool destroyed)
        {
            _member = null;
            _service = null;
            base.Deinitialize(destroyed);
        }

        public override ObserverConditionType GetConditionType() => ObserverConditionType.Normal;
    }
}
