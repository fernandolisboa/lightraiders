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
    /// PlayMode tests for server-set aim facing. The default test scene is empty,
    /// so each test builds its own floor and spawn points (no obstacle: nothing
    /// here collides). During settle the zero-default ScriptedRaiderIntentProvider
    /// sends AimPoint=(0,0,0) - a VALID point - so raiders may already face world
    /// origin at baseline; every test asserts a >30 degree anti-vacuity
    /// precondition instead of assuming spawn facing.
    /// </summary>
    [TestFixture]
    public class RaiderAimTests
    {
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
        public IEnumerator OwnerAim_SetsFacingOnServer()
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
             * prefab's hardware provider never feeds real mouse state into a
             * non-batch editor run. */
            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(serverRaider);
            Vector3 basePos = serverRaider.transform.position;

            // Yaw 90: due +X from the raider, 135 degrees off the 225-degree origin bearing from spawn A.
            Vector3 firstAim = new Vector3(basePos.x + 10f, 0f, basePos.z);
            Assert.That(
                NetworkSessionHarness.PlanarAngle(serverRaider.transform.forward, firstAim - basePos),
                Is.GreaterThan(30f),
                "Precondition: already facing the first target - test would be vacuous.");

            scripted.Intent = new RaiderIntent { AimPoint = firstAim };

            /* Tolerance 5 degrees, honestly: Quaternion32 packing error is ~0.2
             * degrees and this reads the server transform directly - the slack is
             * for the tick boundary, not the wire. */
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarAngle(serverRaider.transform.forward, firstAim - basePos) < 5f,
                "Server facing never converged on the first aim point.");

            /* Yaw 315: a 135-degree swing from the first target, and 90 degrees off
             * the 225-degree origin bearing so the zero-aim default facing cannot
             * satisfy the wait. y=5 is deliberate: correct code flattens Y, so
             * forward.y stays ~0; a flatten-omission mutant pitches forward.y to
             * ~0.33 (5 over the 15.1 slant range) and the pitch assert below
             * kills it. */
            Vector3 secondAim = new Vector3(basePos.x - 10f, 5f, basePos.z + 10f);

            scripted.Intent = new RaiderIntent { AimPoint = secondAim };

            // PlanarAngle flattens both sides, so the y offset does not affect the bearing.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarAngle(serverRaider.transform.forward, secondAim - basePos) < 5f,
                "Server facing never converged on the second aim point.");

            Assert.That(
                Mathf.Abs(serverRaider.transform.forward.y),
                Is.LessThan(0.1f),
                "Facing pitched off the ground plane.");

            /* Sanitization holds. Positive control = the two convergences above:
             * aim intents demonstrably steer facing, so an unchanged facing below
             * means the bad point was discarded, not that aim is inert. The hold
             * window is measured in server TICKS, not wall time: a single editor
             * stall could consume a realtime window before any tick processed the
             * bad intent, letting the assert pass vacuously. */
            scripted.Intent = new RaiderIntent { AimPoint = new Vector3(float.NaN, 0f, float.NaN) };
            yield return _harness.WaitForServerTicks(server, 15);
            Assert.That(
                NetworkSessionHarness.PlanarAngle(serverRaider.transform.forward, secondAim - basePos),
                Is.LessThan(5f),
                "Non-finite AimPoint changed facing.");

            scripted.Intent = new RaiderIntent { AimPoint = serverRaider.transform.position };
            yield return _harness.WaitForServerTicks(server, 15);
            Assert.That(
                NetworkSessionHarness.PlanarAngle(serverRaider.transform.forward, secondAim - basePos),
                Is.LessThan(5f),
                "Aim at own feet changed facing.");
        }

        [UnityTest]
        public IEnumerator OwnerAim_FacingIsObservedByOtherClient()
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

            // Zero-intent injection before baselining; see OwnerAim_SetsFacingOnServer.
            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            raiderOnA.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(raiderOnServer);
            Vector3 basePos = raiderOnServer.transform.position;

            // Yaw 135: at least 90 degrees off the origin bearing from either spawn.
            Vector3 aim = new Vector3(basePos.x + 10f, 0f, basePos.z - 10f);
            Assert.That(
                NetworkSessionHarness.PlanarAngle(raiderOnB.transform.forward, aim - basePos),
                Is.GreaterThan(30f),
                "Precondition: already facing the target - test would be vacuous.");

            scripted.Intent = new RaiderIntent { AimPoint = aim };

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarAngle(raiderOnServer.transform.forward, aim - basePos) < 5f,
                "Positive control failed: server facing never converged.");

            /* Tolerance 5 degrees, honestly: Quaternion32 smallest-three packing
             * (10 bits per component) contributes ~0.2 degrees and the observer's
             * interpolation snap band ~1 degree; the rest is slack, not cover for
             * a wrong bearing (the anti-vacuity gap is 30+ degrees). 15s: 30Hz
             * ticks plus 2-tick interpolation on the observing client. */
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarAngle(raiderOnB.transform.forward, aim - basePos) < 5f,
                "Client B never observed A's facing.",
                15f);
        }

        [UnityTest]
        public IEnumerator AimAndMovement_AreIndependent()
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

            // Zero-intent injection before baselining; see OwnerAim_SetsFacingOnServer.
            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(serverRaider);
            Vector3 basePos = serverRaider.transform.position;

            /* Due -Z from the baseline; the 1000m range keeps the bearing shift
             * under ~0.25 degrees over the few meters of +X travel below, so
             * Vector3.back stays a valid reference for the whole test. */
            Vector3 aim = new Vector3(basePos.x, 0f, basePos.z - 1000f);
            Assert.That(
                NetworkSessionHarness.PlanarAngle(serverRaider.transform.forward, Vector3.back),
                Is.GreaterThan(30f),
                "Precondition: already facing the target - test would be vacuous.");

            // Single struct: move due +X while aiming due -Z (ADR-0001 decoupling).
            scripted.Intent = new RaiderIntent { Move = new Vector2(1f, 0f), AimPoint = aim };

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarAngle(serverRaider.transform.forward, Vector3.back) < 5f,
                "Server facing never converged on the aim point.");

            // A mutant moving along facing goes -Z and never gains +X.
            yield return _harness.WaitUntil(
                () => serverRaider.transform.position.x - basePos.x > 3f,
                "Raider never sustained +X travel.",
                15f);

            Assert.That(
                NetworkSessionHarness.PlanarAngle(serverRaider.transform.forward, Vector3.back),
                Is.LessThan(5f),
                "Facing followed movement direction - aim and movement are not decoupled.");
            Assert.That(
                Mathf.Abs(serverRaider.transform.position.z - basePos.z),
                Is.LessThan(0.5f),
                "Travel deviated from +X - motion steered by facing.");
        }
    }
}
