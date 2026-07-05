using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Per-client manager for the small world-space bar floating over every
    /// damageable in view. Each frame it reconciles a live set of bars against the
    /// Health-carrying objects in its NetworkManager's client view: it spawns a bar
    /// when a new damageable appears, updates each bar's fill from the object's
    /// replicated Shield+Health, and destroys a bar the instant its object leaves
    /// the view - so a despawned target (death) leaves no orphan and a respawn gets
    /// a fresh full bar. Client-local and never networked (same shape as
    /// RaiderCameraRig); reads ONLY replicated Health (ADR-0005).
    ///
    /// Bars are built DYNAMICALLY in code (not baked into the damageable prefabs, so
    /// #17's prefab work never collides with them). A bar is two billboarded graybox
    /// quads: a full-width background and a left-anchored fill scaled by the fraction.
    /// The single fill fraction is (Shield + Health) / (MaxShield + MaxHealth), so
    /// the very first Shield hit is already visible.
    /// </summary>
    public sealed class WorldSpaceHealthBars : MonoBehaviour
    {
        // Above the ~2-tall target/Raider capsule (root at floor level), clear of the head.
        public const float BarHeightOffset = 2.3f;
        private const float BarWidth = 1.2f;
        private const float BarThickness = 0.16f;
        // Fill sits a hair in front of the background (toward -Z after billboarding) to avoid z-fighting.
        private const float FillDepthBias = -0.01f;

        [SerializeField]
        private NetworkManager _networkManager;
        [SerializeField]
        private Material _barBackgroundMaterial;
        [SerializeField]
        private Material _barFillMaterial;

        // ObjectId -> its live bar. ObjectId is the shared network id, identical across views.
        private readonly Dictionary<int, WorldBar> _bars = new Dictionary<int, WorldBar>();
        private readonly List<int> _removeBuffer = new List<int>();

        /// <summary>Live bar count - a test seam for orphan-free reconciliation.</summary>
        public int BarCount => _bars.Count;

        /// <summary>
        /// Seam mirroring RaiderCameraRig.SetNetworkManager: the HUD generator wires
        /// the session manager at authoring time; PlayMode tests inject a per-client
        /// manager because several coexist in-process.
        /// </summary>
        public void SetNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            ClearAllBars();
        }

        /// <summary>Authoring seam: the generator supplies the graybox bar materials.</summary>
        public void SetBarMaterials(Material background, Material fill)
        {
            _barBackgroundMaterial = background;
            _barFillMaterial = fill;
        }

        /// <summary>True while a bar exists for the given ObjectId.</summary>
        public bool HasBar(int objectId) => _bars.ContainsKey(objectId);

        /// <summary>
        /// The fill fraction of the bar over the given ObjectId; false when no bar
        /// exists. Set without rendering, so headless tests can assert the bar
        /// reflects the object's replicated Health.
        /// </summary>
        public bool TryGetFill(int objectId, out float fill)
        {
            if (_bars.TryGetValue(objectId, out WorldBar bar))
            {
                fill = bar.Fill;
                return true;
            }

            fill = 0f;
            return false;
        }

        private void LateUpdate()
        {
            // LateUpdate: after NetworkTransform has moved the tracked objects this frame.
            if (_networkManager == null || !_networkManager.Initialized)
            {
                ClearAllBars();
                return;
            }

            Reconcile(_networkManager.ClientManager.Objects.Spawned);
        }

        private void OnDestroy() => ClearAllBars();

        private void Reconcile(IReadOnlyDictionary<int, NetworkObject> spawned)
        {
            // 1. Ensure and update a bar for every damageable currently in view.
            foreach (NetworkObject networkObject in spawned.Values)
            {
                if (networkObject == null)
                    continue;

                Health health = networkObject.GetComponent<Health>();
                if (health == null)
                    continue;

                int objectId = networkObject.ObjectId;
                if (!_bars.TryGetValue(objectId, out WorldBar bar))
                {
                    bar = CreateBar();
                    _bars[objectId] = bar;
                }

                bar.Health = health;
                bar.Target = networkObject.transform;
                UpdateBar(bar);
            }

            /* 2. Orphan sweep: destroy a bar whose object left the view. A despawn
             * removes the ObjectId from the spawned dictionary (and destroys the
             * GameObject, so Health goes Unity-null) - both are caught here, leaving
             * no bar floating over nothing. */
            _removeBuffer.Clear();
            foreach (KeyValuePair<int, WorldBar> entry in _bars)
            {
                if (entry.Value.Health == null || !spawned.ContainsKey(entry.Key))
                    _removeBuffer.Add(entry.Key);
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                int objectId = _removeBuffer[i];
                DestroyBar(_bars[objectId]);
                _bars.Remove(objectId);
            }
        }

        private void UpdateBar(WorldBar bar)
        {
            float fraction = Mathf.Clamp01(
                (float)(bar.Health.Shield + bar.Health.CurrentHealth) / (Health.MaxShield + Health.MaxHealth));
            bar.Fill = fraction;

            // Left-anchored fill: shrink from the right by scaling and re-centering on X.
            bar.FillTransform.localScale = new Vector3(BarWidth * fraction, BarThickness, 1f);
            bar.FillTransform.localPosition = new Vector3(-0.5f * BarWidth + 0.5f * BarWidth * fraction, 0f, FillDepthBias);

            Vector3 position = bar.Target.position + Vector3.up * BarHeightOffset;

            /* Billboard: align the bar with the camera plane so it always reads
             * face-on. No camera in headless tests, so fall back to a plain
             * position update - the fill seam above still holds. */
            Camera camera = Camera.main;
            if (camera != null)
                bar.Root.transform.SetPositionAndRotation(position, camera.transform.rotation);
            else
                bar.Root.transform.position = position;
        }

        private WorldBar CreateBar()
        {
            GameObject root = new GameObject("WorldHealthBar");
            root.transform.SetParent(transform, false);

            GameObject background = BuildQuad("Background", root.transform, _barBackgroundMaterial);
            background.transform.localScale = new Vector3(BarWidth, BarThickness, 1f);

            GameObject fill = BuildQuad("Fill", root.transform, _barFillMaterial);

            return new WorldBar
            {
                Root = root,
                FillTransform = fill.transform,
            };
        }

        private static GameObject BuildQuad(string name, Transform parent, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;

            /* A Quad primitive ships a MeshCollider; strip it so a bar floating over
             * a target never intercepts the projectile sweep's raycast. */
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            quad.transform.SetParent(parent, false);
            if (material != null)
                quad.GetComponent<MeshRenderer>().sharedMaterial = material;

            return quad;
        }

        private void ClearAllBars()
        {
            foreach (WorldBar bar in _bars.Values)
                DestroyBar(bar);
            _bars.Clear();
        }

        private static void DestroyBar(WorldBar bar)
        {
            if (bar.Root != null)
                Destroy(bar.Root);
        }

        /// <summary>One live bar: its GameObjects plus the object it tracks and the fraction it shows.</summary>
        private sealed class WorldBar
        {
            public GameObject Root;
            public Transform FillTransform;
            public Health Health;
            public Transform Target;
            public float Fill;
        }
    }
}
