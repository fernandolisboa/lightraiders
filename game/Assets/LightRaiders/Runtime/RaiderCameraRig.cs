using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Per-client camera rig for the angled top-down view (ADR-0001). Scans its
    /// NetworkManager's client object view for the locally owned Raider, then
    /// snaps to a fixed offset from it every frame. Deliberately a plain
    /// MonoBehaviour: camera state is client-local and never networked.
    /// </summary>
    public sealed class RaiderCameraRig : MonoBehaviour
    {
        /// <summary>
        /// Fixed pitch in degrees. ADR-0001 commits to "angled top-down" without
        /// a number; 55 sits in the Hades-style 45-60 band. Tunable later.
        /// </summary>
        public const float PitchDegrees = 55f;

        /// <summary>
        /// Rig position relative to the followed Raider root. The Visual capsule
        /// center sits at root+1y; (12 - 1) / 7.7 == tan(55deg), so the view ray
        /// aims at the capsule center.
        /// </summary>
        public static readonly Vector3 FollowOffset = new Vector3(0f, 12f, -7.7f);

        [SerializeField]
        private NetworkManager _networkManager;

        private Transform _target;

        /// <summary>
        /// Seam mirroring RaiderMovement.SetIntentProvider: the scene generator
        /// wires the session manager at authoring time; PlayMode tests inject a
        /// per-client manager because several coexist in-process.
        /// </summary>
        public void SetNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            _target = null;
        }

        private void Awake()
        {
            transform.rotation = Quaternion.Euler(PitchDegrees, 0f, 0f);
        }

        private void LateUpdate()
        {
            // LateUpdate: after NetworkTransform has moved the target this frame.
            if (_target == null)
                _target = FindOwnedRaiderTransform();

            if (_target != null)
                transform.position = _target.position + FollowOffset;
        }

        private Transform FindOwnedRaiderTransform()
        {
            /* The Unity-null check covers a destroyed manager (test teardown
             * destroys managers before scene objects); Initialized covers a
             * not-yet-awoken manager whose ClientManager is still null. An
             * initialized-but-unstarted manager is naturally silent because its
             * Spawned view is empty. */
            if (_networkManager == null || !_networkManager.Initialized)
                return null;

            foreach (NetworkObject networkObject in _networkManager.ClientManager.Objects.Spawned.Values)
            {
                if (networkObject != null && networkObject.IsOwner && networkObject.GetComponent<Raider>() != null)
                    return networkObject.transform;
            }

            return null;
        }
    }
}
