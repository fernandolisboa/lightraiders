using FishNet.Managing;
using FishNet.Object;
using UnityEngine;
using UnityEngine.UI;

namespace LightRaiders
{
    /// <summary>
    /// Per-client screen-space Shield/Health readout for the locally owned Raider.
    /// Scans its NetworkManager's client object view for the owned Raider every
    /// frame, reads that Raider's replicated Health, and drives two Filled Image
    /// bars from it. Deliberately a plain MonoBehaviour in the RaiderCameraRig
    /// mould: the HUD is client-local, never networked, and reads ONLY replicated
    /// Health state (ADR-0005) - no local prediction or client-side bookkeeping.
    ///
    /// Binds generically to whatever Health the owned Raider carries; BoundHealth
    /// is null (and both bars read empty without erroring) only while no Raider is
    /// owned yet - e.g. before the local Raider has spawned.
    /// </summary>
    public sealed class RaiderHud : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager _networkManager;

        /* The FILL Images (Image.Type.Filled, Horizontal): the generator wires the
         * two authored bars here. Optional so a PlayMode test can exercise the
         * binding with no Canvas - the fill fractions are computed regardless and
         * only pushed to an Image when one is present. */
        [SerializeField]
        private Image _shieldBar;
        [SerializeField]
        private Image _healthBar;

        private Health _health;

        /// <summary>
        /// True while the owned Raider is present in the client view, whether or not
        /// it carries a Health - acquisition is tracked separately from binding.
        /// </summary>
        public bool HasOwnedRaider { get; private set; }

        /// <summary>The owned Raider's Health, or null while no Raider is owned yet.</summary>
        public Health BoundHealth => _health;

        /// <summary>Shield fill fraction last applied to the bar (0 when unbound). Set without rendering, so headless tests can read it.</summary>
        public float ShieldFill { get; private set; }

        /// <summary>Health fill fraction last applied to the bar (0 when unbound).</summary>
        public float HealthFill { get; private set; }

        /// <summary>
        /// Seam mirroring RaiderCameraRig.SetNetworkManager: the HUD generator wires
        /// the session manager at authoring time; PlayMode tests inject a per-client
        /// manager because several coexist in-process.
        /// </summary>
        public void SetNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            _health = null;
            HasOwnedRaider = false;
        }

        /// <summary>Authoring seam: the generator hands the two Filled fill Images here.</summary>
        public void SetBars(Image shieldBar, Image healthBar)
        {
            _shieldBar = shieldBar;
            _healthBar = healthBar;
        }

        private void LateUpdate()
        {
            // LateUpdate: consistent with RaiderCameraRig, after the frame's replication.
            _health = FindOwnedRaiderHealth(out bool foundRaider);
            HasOwnedRaider = foundRaider;

            /* Fill straight from the replicated SyncVars. When there is no bound
             * Health (no owned Raider, or a Raider without Health this slice) both
             * bars read empty rather than throwing. */
            ShieldFill = _health != null ? Mathf.Clamp01((float)_health.Shield / Health.MaxShield) : 0f;
            HealthFill = _health != null ? Mathf.Clamp01((float)_health.CurrentHealth / Health.MaxHealth) : 0f;

            if (_shieldBar != null)
                _shieldBar.fillAmount = ShieldFill;
            if (_healthBar != null)
                _healthBar.fillAmount = HealthFill;
        }

        private Health FindOwnedRaiderHealth(out bool foundRaider)
        {
            foundRaider = false;

            /* Same guards as RaiderCameraRig: the Unity-null check covers a destroyed
             * manager (test teardown), Initialized covers a not-yet-awoken one whose
             * ClientManager is still null; an unstarted manager is silent because its
             * Spawned view is empty. */
            if (_networkManager == null || !_networkManager.Initialized)
                return null;

            foreach (NetworkObject networkObject in _networkManager.ClientManager.Objects.Spawned.Values)
            {
                if (networkObject != null && networkObject.IsOwner && networkObject.GetComponent<Raider>() != null)
                {
                    foundRaider = true;
                    return networkObject.GetComponent<Health>();
                }
            }

            return null;
        }
    }
}
