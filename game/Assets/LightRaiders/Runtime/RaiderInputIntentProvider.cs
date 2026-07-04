using UnityEngine;
using UnityEngine.InputSystem;

namespace LightRaiders
{
    /// <summary>
    /// Reads the local Raider's intent from the project's Input System actions.
    /// Wired to the input action asset by the prefab generator.
    /// </summary>
    public sealed class RaiderInputIntentProvider : MonoBehaviour, IRaiderIntentProvider
    {
        [SerializeField]
        private InputActionAsset _actions;

        private InputAction _moveAction;
        private InputAction _attackAction;
        private Camera _camera;

        public RaiderIntent GetIntent()
        {
            if (_actions == null)
                return default;

            /* Lazy resolution: action maps only activate on instances that
             * actually read input — the owner. */
            if (_moveAction == null)
            {
                _moveAction = _actions.FindAction("Player/Move", throwIfNotFound: true);
                _moveAction.Enable();
            }

            if (_attackAction == null)
            {
                _attackAction = _actions.FindAction("Player/Attack", throwIfNotFound: true);
                _attackAction.Enable();
            }

            return new RaiderIntent
            {
                Move = _moveAction.ReadValue<Vector2>(),
                AimPoint = ComputeAimPoint(),
                /* Level semantics (IsPressed), not WasPressedThisFrame: GetIntent runs on
                 * the network tick, not Update - frame-scoped edge queries drop presses
                 * when tick rate < frame rate and double-report when tick rate > frame
                 * rate. The server cooldown owns the fire rate (PRD: level-state at 30Hz). */
                FirePressed = _attackAction.IsPressed(),
            };
        }

        /* Direct device read instead of an input action: Player/Look is a relative
         * pointer delta (unsuitable for an absolute aim point), and UI/Point belongs
         * to the UI map. Bypasses control schemes/rebinding - acceptable for Phase 0
         * mouse-only aim; a dedicated Player/Aim action can supersede this later. */
        private Vector3 ComputeAimPoint()
        {
            /* Sentinel fallback: the server discards non-finite aim points and keeps
             * the current facing, which is the true neutral even while moving.
             * (transform.position would drift past the server epsilon on the owner's
             * interpolated view; Vector3.zero is a real world point.) */
            Vector3 fallback = new Vector3(float.NaN, 0f, float.NaN);

            if (Mouse.current == null) // headless/batchmode: no mouse device
                return fallback;

            if (_camera == null) // lazy + Unity-null recheck: survives camera recreation
                _camera = Camera.main;
            if (_camera == null)
                return fallback;

            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, 0f);
            return groundPlane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : fallback;
        }

        private void OnDestroy()
        {
            _moveAction?.Disable();
            _attackAction?.Disable();
        }
    }
}
