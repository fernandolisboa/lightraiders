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
    /// PlayMode tests for doors (#35): a predicted, server-validated teleport that
    /// moves a Raider into the linked door's Layer. Each test builds a Surface
    /// floor with a door, and an interior floor far away (separated geometry,
    /// ADR-0008) with the linked door. Spawn A stands on the Surface door; spawn
    /// B stands 4m away, outside the use radius.
    /// </summary>
    [TestFixture]
    public class DoorTests
    {
        private const int InteriorLayer = 3;
        private static readonly Vector3 SurfaceDoorPos = new Vector3(10f, 0f, 10f);
        private static readonly Vector3 InteriorOrigin = new Vector3(300f, 0f, 300f);

        private NetworkSessionHarness _harness;
        private List<GameObject> _sceneObjects;
        private Transform[] _spawns;
        private Door _surfaceDoor;
        private Door _interiorDoor;

        [SetUp]
        public void SetUp()
        {
            _harness = new NetworkSessionHarness();
            _sceneObjects = new List<GameObject>();

            _sceneObjects.Add(MakeFloor("SurfaceFloor", Vector3.zero, 60f));
            _sceneObjects.Add(MakeFloor("InteriorFloor", InteriorOrigin, 20f));

            _surfaceDoor = MakeDoor("SurfaceDoor", SurfaceDoorPos, LayerMember.Surface);
            _interiorDoor = MakeDoor("InteriorDoor", InteriorOrigin, InteriorLayer);
            Door.Link(_surfaceDoor, _interiorDoor);

            GameObject spawnA = new GameObject("TestSpawnA");
            spawnA.transform.position = SurfaceDoorPos + Vector3.up;
            _sceneObjects.Add(spawnA);
            GameObject spawnB = new GameObject("TestSpawnB");
            spawnB.transform.position = SurfaceDoorPos + new Vector3(0f, 1f, 4f);
            _sceneObjects.Add(spawnB);
            _spawns = new Transform[] { spawnA.transform, spawnB.transform };
        }

        private static GameObject MakeFloor(string name, Vector3 topCenter, float size)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.position = topCenter + new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(size, 1f, size);
            return floor;
        }

        private Door MakeDoor(string name, Vector3 position, int layer)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            // Exit faces -X: 2.5m from the door, outside UseRadius, and away from spawn B's +Z offset.
            go.transform.rotation = Quaternion.LookRotation(Vector3.left);
            Door door = go.AddComponent<Door>();
            door.Configure(layer, null, null);
            _sceneObjects.Add(go);
            return door;
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
        public IEnumerator Door_MovesRaiderToLinkedExitAndLayer_AndFollowerRejoinsIt()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager clientA = _harness.CreateNetworkManager(withSpawner: false);
            clientA.ClientManager.StartConnection();
            yield return _harness.WaitUntil(() => NetworkSessionHarness.FindOwnedRaider(clientA) != null, "Client A never saw its owned Raider.");
            // B connects second so PlayerSpawner's round-robin puts it on spawn B.
            NetworkManager clientB = _harness.CreateNetworkManager(withSpawner: false);
            clientB.ClientManager.StartConnection();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(clientB) != null
                    && NetworkSessionHarness.CountRaiders(clientB.ClientManager.Objects.Spawned) == 2,
                "Client B never saw both Raiders.");

            NetworkObject raiderA = NetworkSessionHarness.FindOwnedRaider(clientA);
            NetworkObject raiderAOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderA.ObjectId);
            NetworkObject raiderB = NetworkSessionHarness.FindOwnedRaider(clientB);
            NetworkObject raiderBOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderB.ObjectId);

            ScriptedRaiderIntentProvider scriptedA = new ScriptedRaiderIntentProvider();
            raiderA.GetComponent<RaiderMovement>().SetIntentProvider(scriptedA);
            ScriptedRaiderIntentProvider scriptedB = new ScriptedRaiderIntentProvider();
            raiderB.GetComponent<RaiderMovement>().SetIntentProvider(scriptedB);
            yield return _harness.SettleServerRaider(raiderAOnServer);
            yield return _harness.SettleServerRaider(raiderBOnServer);

            Vector3 interiorExit = _interiorDoor.ExitPosition;

            // A holds interact on the Surface door: server AND owner land at the interior exit, in the interior Layer.
            scriptedA.Intent = new RaiderIntent { Interact = true };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(raiderAOnServer.transform.position, interiorExit) < 0.5f,
                "Server never moved A to the interior door's exit.",
                15f);
            Assert.That(raiderAOnServer.GetComponent<LayerMember>().ServerLayer, Is.EqualTo(InteriorLayer), "A's Layer did not follow the door.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(raiderA.transform.position, interiorExit) < 0.5f,
                "A's own (predicted) view never arrived at the interior exit.",
                15f);

            // Holding interact must not bounce A back: the exit is outside the use radius and the cooldown guards it.
            yield return _harness.WaitForServerTicks(server, 30);
            Assert.That(NetworkSessionHarness.PlanarDistance(raiderAOnServer.transform.position, interiorExit), Is.LessThan(0.5f),
                "Holding interact bounced A back through the door.");
            scriptedA.Intent = new RaiderIntent();

            // B, still on the Surface, stops observing A.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderA.ObjectId) == null,
                "Client B still observes A after A went through the door.",
                15f);

            // B starts out of range: interacting there does nothing (negative control, tick-gated).
            Vector3 bStart = raiderBOnServer.transform.position;
            scriptedB.Intent = new RaiderIntent { Interact = true };
            yield return _harness.WaitForServerTicks(server, 15);
            Assert.That(NetworkSessionHarness.PlanarDistance(raiderBOnServer.transform.position, bStart), Is.LessThan(0.5f),
                "Interacting out of range moved B.");
            Assert.That(raiderBOnServer.GetComponent<LayerMember>().ServerLayer, Is.EqualTo(LayerMember.Surface));

            // B walks to the door (-Z) while holding interact, and follows A through it.
            scriptedB.Intent = new RaiderIntent { Move = new Vector2(0f, -1f), Interact = true };
            yield return _harness.WaitUntil(
                () => raiderBOnServer.GetComponent<LayerMember>().ServerLayer == InteriorLayer,
                "B never followed A through the door.",
                15f);
            scriptedB.Intent = new RaiderIntent();

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderA.ObjectId) != null
                    && NetworkSessionHarness.FindOnView(clientA.ClientManager.Objects.Spawned, raiderB.ObjectId) != null,
                "A and B never observed each other inside the interior.",
                15f);

            // Rejoining observers get the true state, not defaults.
            NetworkObject raiderAOnB = NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderA.ObjectId);
            Health healthOnB = raiderAOnB.GetComponent<Health>();
            Health healthOnServer = raiderAOnServer.GetComponent<Health>();
            yield return _harness.WaitUntil(
                () => healthOnB.Shield == healthOnServer.Shield && healthOnB.CurrentHealth == healthOnServer.CurrentHealth,
                "B's view of A's Shield/Health disagrees with the server after rejoining A's Layer.");
        }
    }
}
