using System.Collections;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LightRaiders.Tests
{
    /// <summary>
    /// PlayMode tests for server-authoritative Raider movement. The default test
    /// scene is empty, so each test builds its own floor, obstacle and spawn
    /// points. Assertions read transforms through the network-visible surface
    /// (server/client Objects.Spawned) and use inequality thresholds only.
    /// </summary>
    [TestFixture]
    public class RaiderMovementTests
    {
        /* Deliberately mirrors ArenaSceneGenerator's Obstacle1 dimensions, but the
         * obstacle here is built locally by the fixture — no sync required. */
        private static readonly Vector3 ObstacleCenter = new Vector3(6f, 1f, 4f);

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

            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "TestObstacle";
            obstacle.transform.position = ObstacleCenter;
            obstacle.transform.localScale = new Vector3(2f, 2f, 2f);
            _sceneObjects.Add(obstacle);

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
        public IEnumerator OwnerIntent_MovesRaiderOnServer()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager client = _harness.CreateNetworkManager(withSpawner: false);
            client.ClientManager.StartConnection();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 1,
                "Server never saw the Raider spawn.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(client) != null,
                "Client never saw its owned Raider.");

            NetworkObject ownedRaider = NetworkSessionHarness.FindOwnedRaider(client);
            NetworkObject serverRaider = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, ownedRaider.ObjectId);
            Assert.IsNotNull(serverRaider, "Server view has no instance for the client-owned Raider.");

            /* Inject the scripted provider (zero intent) before baselining so the
             * prefab's hardware provider never feeds real keyboard state into a
             * non-batch editor run. */
            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(serverRaider);
            Vector3 baseline = serverRaider.transform.position;

            scripted.Intent = new RaiderIntent { Move = new Vector2(-1f, 0f) };

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(serverRaider.transform.position, baseline) > 0.5f,
                "Server Raider never moved in response to owner intent.");

            float y = serverRaider.transform.position.y;
            Assert.That(y, Is.GreaterThan(-0.5f), "Server Raider fell through the floor.");
            Assert.That(y, Is.LessThan(1.5f), "Server Raider climbed unexpectedly.");
        }

        [UnityTest]
        public IEnumerator OwnerMovement_IsObservedByOtherClient()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager clientA = _harness.CreateNetworkManager(withSpawner: false);
            clientA.ClientManager.StartConnection();
            NetworkManager clientB = _harness.CreateNetworkManager(withSpawner: false);
            clientB.ClientManager.StartConnection();

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(clientB.ClientManager.Objects.Spawned) == 2,
                "Client B never saw both Raiders.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(clientA) != null,
                "Client A never saw its owned Raider.");

            NetworkObject raiderOnA = NetworkSessionHarness.FindOwnedRaider(clientA);
            NetworkObject raiderOnB = NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderOnA.ObjectId);
            Assert.IsNotNull(raiderOnB, "Client B's view has no instance of A's Raider.");
            NetworkObject raiderOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderOnA.ObjectId);
            Assert.IsNotNull(raiderOnServer, "Server view has no instance of A's Raider.");

            // Zero-intent injection before baselining; see OwnerIntent_MovesRaiderOnServer.
            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            raiderOnA.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(raiderOnServer);
            Vector3 baseline = raiderOnB.transform.position;

            scripted.Intent = new RaiderIntent { Move = new Vector2(-1f, 0f) };

            // 15s: 30Hz ticks plus 2-tick interpolation on the observing client.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(raiderOnB.transform.position, baseline) > 0.5f,
                "Client B never observed A's Raider moving.",
                15f);
        }

        [UnityTest]
        public IEnumerator NonOwnerIntent_DoesNotMoveAnotherRaider()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager clientA = _harness.CreateNetworkManager(withSpawner: false);
            clientA.ClientManager.StartConnection();
            NetworkManager clientB = _harness.CreateNetworkManager(withSpawner: false);
            clientB.ClientManager.StartConnection();

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(clientB.ClientManager.Objects.Spawned) == 2,
                "Client B never saw both Raiders.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(clientA) != null && NetworkSessionHarness.FindOwnedRaider(clientB) != null,
                "Clients never saw their owned Raiders.");

            NetworkObject raiderA = NetworkSessionHarness.FindOwnedRaider(clientA);
            NetworkObject raiderAOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderA.ObjectId);
            Assert.IsNotNull(raiderAOnServer, "Server view has no instance of A's Raider.");
            NetworkObject raiderAOnB = NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderA.ObjectId);
            Assert.IsNotNull(raiderAOnB, "Client B's view has no instance of A's Raider.");

            NetworkObject raiderB = NetworkSessionHarness.FindOwnedRaider(clientB);
            NetworkObject raiderBOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderB.ObjectId);
            Assert.IsNotNull(raiderBOnServer, "Server view has no instance of B's Raider.");

            /* Neutral scripted providers on both OWNED instances before baselining,
             * so neither polls the prefab's hardware provider during the test. */
            ScriptedRaiderIntentProvider neutralProviderA = new ScriptedRaiderIntentProvider();
            raiderA.GetComponent<RaiderMovement>().SetIntentProvider(neutralProviderA);
            ScriptedRaiderIntentProvider ownProvider = new ScriptedRaiderIntentProvider();
            raiderB.GetComponent<RaiderMovement>().SetIntentProvider(ownProvider);

            yield return _harness.SettleServerRaider(raiderAOnServer);
            yield return _harness.SettleServerRaider(raiderBOnServer);
            Vector3 baselineA = raiderAOnServer.transform.position;

            /* The component only sends intent when IsOwner, so B's provider on
             * A's Raider sends nothing; FishNet's RequireOwnership is the second
             * line of defense, deliberately untested here. */
            ScriptedRaiderIntentProvider hostileProvider = new ScriptedRaiderIntentProvider();
            hostileProvider.Intent = new RaiderIntent { Move = new Vector2(1f, 0f) };
            raiderAOnB.GetComponent<RaiderMovement>().SetIntentProvider(hostileProvider);

            // Positive control: B moves its own Raider, proving ticks/RPCs flow.
            Vector3 baselineB = raiderBOnServer.transform.position;
            ownProvider.Intent = new RaiderIntent { Move = new Vector2(-1f, 0f) };

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(raiderBOnServer.transform.position, baselineB) > 0.5f,
                "Positive control failed: B's own Raider never moved on the server.");

            Assert.That(
                NetworkSessionHarness.PlanarDistance(raiderAOnServer.transform.position, baselineA),
                Is.LessThan(0.1f),
                "A's Raider moved from a non-owner's intent.");
        }

        [UnityTest]
        public IEnumerator Raider_IsBlockedByObstacle()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager client = _harness.CreateNetworkManager(withSpawner: false);
            client.ClientManager.StartConnection();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 1,
                "Server never saw the Raider spawn.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(client) != null,
                "Client never saw its owned Raider.");

            NetworkObject ownedRaider = NetworkSessionHarness.FindOwnedRaider(client);
            NetworkObject serverRaider = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, ownedRaider.ObjectId);
            Assert.IsNotNull(serverRaider, "Server view has no instance for the client-owned Raider.");

            /* Homing intent instead of a constant direction: a constant diagonal
             * would slide along the cube face instead of stalling against it. */
            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(serverRaider);

            yield return HomeUntil(
                scripted,
                serverRaider.transform,
                () => NetworkSessionHarness.PlanarDistance(serverRaider.transform.position, ObstacleCenter) < 2.5f,
                20f,
                "Raider never approached the obstacle.");

            /* The proximity gate above fires in free flight, ~0.6m before first
             * contact, and the slide along the face covers another ~1m. Keep
             * homing for a fixed convergence window so the snapshot below is
             * taken AFTER the raider has stalled perpendicular to the face. */
            yield return HomeUntil(scripted, serverRaider.transform, null, 2f, null);

            Vector3 snapshot = serverRaider.transform.position;
            yield return HomeUntil(scripted, serverRaider.transform, null, 1.5f, null);

            Assert.That(
                NetworkSessionHarness.PlanarDistance(serverRaider.transform.position, snapshot),
                Is.LessThan(0.5f),
                "Raider kept moving while pushing into the obstacle.");
            Assert.That(
                NetworkSessionHarness.PlanarDistance(serverRaider.transform.position, ObstacleCenter),
                Is.GreaterThan(1.2f),
                "Raider penetrated the obstacle.");
            float y = serverRaider.transform.position.y;
            Assert.That(y, Is.GreaterThan(-0.5f), "Raider fell through the floor at the obstacle.");
            Assert.That(y, Is.LessThan(1.5f), "Raider climbed the obstacle.");
        }

        /// <summary>
        /// Re-aims the scripted intent at the obstacle every frame until the
        /// condition holds. A null condition homes for the full duration; a
        /// non-null condition that never holds fails the test.
        /// </summary>
        private IEnumerator HomeUntil(
            ScriptedRaiderIntentProvider scripted,
            Transform serverRaider,
            System.Func<bool> condition,
            float seconds,
            string failureMessage)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                Vector3 toObstacle = ObstacleCenter - serverRaider.position;
                Vector2 planar = new Vector2(toObstacle.x, toObstacle.z);
                scripted.Intent = new RaiderIntent { Move = planar.normalized };

                if (condition != null && condition())
                    yield break;
                yield return null;
            }

            if (condition != null)
                Assert.Fail(failureMessage + " (timed out after " + seconds + "s)");
        }

    }
}
