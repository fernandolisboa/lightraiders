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
    /// PlayMode tests for Raider death (issue #18): a Raider driven to zero Health
    /// by real hostile-emitter fire is removed from play, drops a loot bag at the
    /// death position, and respawns after a delay at a spawn point with full
    /// Shield/Health - owned by the SAME connection, so its input and the owner's
    /// camera reattach. Every assertion is on network-visible state across the
    /// server and an observer client view.
    /// </summary>
    [TestFixture]
    public class RaiderDeathTests
    {
        /* Duplicated from SessionAssetPaths (same rule as NetworkSessionHarness):
         * this assembly cannot reference the editor-only LightRaiders.Editor asmdef. */
        private const string HostileEmitterPrefabPath = "Assets/LightRaiders/Prefabs/HostileEmitter.prefab";
        private const string LootBagPrefabPath = "Assets/LightRaiders/Prefabs/LootBag.prefab";

        private NetworkSessionHarness _harness;
        private List<GameObject> _sceneObjects;

        [SetUp]
        public void SetUp()
        {
            _harness = new NetworkSessionHarness();
            _sceneObjects = new List<GameObject>();

            // Top of the floor sits at y=0.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(80f, 1f, 80f);
            _sceneObjects.Add(floor);
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
        public IEnumerator RaiderDeath_DropsBag_Respawns_PreservingOwnershipInputAndCamera()
        {
            // The Raider spawns (and respawns) at (5,1,0); three emitters cluster around it to kill it quickly.
            Transform[] spawns = MakeSpawns(new Vector3(5f, 1f, 0f));
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: spawns);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager client = _harness.CreateNetworkManager(withSpawner: false);
            RaiderCameraRig rig = AddCameraRig(client);   // client-local camera, wired like production
            client.ClientManager.StartConnection();

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 1,
                "Server never saw the Raider spawn.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(client) != null,
                "Client never saw its owned Raider.");

            NetworkObject ownedRaider = NetworkSessionHarness.FindOwnedRaider(client);
            int originalRaiderId = ownedRaider.ObjectId;
            NetworkObject serverRaider = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, originalRaiderId);
            Assert.IsNotNull(serverRaider, "Server view has no instance of the owned Raider.");

            // Neutral provider so the victim never moves or fires on its own; settle, then note the death position.
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(new ScriptedRaiderIntentProvider());
            yield return _harness.SettleServerRaider(serverRaider);
            Vector3 deathPosition = serverRaider.transform.position;

            // Baselines (anti-vacuity).
            Assert.That(serverRaider.GetComponent<Health>().CurrentHealth, Is.EqualTo(Health.MaxHealth), "Raider did not start at full Health.");
            Assert.That(NetworkSessionHarness.CountLootBags(server.ServerManager.Objects.Spawned), Is.EqualTo(0), "A loot bag existed before any death.");

            // Three emitters within 15 m; they fire on cadence and drive the Raider to zero Health.
            List<NetworkObject> emitters = new List<NetworkObject>
            {
                SpawnEmitterAt(server, new Vector3(5f, 0f, 4f)),
                SpawnEmitterAt(server, new Vector3(5f, 0f, -4f)),
                SpawnEmitterAt(server, new Vector3(9f, 0f, 0f))
            };

            // Removed from play on the server AND the observer view.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 0,
                "Emitter fire never removed the Raider on the server.",
                25f);
            uint deathObservedTick = server.TimeManager.Tick;
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOnView(client.ClientManager.Objects.Spawned, originalRaiderId) == null,
                "The observer never saw the Raider removed.",
                15f);

            // Silence the threat so the respawn at the same spawn point is not instantly re-killed.
            foreach (NetworkObject emitter in emitters)
            {
                if (emitter != null && emitter.IsSpawned)
                    server.ServerManager.Despawn(emitter);
            }

            // A loot bag dropped at the death position, on the server and the observer.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountLootBags(server.ServerManager.Objects.Spawned) == 1,
                "No loot bag dropped on the server.",
                15f);
            NetworkObject serverBag = NetworkSessionHarness.FindLootBag(server.ServerManager.Objects.Spawned);
            Assert.That(
                NetworkSessionHarness.PlanarDistance(serverBag.transform.position, deathPosition),
                Is.LessThan(0.6f),
                "Loot bag did not drop at the death position.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountLootBags(client.ClientManager.Objects.Spawned) == 1,
                "The observer never saw the loot bag.",
                15f);

            // Respawns after the delay at the spawn point, full Health, a fresh object, on all views.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 1,
                "The Raider never respawned on the server.",
                15f);
            uint respawnObservedTick = server.TimeManager.Tick;
            uint respawnTicks = RaiderRespawner.RespawnTicks(server.TimeManager);
            Assert.That(
                respawnObservedTick - deathObservedTick,
                Is.GreaterThanOrEqualTo(respawnTicks / 2),
                "The Raider respawned too soon - the respawn delay was not honored.");

            NetworkObject respawnedServer = NetworkSessionHarness.FindRaider(server.ServerManager.Objects.Spawned);
            Assert.AreNotEqual(originalRaiderId, respawnedServer.ObjectId, "Respawn reused the dead Raider - not a fresh spawn.");
            Assert.That(
                NetworkSessionHarness.PlanarDistance(respawnedServer.transform.position, new Vector3(5f, 0f, 0f)),
                Is.LessThan(1f),
                "The Raider did not respawn at the spawn point.");
            Health respawnedHealth = respawnedServer.GetComponent<Health>();
            Assert.That(respawnedHealth.Shield, Is.EqualTo(Health.MaxShield), "Respawned Raider did not have full Shield.");
            Assert.That(respawnedHealth.CurrentHealth, Is.EqualTo(Health.MaxHealth), "Respawned Raider did not have full Health.");

            // Ownership survives: the SAME client owns the fresh Raider.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(client) != null
                    && NetworkSessionHarness.FindOwnedRaider(client).ObjectId == respawnedServer.ObjectId,
                "The client does not own its respawned Raider.",
                15f);
            NetworkObject respawnedOwned = NetworkSessionHarness.FindOwnedRaider(client);

            // The dying client's camera follows its own respawned Raider.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(rig.transform.position, respawnedServer.transform.position + RaiderCameraRig.FollowOffset) < 0.6f,
                "The camera did not follow the respawned Raider.",
                15f);

            // Input survives: a scripted fire intent on the respawned Raider spawns a projectile.
            ScriptedRaiderIntentProvider refire = new ScriptedRaiderIntentProvider();
            respawnedOwned.GetComponent<RaiderMovement>().SetIntentProvider(refire);
            refire.Intent = new RaiderIntent
            {
                AimPoint = respawnedServer.transform.position + new Vector3(10f, 0f, 0f),
                FirePressed = true
            };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned) >= 1,
                "The respawned Raider could not fire - input did not survive respawn.",
                15f);
        }

        [UnityTest]
        public IEnumerator LootBag_DespawnsAfterLifetime_AcrossViews()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: false);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager client = _harness.CreateNetworkManager(withSpawner: false);
            client.ClientManager.StartConnection();
            yield return _harness.WaitUntil(() => client.ClientManager.Started, "Client did not start.");

            // Spawn a bag directly with a SHORT test lifetime (the death path leaves the ~60s default).
            const float testLifetime = 2f;
            NetworkObject bag = SpawnLootBagWithLifetime(server, new Vector3(0f, 0f, 3f), testLifetime);
            uint spawnTick = server.TimeManager.Tick;
            Assert.IsNotNull(bag, "Failed to spawn the loot bag.");

            // Observer sees it (anti-vacuity for the despawn below).
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountLootBags(client.ClientManager.Objects.Spawned) == 1,
                "The observer never saw the loot bag.",
                15f);

            // It despawns everywhere after its lifetime.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountLootBags(server.ServerManager.Objects.Spawned) == 0,
                "The loot bag never despawned on the server.",
                15f);
            uint lifetimeTicks = LootBag.LifetimeTicks(server.TimeManager, testLifetime);
            Assert.That(
                server.TimeManager.Tick - spawnTick,
                Is.GreaterThanOrEqualTo(lifetimeTicks),
                "The loot bag despawned before its lifetime elapsed.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountLootBags(client.ClientManager.Objects.Spawned) == 0,
                "The loot bag never despawned on the observer.",
                15f);
        }

        private Transform[] MakeSpawns(params Vector3[] positions)
        {
            Transform[] spawns = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject go = new GameObject("TestSpawn" + i);
                go.transform.position = positions[i];
                _sceneObjects.Add(go);
                spawns[i] = go.transform;
            }

            return spawns;
        }

        private RaiderCameraRig AddCameraRig(NetworkManager client)
        {
            GameObject rigGo = new GameObject("TestCameraRig");
            _sceneObjects.Add(rigGo);
            RaiderCameraRig rig = rigGo.AddComponent<RaiderCameraRig>();
            rig.SetNetworkManager(client);
            return rig;
        }

        private NetworkObject SpawnEmitterAt(NetworkManager server, Vector3 position)
        {
            NetworkObject prefab = LoadPrefab(HostileEmitterPrefabPath, "HostileEmitter");
            NetworkObject nob = server.GetPooledInstantiated(prefab, position, Quaternion.identity, true);
            server.ServerManager.Spawn(nob);
            return nob;
        }

        private NetworkObject SpawnLootBagWithLifetime(NetworkManager server, Vector3 position, float lifetimeSeconds)
        {
            NetworkObject prefab = LoadPrefab(LootBagPrefabPath, "LootBag");
            NetworkObject nob = server.GetPooledInstantiated(prefab, position, Quaternion.identity, true);
            nob.GetComponent<LootBag>().ServerInitLifetime(lifetimeSeconds);
            server.ServerManager.Spawn(nob);
            return nob;
        }

        private NetworkObject LoadPrefab(string path, string label)
        {
            NetworkObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
#endif
            if (prefab == null)
                Assert.Fail(label + " prefab missing — run 'Light Raiders > Generate Session Assets' or the documented -executeMethod first.");
            return prefab;
        }
    }
}
