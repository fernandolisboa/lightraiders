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
        /* Simulated one-way latency for the latency test. At MoveSpeed 5 m/s, an
         * owner predicting ahead of the server by one-way latency plus input
         * buffering leads it by ~0.4-0.6m while moving. */
        private const long LatencyOneWayMs = 80;

        /* The owner must lead the server by at least this much at some point while
         * moving under latency. A non-predicting owner only follows the server,
         * so its lead is <= 0. */
        private const float MinPredictedLead = 0.2f;

        /* At rest, a reconcile replays zero-move inputs on top of the server's
         * state, so the two views should coincide. These bounds catch a steady
         * offset or a facing desync that the old 0.75m / 1.5m bounds hid. */
        private const float RestPositionTolerance = 0.05f;
        private const float RestPositionToleranceUnderLatency = 0.1f;
        private const float RestFacingToleranceDegrees = 3f;

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

            /* Converged: at rest the reconcile leaves the owner on the server's
             * position and facing - no steady offset, no rotation desync. */
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, serverRaider.transform.position) < RestPositionTolerance,
                () => "Owner and server never converged after prediction - reconcile is not correcting. Gap="
                    + NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, serverRaider.transform.position).ToString("F3") + "m.",
                10f);
            Assert.That(
                NetworkSessionHarness.PlanarAngle(ownedRaider.transform.forward, serverRaider.transform.forward),
                Is.LessThan(RestFacingToleranceDegrees),
                "Owner and server facing diverged at rest - rotation is not reconciled.");

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
            EnableLatency(server, LatencyOneWayMs);
            EnableLatency(client, LatencyOneWayMs);
            // Let the simulator take effect before baselining (packets already in flight drain first).
            yield return _harness.WaitForServerTicks(server, 5);

            Vector3 ownerBaseline = ownedRaider.transform.position;
            Vector3 serverBaseline = serverRaider.transform.position;
            scripted.Intent = new RaiderIntent { Move = new Vector2(-1f, 0f) };

            /* The prediction proof: while moving under latency, the owner's local
             * transform runs AHEAD of the server's. Sampled every frame over a
             * tick-gated window and judged on the maximum, so a single editor stall
             * (where both sides catch up at once) cannot fail it; a non-predicting
             * owner only ever trails the server, so its maximum lead stays <= 0. */
            float maxLead = float.NegativeInfinity;
            uint leadWindowEnd = server.TimeManager.Tick + 20;
            float leadDeadline = Time.realtimeSinceStartup + 15f;
            while (server.TimeManager.Tick < leadWindowEnd)
            {
                float lead = NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, ownerBaseline)
                    - NetworkSessionHarness.PlanarDistance(serverRaider.transform.position, serverBaseline);
                if (lead > maxLead)
                    maxLead = lead;
                if (Time.realtimeSinceStartup > leadDeadline)
                    Assert.Fail("Server ticks never advanced during the lead window.");
                yield return null;
            }

            Assert.That(
                NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, ownerBaseline),
                Is.GreaterThan(0.5f),
                "Owner's predicted transform never moved under latency.");
            Assert.That(
                maxLead,
                Is.GreaterThan(MinPredictedLead),
                "Owner never led the server under latency - movement is not being predicted locally.");

            // Stop and let the delayed reconciles settle.
            scripted.Intent = new RaiderIntent { Move = Vector2.zero };
            yield return _harness.WaitForServerTicks(server, 40);

            /* Converged despite the round-trip: at rest the reconcile must put the
             * owner back on the server's position and facing. */
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, serverRaider.transform.position) < RestPositionToleranceUnderLatency,
                () => "Owner and server never converged under latency - reconcile is not correcting through the round-trip. Gap="
                    + NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, serverRaider.transform.position).ToString("F3") + "m.",
                15f);
            Assert.That(
                NetworkSessionHarness.PlanarAngle(ownedRaider.transform.forward, serverRaider.transform.forward),
                Is.LessThan(RestFacingToleranceDegrees),
                "Owner and server facing diverged under latency - rotation is not reconciled.");
        }

        private static void EnableLatency(NetworkManager networkManager, long oneWayMs)
        {
            var simulator = networkManager.TransportManager.LatencySimulator;
            simulator.SetLatency(oneWayMs);
            simulator.SetEnabled(true);
        }
    }
}
