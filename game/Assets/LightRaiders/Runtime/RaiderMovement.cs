using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Client-side-predicted Raider movement (ADR-0007), replacing the Phase-0
    /// server-authoritative RPC shape. The owner runs the movement/facing
    /// simulation locally every tick inside <see cref="PerformReplicate"/> and
    /// the server re-runs the same simulation authoritatively; FishNet reconciles
    /// the owner (and forwards state to spectators) via <see cref="PerformReconcile"/>.
    /// NetworkTransform is gone — reconcile drives the transform.
    ///
    /// Server authority is preserved (ADR-0005): the server's replicate is the
    /// truth a reconcile snaps the owner back to, and fire stays SERVER-ONLY —
    /// <see cref="RaiderIntent.FirePressed"/> rides the ReplicateData but only the
    /// server consumes it, so no projectile is ever client-predicted. ADR-0001
    /// keeps facing decoupled from motion; both are predicted here.
    /// </summary>
    public sealed class RaiderMovement : TickNetworkBehaviour
    {
        public const float MoveSpeed = 5f;

        private const float Gravity = -9.81f;
        private const float GroundedGravity = -1f;
        private const float AimEpsilonSqr = 1e-4f;

        private CharacterController _controller;
        private IRaiderIntentProvider _intentProvider;
        private RaiderWeapon _weapon;

        private float _verticalVelocity;

        /// <summary>
        /// Owner-side input for one tick. FishNet serializes public fields; the
        /// tick field is managed by the framework. Facing (AimPoint) and fire
        /// (FirePressed) ride alongside movement so the single owner→server input
        /// pipe from ADR-0005 is preserved as one Replicate.
        /// </summary>
        public struct ReplicateData : IReplicateData
        {
            public Vector2 Move;
            public Vector3 AimPoint;
            public bool FirePressed;

            private uint _tick;

            public ReplicateData(RaiderIntent intent)
            {
                Move = intent.Move;
                AimPoint = intent.AimPoint;
                FirePressed = intent.FirePressed;
                _tick = 0;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /// <summary>
        /// Authoritative state a reconcile restores. Rotation is included because
        /// facing is predicted (ADR-0001 decoupling) and must be corrected in the
        /// same snap as position.
        /// </summary>
        public struct ReconcileData : IReconcileData
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float VerticalVelocity;

            private uint _tick;

            public ReconcileData(Vector3 position, Quaternion rotation, float verticalVelocity)
            {
                Position = position;
                Rotation = rotation;
                VerticalVelocity = verticalVelocity;
                _tick = 0;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /* AimPoint starts as the NaN sentinel, not default(Vector3): zero is a
         * VALID world point and would snap a freshly spawned Raider to face world
         * origin before the owner's first replicate carries a real aim. Facing
         * discards a non-finite aim by keeping current rotation. */
        private RaiderIntent _neutralIntent = new RaiderIntent { AimPoint = new Vector3(float.NaN, 0f, float.NaN) };

        /// <summary>
        /// Seam for tests and alternate control schemes; overrides the component
        /// lookup in <see cref="BuildMoveData"/>.
        /// </summary>
        public void SetIntentProvider(IRaiderIntentProvider provider) => _intentProvider = provider;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            /* Reconcile (owner correction) plus replicate (all runners) need the CC
             * enabled on every instance — the old NetworkTransform CC-config that
             * disabled it off-server is gone. PostTick builds the reconcile after
             * the tick's movement has run. */
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        protected override void TimeManager_OnTick()
        {
            PerformReplicate(BuildMoveData());
        }

        protected override void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        /// <summary>
        /// Only the owner authors input; the server and spectators receive it
        /// through FishNet's replicate/state pipeline, not from a local provider.
        /// </summary>
        private ReplicateData BuildMoveData()
        {
            if (!IsOwner)
                return default;

            _intentProvider ??= GetComponent<IRaiderIntentProvider>();
            RaiderIntent intent = _intentProvider != null ? _intentProvider.GetIntent() : _neutralIntent;
            return new ReplicateData(intent);
        }

        public override void CreateReconcile()
        {
            /* Both server and client build a reconcile every tick; the client uses
             * its own copy only as a fallback for a dropped server reconcile. We
             * have no platform/trigger dependency, so PostTick timing is purely to
             * capture post-movement state. */
            ReconcileData rd = new ReconcileData(transform.position, transform.rotation, _verticalVelocity);
            PerformReconcile(rd);
        }

        [Replicate]
        private void PerformReplicate(ReplicateData rd, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            float delta = (float)base.TimeManager.TickDelta;

            /* Facing before motion for readability (ADR-0001 decoupling). Runs on
             * owner + server + spectators so aim is observed everywhere. */
            ApplyFacing(rd.AimPoint);
            Move(rd.Move, delta);

            /* Fire is SERVER-ONLY (ADR-0005): the server owns the projectile, the
             * cooldown, and the muzzle origin from the authoritative transform.
             * FirePressed travels in the replicate but is never acted on by the
             * predicting owner/spectator — no client-predicted projectiles. The
             * per-Raider cooldown (RaiderWeapon) makes a repeated server-side call
             * within a tick idempotent. */
            if (IsServerInitialized)
            {
                _weapon ??= GetComponent<RaiderWeapon>();
                if (_weapon != null)
                    _weapon.TryFireOnServer(rd.FirePressed);
            }
        }

        [Reconcile]
        private void PerformReconcile(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            _verticalVelocity = rd.VerticalVelocity;

            /* The CharacterController must be disabled before its Transform is
             * written, or the physics body lags a tick behind the visual position
             * (FishNet CC-prediction requirement). Rotation is safe to set either
             * side of the toggle; grouped here for one atomic correction. */
            _controller.enabled = false;
            transform.SetPositionAndRotation(rd.Position, rd.Rotation);
            _controller.enabled = true;
        }

        private void ApplyFacing(Vector3 aim)
        {
            /* Never trust a non-finite aim (ADR-0005): zero is a real world point
             * so there is no safe neutral substitute; the discard action is "keep
             * current facing". A fresh aim arrives next replicate anyway. */
            if (!float.IsFinite(aim.x) || !float.IsFinite(aim.z))
                return;

            Vector3 flatDirection = aim - transform.position;
            flatDirection.y = 0f;

            /* The finite check on the squared magnitude subsumes overflow: huge
             * components square to Infinity, which would pass a bare epsilon
             * comparison. The epsilon also silences LookRotation's zero-vector
             * warning when aiming within ~1cm of own feet. */
            float sqrMagnitude = flatDirection.sqrMagnitude;
            if (!float.IsFinite(sqrMagnitude) || sqrMagnitude < AimEpsilonSqr)
                return;

            transform.rotation = Quaternion.LookRotation(flatDirection, Vector3.up);
        }

        private void Move(Vector2 move, float delta)
        {
            if (_controller == null || !_controller.enabled)
                return;

            /* Non-finite input is discarded (NaN passes a "> 1f" check) and
             * magnitude is clamped — a hostile owner cannot outrun MoveSpeed. */
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
