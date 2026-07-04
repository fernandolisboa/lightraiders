using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Server-authoritative Raider movement per ADR-0005: the owner sends its
    /// intent every tick, the server simulates via CharacterController, and
    /// NetworkTransform replicates the result to everyone (owner included).
    /// </summary>
    public sealed class RaiderMovement : NetworkBehaviour
    {
        public const float MoveSpeed = 5f;

        private const float Gravity = -9.81f;
        private const float GroundedGravity = -1f;

        private CharacterController _controller;
        private IRaiderIntentProvider _intentProvider;
        private RaiderIntent _lastReceivedIntent;
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
                MoveOnServer((float)base.TimeManager.TickDelta);
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
