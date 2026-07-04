using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-authoritative Raider movement per ADR-0005: the owner sends its
    /// intent every tick, the server simulates via CharacterController and sets
    /// facing from the owner's aim point, and NetworkTransform replicates the
    /// result to everyone (owner included); ADR-0001 keeps facing fully
    /// decoupled from motion.
    /// </summary>
    public sealed class RaiderMovement : NetworkBehaviour
    {
        public const float MoveSpeed = 5f;

        private const float Gravity = -9.81f;
        private const float GroundedGravity = -1f;
        private const float AimEpsilonSqr = 1e-4f;

        private CharacterController _controller;
        private IRaiderIntentProvider _intentProvider;

        /* AimPoint starts as the NaN sentinel, not default(Vector3): zero is a
         * VALID world point and would snap every freshly spawned Raider to face
         * world origin on its first server tick, discarding the spawn rotation
         * before the owner's first intent can possibly arrive. */
        private RaiderIntent _lastReceivedIntent = new RaiderIntent { AimPoint = new Vector3(float.NaN, 0f, float.NaN) };

        private float _verticalVelocity;

        /// <summary>
        /// Seam for tests and alternate control schemes; overrides the
        /// component lookup in the tick handler.
        /// </summary>
        public void SetIntentProvider(IRaiderIntentProvider provider) => _intentProvider = provider;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public override void OnStartNetwork()
        {
            /* IsOwner is forbidden here ([PreventUsageInside]); branch inside
             * the tick handler instead. */
            base.TimeManager.OnTick += TimeManager_OnTick;
        }

        public override void OnStopNetwork()
        {
            if (base.TimeManager != null)
                base.TimeManager.OnTick -= TimeManager_OnTick;
        }

        private void TimeManager_OnTick()
        {
            // On a host both branches run in the same handler; that is correct.
            if (IsOwner)
            {
                _intentProvider ??= GetComponent<IRaiderIntentProvider>();
                if (_intentProvider != null)
                    ServerRpcSendIntent(_intentProvider.GetIntent());
            }

            if (IsServerInitialized)
            {
                /* Facing before motion for readability only: motion is world-space
                 * (ADR-0001 decoupling), and NetworkTransform samples at tick end,
                 * so the order is immaterial. */
                ApplyFacingOnServer();
                MoveOnServer((float)base.TimeManager.TickDelta);
            }
        }

        /* RequireOwnership defaults to true, so non-owner sends are dropped
         * server-side. The trailing Channel parameter selects unreliable
         * (unordered): drops or reordering cost at most a tick or two of stale
         * intent because intents persist and resend every tick. */
        [ServerRpc]
        private void ServerRpcSendIntent(RaiderIntent intent, Channel channel = Channel.Unreliable)
        {
            _lastReceivedIntent = intent;
        }

        private void ApplyFacingOnServer()
        {
            /* ADR-0005: never trust the client. A non-finite AimPoint has no safe
             * neutral substitute (zero is a real world point), so the discard action
             * is "keep current facing"; a fresh intent arrives next tick anyway. */
            Vector3 aim = _lastReceivedIntent.AimPoint;
            if (!float.IsFinite(aim.x) || !float.IsFinite(aim.z))
                return;

            Vector3 flatDirection = aim - transform.position;
            flatDirection.y = 0f;

            /* The finite check on the squared magnitude subsumes overflow: components
             * near float range square to Infinity, which would pass a bare epsilon
             * comparison. Huge-but-representable points are fine - LookRotation
             * normalizes, so no distance clamp is needed (nothing consumes aim
             * distance yet). The epsilon also keeps LookRotation's zero-vector
             * warning out of the console when aiming within ~1cm of own feet. */
            float sqrMagnitude = flatDirection.sqrMagnitude;
            if (!float.IsFinite(sqrMagnitude) || sqrMagnitude < AimEpsilonSqr)
                return;

            // Root rotation: NetworkTransform (rotation sync on by default) replicates it.
            transform.rotation = Quaternion.LookRotation(flatDirection, Vector3.up);
        }

        private void MoveOnServer(float delta)
        {
            /* NetworkTransform's CharacterController configuration owns
             * cc.enabled — it is enabled only server-side. */
            if (_controller == null || !_controller.enabled)
                return;

            /* The server never trusts client-supplied values: non-finite input
             * is discarded (NaN passes a "> 1f" check) and magnitude is clamped. */
            Vector2 move = _lastReceivedIntent.Move;
            if (!float.IsFinite(move.x) || !float.IsFinite(move.y))
                move = Vector2.zero;
            else if (move.sqrMagnitude > 1f)
                move.Normalize();

            _verticalVelocity = _controller.isGrounded
                ? GroundedGravity
                : _verticalVelocity + Gravity * delta;

            Vector3 motion = new Vector3(move.x, 0f, move.y) * (MoveSpeed * delta);
            motion.y = _verticalVelocity * delta;
            _controller.Move(motion);
        }
    }
}
