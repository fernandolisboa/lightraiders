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

            return new RaiderIntent
            {
                Move = _moveAction.ReadValue<Vector2>(),
                /* AimPoint/FirePressed stay default until issues #5/#6; the Look
                 * action is a relative pointer delta, unsuitable for an absolute
                 * aim point. */
            };
        }

        private void OnDestroy()
        {
            _moveAction?.Disable();
        }
    }
}
