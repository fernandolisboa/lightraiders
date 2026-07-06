using FishNet.Managing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LightRaiders
{
    /// <summary>
    /// Debug-only harness for the #20 / ADR-0007 prediction feel-test: drives
    /// FishNet's framework-level <c>TransportManager.LatencySimulator</c> so the
    /// with/without-prediction difference is actually feelable. MPPM three-window
    /// runs sit at ~0ms on one machine, where server-authoritative movement
    /// already feels immediate to the owner — this adds the RTT that exposes CSP's
    /// benefit (or its absence).
    ///
    /// Present in every window (it lives in the shared scene), so each Network
    /// manager simulates its own outgoing latency; <c>_simulateHost</c> stays true
    /// so the main-editor owner feels its OWN round-trip too — the whole point of
    /// the comparison. A plain MonoBehaviour: never networked, never shipped past
    /// the graybox. Keys use the new Input System device directly (no action asset).
    /// </summary>
    public sealed class LatencyToggle : MonoBehaviour
    {
        // Per-window keys. F9 toggles; F10/F11 step latency by StepMs.
        private const long StepMs = 20;
        private const long MaxMs = 1000;

        [SerializeField]
        private NetworkManager _networkManager;

        [Tooltip("One-way latency added per packet (ms). Host doubles it, so this reads roughly as half the felt RTT on the main editor.")]
        [SerializeField]
        private long _latencyMs = 100;

        [Tooltip("Start with simulation already on. Leave off to A/B from a clean 0ms baseline with F9.")]
        [SerializeField]
        private bool _enabledOnStart = false;

        private bool _enabled;

        /// <summary>
        /// Seam mirroring RaiderCameraRig.SetNetworkManager: the scene generator
        /// wires the session manager at authoring time; tests could inject one.
        /// </summary>
        public void SetNetworkManager(NetworkManager networkManager) => _networkManager = networkManager;

        private void Start()
        {
            _enabled = _enabledOnStart;
            Apply();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                _enabled = !_enabled;
                Apply();
            }
            else if (keyboard.f10Key.wasPressedThisFrame)
            {
                _latencyMs = Mathf.Max(0, (int)(_latencyMs - StepMs));
                Apply();
            }
            else if (keyboard.f11Key.wasPressedThisFrame)
            {
                _latencyMs = System.Math.Min(MaxMs, _latencyMs + StepMs);
                Apply();
            }
        }

        private void Apply()
        {
            if (_networkManager == null)
                return;

            var simulator = _networkManager.TransportManager.LatencySimulator;
            simulator.SetLatency(_latencyMs);
            simulator.SetEnabled(_enabled);

            Debug.Log(_enabled
                ? $"[LatencyToggle] Simulation ON — {_latencyMs}ms one-way (host doubles → ~{_latencyMs * 2}ms felt RTT). F9 off, F10/F11 -/+{StepMs}ms."
                : "[LatencyToggle] Simulation OFF (0ms). F9 on, F10/F11 -/+ latency.");
        }

        private void OnGUI()
        {
            string label = _enabled
                ? $"Latency sim: ON  {_latencyMs}ms one-way"
                : "Latency sim: OFF";
            GUI.color = _enabled ? Color.yellow : Color.white;
            GUI.Label(new Rect(10f, 10f, 360f, 24f), label + "   [F9 toggle  F10/F11 -/+]");
        }
    }
}
