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
    /// PlayMode tests for client-side prediction (ADR-0007). These guard the two
    /// CSP guarantees the server-authoritative model did NOT provide: the owner's
    /// LOCAL transform is driven by prediction (there is no NetworkTransform to
    /// stream it), and reconcile keeps the owner converged with the authoritative
    /// server — including under simulated latency, where the un-predicted model
    /// would leave the owner lagging by a full round-trip.
    ///
    /// The MPPM three-window feel-test (issue #20) is where the *feel* is judged;
    /// these tests only prove the prediction pipeline is wired and stable. The
    /// default test scene is empty, so each test builds its own floor and spawns.
    /// </summary>
    [TestFixture]
    public class RaiderPredictionTests
    {
        private NetworkSessionHarness _harness;
        private List<GameObject> _sceneObjects;
        private Transform[] _spawns;

        [SetUp]
        public void SetUp()
        {
            _harness = new NetworkSessionHarness();
            _sceneObjects = new List<GameObject>();

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
        public IEnumerator OwnerPrediction_DrivesLocalTransform_AndConvergesWithServer()
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

            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(serverRaider);
            Vector3 ownerBaseline = ownedRaider.transform.position;

            scripted.Intent = new RaiderIntent { Move = new Vector2(-1f, 0f) };

            /* The OWNER's own client-side instance moves — driven by its local
             * replicate, not by any NetworkTransform (there is none). This is the
             * prediction: the old server-authoritative model froze this transform
             * until the server streamed a position back. */
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, ownerBaseline) > 0.5f,
                "Owner's local (predicted) transform never moved.");

            // Stop and let the reconcile settle both views to the same authoritative rest.
            scripted.Intent = new RaiderIntent { Move = Vector2.zero };
            yield return _harness.WaitForServerTicks(server, 20);

            /* Converged: prediction plus reconcile leaves the owner within a small
             * tolerance of the authoritative server position — no runaway desync. */
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, serverRaider.transform.position) < 0.75f,
                "Owner and server never converged after prediction — reconcile is not correcting.",
                10f);

            float y = ownedRaider.transform.position.y;
            Assert.That(y, Is.GreaterThan(-0.5f), "Predicted Raider fell through the floor.");
            Assert.That(y, Is.LessThan(1.5f), "Predicted Raider climbed unexpectedly.");
        }

        [UnityTest]
        public IEnumerator OwnerPrediction_StaysConvergedUnderLatency()
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

            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(serverRaider);

            /* ~80ms one-way on both sides: a real round-trip the un-predicted model
             * could not hide. The owner predicts through it locally; the reconcile
             * must still keep the two views aligned. This is the same simulator the
             * LatencyToggle drives for the manual feel-test. */
            EnableLatency(server, 80);
            EnableLatency(client, 80);

            Vector3 ownerBaseline = ownedRaider.transform.position;
            scripted.Intent = new RaiderIntent { Move = new Vector2(-1f, 0f) };

            // The owner still moves promptly under latency — prediction, not a stalled RTT wait.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, ownerBaseline) > 0.5f,
                "Owner's predicted transform never moved under latency.",
                15f);

            // Sustain, then stop and let the delayed reconciles settle.
            yield return _harness.WaitForServerTicks(server, 20);
            scripted.Intent = new RaiderIntent { Move = Vector2.zero };
            yield return _harness.WaitForServerTicks(server, 40);

            /* Converged despite the round-trip: a wider 1.5m tolerance than the
             * no-latency case absorbs the in-flight reconcile buffer, but a
             * rubber-banding or runaway desync would blow well past it. */
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, serverRaider.transform.position) < 1.5f,
                "Owner and server never converged under latency — reconcile is not correcting through the round-trip.",
                15f);
        }

        private static void EnableLatency(NetworkManager networkManager, long oneWayMs)
        {
            var simulator = networkManager.TransportManager.LatencySimulator;
            simulator.SetLatency(oneWayMs);
            simulator.SetEnabled(true);
        }
    }
}
