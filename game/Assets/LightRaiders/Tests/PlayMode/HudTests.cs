using System.Collections;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LightRaiders.Tests
{
    /// <summary>
    /// PlayMode tests for the graybox HUD (issue #19). The HUD is client-local and
    /// reads ONLY replicated Health, so every assertion is on a CLIENT view: a
    /// world-space bar's fill fraction and the live bar count, and the screen-space
    /// RaiderHud's acquisition of the owned Raider. Damage is applied straight on
    /// the server (ApplyDamageOnServer) to change replicated state directly - the
    /// projectile delivery path is exercised by RaiderCombatTests; here the concern
    /// is purely that a bar reflects replicated Health and never orphans.
    /// </summary>
    [TestFixture]
    public class HudTests
    {
        /* Duplicated from SessionAssetPaths (same rule as NetworkSessionHarness):
         * this assembly cannot reference the editor-only LightRaiders.Editor asmdef. */
        private const string DummyTargetPrefabPath = "Assets/LightRaiders/Prefabs/DummyTarget.prefab";

        private NetworkSessionHarness _harness;
        private List<GameObject> _sceneObjects;
        private Transform[] _spawns;

        [SetUp]
        public void SetUp()
        {
            _harness = new NetworkSessionHarness();
            _sceneObjects = new List<GameObject>();

            // Top of the floor sits at y=0.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);
            _sceneObjects.Add(floor);

            GameObject spawnA = new GameObject("TestSpawnA");
            spawnA.transform.position = new Vector3(10f, 1f, 10f);
            _sceneObjects.Add(spawnA);
            GameObject spawnB = new GameObject("TestSpawnB");
            spawnB.transform.position = new Vector3(-10f, 1f, -10f);
            _sceneObjects.Add(spawnB);
            _spawns = new Transform[] { spawnA.transform, spawnB.transform };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _harness.Teardown();

            foreach (GameObject sceneObject in _sceneObjects)
            {
                if (sceneObject != null)
                    Object.Destroy(sceneObject);
            }

            _sceneObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldBar_AppearsAndFillReflectsReplicatedHealth()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: false);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager client = _harness.CreateNetworkManager(withSpawner: false);
            WorldSpaceHealthBars bars = AttachWorldBars(client, "WorldBarsA");
            client.ClientManager.StartConnection();

            NetworkObject serverTarget = SpawnTargetAt(server, new Vector3(0f, 0f, 10f));
            Health serverHealth = serverTarget.GetComponent<Health>();
            int objectId = serverTarget.ObjectId;

            // A bar appears over the damageable and starts full (anti-vacuity for the drop below).
            yield return _harness.WaitUntil(
                () => bars.BarCount == 1 && bars.TryGetFill(objectId, out float f) && f > 0.999f,
                "World bar never appeared at full fill over the target.",
                15f);

            // Deplete the whole Shield and part of the Health on the server.
            const int damage = Health.MaxShield + 20;
            serverHealth.ApplyDamageOnServer(damage);
            Assert.That(serverHealth.Shield, Is.EqualTo(0), "Precondition: server Shield should be gone.");
            Assert.That(serverHealth.CurrentHealth, Is.EqualTo(Health.MaxHealth - 20), "Precondition: server Health should have dropped by the overflow.");

            // The client-view bar drops to the replicated combined fraction.
            float expected = (float)(0 + (Health.MaxHealth - 20)) / (Health.MaxShield + Health.MaxHealth);
            yield return _harness.WaitUntil(
                () => bars.TryGetFill(objectId, out float f) && Mathf.Abs(f - expected) < 0.01f,
                "World bar never reflected the target's depleted replicated Health.",
                15f);

            // Still exactly one bar - no phantom duplicate for the same object.
            Assert.That(bars.BarCount, Is.EqualTo(1), "More than one bar exists for a single damageable.");
        }

        [UnityTest]
        public IEnumerator WorldBar_DespawnLeavesNoOrphan_AndRespawnGetsFreshFullBar()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: false);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager client = _harness.CreateNetworkManager(withSpawner: false);
            WorldSpaceHealthBars bars = AttachWorldBars(client, "WorldBarsB");
            client.ClientManager.StartConnection();

            // A spawner owning one post: it despawns a dead target and respawns it after a delay.
            GameObject postGo = new GameObject("TestTargetPost");
            postGo.transform.position = new Vector3(0f, 0f, 10f);
            _sceneObjects.Add(postGo);

            GameObject spawnerGo = new GameObject("TestTargetSpawner");
            _sceneObjects.Add(spawnerGo);
            DummyTargetSpawner spawner = spawnerGo.AddComponent<DummyTargetSpawner>();
            spawner.SetTargetPrefab(LoadTargetPrefab());
            spawner.SetPosts(new Transform[] { postGo.transform });
            spawner.SetNetworkManager(server);   // server already started -> spawns immediately

            // A bar tracks the first target on the client view.
            yield return _harness.WaitUntil(
                () => bars.BarCount == 1,
                "World bar never appeared over the spawned target.",
                15f);
            NetworkObject firstClientTarget = NetworkSessionHarness.FindDamageable(client.ClientManager.Objects.Spawned);
            Assert.IsNotNull(firstClientTarget, "Observer has no instance of the first target.");
            int firstObjectId = firstClientTarget.ObjectId;
            Assert.That(bars.HasBar(firstObjectId), Is.True, "No bar for the first target.");

            // Kill the target on the server; the spawner despawns it.
            NetworkObject serverTarget = NetworkSessionHarness.FindDamageable(server.ServerManager.Objects.Spawned);
            serverTarget.GetComponent<Health>().ApplyDamageOnServer(Health.MaxShield + Health.MaxHealth);

            // The bar drops with the object - no orphan floats over the despawned target.
            yield return _harness.WaitUntil(
                () => bars.BarCount == 0,
                "World bar never dropped when its target despawned (orphan).",
                15f);
            Assert.That(bars.HasBar(firstObjectId), Is.False, "A stale bar survived its target's despawn.");

            // The respawn gets a fresh, full bar (not a leftover damaged one).
            yield return _harness.WaitUntil(
                () => bars.BarCount == 1,
                "World bar never returned when the target respawned.",
                15f);
            NetworkObject respawnedClientTarget = NetworkSessionHarness.FindDamageable(client.ClientManager.Objects.Spawned);
            Assert.IsNotNull(respawnedClientTarget, "Observer has no instance of the respawned target.");
            int respawnObjectId = respawnedClientTarget.ObjectId;
            yield return _harness.WaitUntil(
                () => bars.TryGetFill(respawnObjectId, out float f) && f > 0.999f,
                "Respawned target's bar was not fresh and full.",
                15f);
            Assert.That(bars.BarCount, Is.EqualTo(1), "More than one bar exists after respawn.");
        }

        [UnityTest]
        public IEnumerator RaiderHud_ReflectsOwnedRaiderReplicatedHealth()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager client = _harness.CreateNetworkManager(withSpawner: false);

            /* Production order: the generated scene wires the HUD before any
             * connection exists, so create it BEFORE the client connects. */
            GameObject hudGo = new GameObject("RaiderHudA");
            _sceneObjects.Add(hudGo);
            RaiderHud hud = hudGo.AddComponent<RaiderHud>();
            hud.SetNetworkManager(client);

            // Structural: the HUD is client-local and never networked (ADR-0005).
            Assert.IsNull(hudGo.GetComponent<NetworkObject>(), "HUD must not carry a NetworkObject.");
            /* Upcast to MonoBehaviour: a direct 'hud is NetworkBehaviour' check on
             * the sealed component type triggers compiler warning CS0184. */
            Assert.IsFalse((MonoBehaviour)hud is FishNet.Object.NetworkBehaviour, "HUD must be a plain MonoBehaviour, not a NetworkBehaviour.");

            client.ClientManager.StartConnection();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 1,
                "Server never saw the Raider spawn.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(client) != null,
                "Client never saw its owned Raider.");

            // Neutral scripted provider so the prefab's hardware provider never fires in a non-batch editor run.
            NetworkObject ownedRaider = NetworkSessionHarness.FindOwnedRaider(client);
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(new ScriptedRaiderIntentProvider());

            // The HUD acquires the owned Raider and binds to its Health (Raiders carry Health as of #17).
            yield return _harness.WaitUntil(() => hud.HasOwnedRaider, "HUD never acquired the owned Raider.");
            yield return _harness.WaitUntil(() => hud.BoundHealth != null, "HUD never bound to the owned Raider's replicated Health.");

            // Full bars at spawn (anti-vacuity for the drop below).
            yield return _harness.WaitUntil(
                () => hud.ShieldFill > 0.999f && hud.HealthFill > 0.999f,
                "HUD screen bars did not read full Shield/Health at spawn.");

            /* Live binding: damage the owned Raider on the SERVER (authoritative), and
             * the client-local bars must follow the replicated drop - Shield empties,
             * then Health falls by the overflow. This is AC #1 (the own bars reflect
             * the replicated Shield/Health), the way taking emitter fire will read. */
            NetworkObject serverRaider = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, ownedRaider.ObjectId);
            Assert.IsNotNull(serverRaider, "Server view has no instance of the owned Raider.");
            serverRaider.GetComponent<Health>().ApplyDamageOnServer(Health.MaxShield + 20);

            float expectedHealthFill = (float)(Health.MaxHealth - 20) / Health.MaxHealth;
            yield return _harness.WaitUntil(
                () => hud.ShieldFill < 0.01f && Mathf.Abs(hud.HealthFill - expectedHealthFill) < 0.01f,
                "HUD screen bars never reflected the owned Raider's replicated damage.",
                15f);
        }

        private WorldSpaceHealthBars AttachWorldBars(NetworkManager client, string name)
        {
            GameObject go = new GameObject(name);
            _sceneObjects.Add(go);
            WorldSpaceHealthBars bars = go.AddComponent<WorldSpaceHealthBars>();
            bars.SetNetworkManager(client);
            return bars;
        }

        /// <summary>Server-spawns a DummyTarget (Side.Hostile) at the given position and returns the server instance.</summary>
        private NetworkObject SpawnTargetAt(NetworkManager server, Vector3 position)
        {
            NetworkObject nob = server.GetPooledInstantiated(LoadTargetPrefab(), position, Quaternion.identity, true);
            server.ServerManager.Spawn(nob);
            return nob;
        }

        private NetworkObject LoadTargetPrefab()
        {
            NetworkObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(DummyTargetPrefabPath);
#endif
            if (prefab == null)
                Assert.Fail("DummyTarget prefab missing — run 'Light Raiders > Generate Session Assets' or the documented -executeMethod first.");
            return prefab;
        }
    }
}
