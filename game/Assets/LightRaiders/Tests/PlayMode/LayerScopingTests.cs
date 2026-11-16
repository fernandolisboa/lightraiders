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
    /// PlayMode tests for Layer scoping (ADR-0004, ADR-0008): a client observes
    /// only objects in its Raider's Layer. There are no doors yet (#35), so tests
    /// move Raiders through the server-side LayerService directly - the same
    /// entry point doors will use. Assertions read only network-visible state:
    /// Objects.Spawned on each view.
    /// </summary>
    [TestFixture]
    public class LayerScopingTests
    {
        private const int InteriorLayer = 5;

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
        public IEnumerator RaidersInDifferentLayers_DoNotObserveEachOther_UntilTheyShareALayer()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager clientA = _harness.CreateNetworkManager(withSpawner: false);
            clientA.ClientManager.StartConnection();
            NetworkManager clientB = _harness.CreateNetworkManager(withSpawner: false);
            clientB.ClientManager.StartConnection();

            // Positive control: on the shared Surface both clients see both Raiders.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(clientA.ClientManager.Objects.Spawned) == 2
                    && NetworkSessionHarness.CountRaiders(clientB.ClientManager.Objects.Spawned) == 2,
                "Clients never saw both Raiders on the Surface.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(clientA) != null && NetworkSessionHarness.FindOwnedRaider(clientB) != null,
                "Clients never saw their owned Raiders.");

            NetworkObject raiderA = NetworkSessionHarness.FindOwnedRaider(clientA);
            NetworkObject raiderAOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderA.ObjectId);
            NetworkObject raiderB = NetworkSessionHarness.FindOwnedRaider(clientB);
            NetworkObject raiderBOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderB.ObjectId);
            Assert.IsNotNull(raiderAOnServer, "Server view has no instance of A's Raider.");
            Assert.IsNotNull(raiderBOnServer, "Server view has no instance of B's Raider.");

            LayerService layers = server.GetComponent<LayerService>();
            Assert.IsNotNull(layers, "Harness did not add a LayerService to the server manager.");

            // A goes into an interior: A and B stop observing each other; each keeps its own Raider.
            layers.ServerMoveRaider(raiderAOnServer, InteriorLayer);

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderA.ObjectId) == null,
                "Client B still observes A's Raider after A left the Surface (check the LayerCondition on the Raider prefab).",
                15f);
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOnView(clientA.ClientManager.Objects.Spawned, raiderB.ObjectId) == null,
                "Client A still observes B's Raider from inside the interior.",
                15f);
            Assert.IsNotNull(NetworkSessionHarness.FindOwnedRaider(clientA), "A lost its own Raider when changing Layer.");
            Assert.IsNotNull(NetworkSessionHarness.FindOwnedRaider(clientB), "B lost its own Raider when A changed Layer.");
            Assert.That(raiderAOnServer.GetComponent<LayerMember>().ServerLayer, Is.EqualTo(InteriorLayer));

            // B follows A into the interior: they observe each other again.
            layers.ServerMoveRaider(raiderBOnServer, InteriorLayer);

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderA.ObjectId) != null
                    && NetworkSessionHarness.FindOnView(clientA.ClientManager.Objects.Spawned, raiderB.ObjectId) != null,
                "A and B never observed each other after sharing the interior Layer.",
                15f);

            // The replicated Layer reaches clients (the HUD reads it).
            NetworkObject raiderAOnB = NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderA.ObjectId);
            yield return _harness.WaitUntil(
                () => raiderAOnB.GetComponent<LayerMember>().LayerId == InteriorLayer,
                "Client B never received A's replicated Layer id.");
        }

        [UnityTest]
        public IEnumerator Projectile_SpawnsInShootersLayer_AndIsNeverSeenFromAnotherLayer()
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

            NetworkObject raiderA = NetworkSessionHarness.FindOwnedRaider(clientA);
            NetworkObject raiderAOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderA.ObjectId);
            Assert.IsNotNull(raiderAOnServer, "Server view has no instance of A's Raider.");

            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            raiderA.GetComponent<RaiderMovement>().SetIntentProvider(scripted);
            yield return _harness.SettleServerRaider(raiderAOnServer);

            server.GetComponent<LayerService>().ServerMoveRaider(raiderAOnServer, InteriorLayer);
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderA.ObjectId) == null,
                "Client B still observes A after A changed Layer.",
                15f);

            scripted.Intent = new RaiderIntent { AimPoint = raiderAOnServer.transform.position + new Vector3(10f, 0f, 0f), FirePressed = true };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned) >= 1,
                "Fire intent never spawned a projectile on the server.");
            scripted.Intent = new RaiderIntent { AimPoint = raiderAOnServer.transform.position + new Vector3(10f, 0f, 0f) };

            NetworkObject projectile = NetworkSessionHarness.FindProjectile(server.ServerManager.Objects.Spawned);
            Assert.That(projectile.GetComponent<LayerMember>().ServerLayer, Is.EqualTo(InteriorLayer),
                "The projectile did not inherit the shooter's Layer.");

            // Positive control: the shooter's own client (same Layer) sees the shot.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(clientA.ClientManager.Objects.Spawned) >= 1,
                "The shooter's client never saw its own projectile.",
                15f);

            /* Tick-gated hold across the projectile's whole lifetime: client B, on the
             * Surface, must never have observed it. */
            uint holdUntil = server.TimeManager.Tick + Projectile.LifetimeTicks(server.TimeManager);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (server.TimeManager.Tick < holdUntil)
            {
                Assert.That(NetworkSessionHarness.CountProjectiles(clientB.ClientManager.Objects.Spawned), Is.EqualTo(0),
                    "A client on the Surface observed a projectile fired inside an interior Layer.");
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail("Server ticks never advanced during the hold.");
                yield return null;
            }
        }
    }
}
