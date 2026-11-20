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
    /// PlayMode tests for the Waygate pair (#37). Gate 1 stands on the Surface,
    /// gate 2 inside an interior Layer far away (separated geometry, ADR-0008), so
    /// a use must teleport AND switch Layer. Spawn A stands on gate 1; spawn B is
    /// 4 m away, outside the use radius, and later walks onto gate 1 during the
    /// shared cooldown to prove the rejection.
    /// </summary>
    [TestFixture]
    public class WaygateTests
    {
        private const string WaygatePrefabPath = "Assets/LightRaiders/Prefabs/Waygate.prefab";
        private const int InteriorLayer = 4;
        private static readonly Vector3 GateOnePos = new Vector3(10f, 0f, 10f);
        private static readonly Vector3 GateTwoPos = new Vector3(300f, 0f, 300f);

        private NetworkSessionHarness _harness;
        private List<GameObject> _sceneObjects;
        private Transform[] _spawns;
        private Transform _postOne;
        private Transform _postTwo;

        [SetUp]
        public void SetUp()
        {
            _harness = new NetworkSessionHarness();
            _sceneObjects = new List<GameObject>
            {
                MakeFloor("SurfaceFloor", Vector3.zero, 60f),
                MakeFloor("InteriorFloor", GateTwoPos, 20f),
            };

            _postOne = MakePost("PostOne", GateOnePos, Vector3.left);
            _postTwo = MakePost("PostTwo", GateTwoPos, Vector3.left);

            GameObject spawnA = new GameObject("TestSpawnA");
            spawnA.transform.position = GateOnePos + Vector3.up;
            GameObject spawnB = new GameObject("TestSpawnB");
            spawnB.transform.position = GateOnePos + new Vector3(0f, 1f, 4f);
            _sceneObjects.Add(spawnA);
            _sceneObjects.Add(spawnB);
            _spawns = new[] { spawnA.transform, spawnB.transform };
        }

        private static GameObject MakeFloor(string name, Vector3 topCenter, float size)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.position = topCenter + new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(size, 1f, size);
            return floor;
        }

        private Transform MakePost(string name, Vector3 position, Vector3 facing)
        {
            GameObject post = new GameObject(name);
            post.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
            _sceneObjects.Add(post);
            return post.transform;
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

        private static Waygate FindGate(NetworkManager manager, Vector3 near)
            => Waygate.FindNearest(near, manager, 0.5f);

        [UnityTest]
        public IEnumerator Use_TeleportsAndSwitchesLayer_ThenSharedCooldownRejectsTheNextUse_OnEveryView()
        {
            NetworkObject waygatePrefab = null;
#if UNITY_EDITOR
            waygatePrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(WaygatePrefabPath);
#endif
            Assert.IsNotNull(waygatePrefab, "Waygate prefab missing - run GenerateAll first.");

            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            WaygateSpawner spawner = server.gameObject.AddComponent<WaygateSpawner>();
            spawner.SetWaygatePrefab(waygatePrefab);
            spawner.SetPosts(_postOne, LayerMember.Surface, _postTwo, InteriorLayer);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");
            spawner.SetNetworkManager(server);

            NetworkManager clientA = _harness.CreateNetworkManager(withSpawner: false);
            clientA.ClientManager.StartConnection();
            yield return _harness.WaitUntil(() => NetworkSessionHarness.FindOwnedRaider(clientA) != null, "Client A never saw its owned Raider.");
            NetworkManager clientB = _harness.CreateNetworkManager(withSpawner: false);
            clientB.ClientManager.StartConnection();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(clientB) != null && FindGate(clientB, GateOnePos) != null,
                "Client B never saw its Raider and the Surface gate.");

            Waygate gateOneOnServer = FindGate(server, GateOnePos);
            Waygate gateTwoOnServer = FindGate(server, GateTwoPos);
            Assert.IsNotNull(gateOneOnServer, "Server has no gate at post one.");
            Assert.IsNotNull(gateTwoOnServer, "Server has no gate at post two.");
            Assert.IsNull(FindGate(clientB, GateTwoPos), "A Surface client observes the interior gate (Layer scoping).");
            Assert.That(gateOneOnServer.IsReady, "The pair starts on cooldown.");

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

            // A uses gate one: lands in front of gate two, in gate two's Layer.
            Vector3 arrival = GateTwoPos + Vector3.left * Waygate.ExitDistance;
            scriptedA.Intent = new RaiderIntent { Interact = true };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(raiderAOnServer.transform.position, arrival) < 0.5f,
                "Server never moved A to gate two's arrival point.",
                15f);
            scriptedA.Intent = new RaiderIntent();
            Assert.That(raiderAOnServer.GetComponent<LayerMember>().ServerLayer, Is.EqualTo(InteriorLayer), "A's Layer did not follow the Waygate.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(raiderA.transform.position, arrival) < 0.5f,
                "A's own view never arrived at gate two.",
                15f);

            // Both gates report the shared cooldown on the server...
            Assert.That(gateOneOnServer.IsReady, Is.False, "Gate one is not on cooldown after a use.");
            Assert.That(gateTwoOnServer.IsReady, Is.False, "Gate two did not share the cooldown.");
            Assert.That(gateOneOnServer.CooldownEndTick, Is.EqualTo(gateTwoOnServer.CooldownEndTick));
            // ...and a second client observes the same cooldown.
            Waygate gateOneOnB = FindGate(clientB, GateOnePos);
            yield return _harness.WaitUntil(
                () => gateOneOnB.CooldownEndTick == gateOneOnServer.CooldownEndTick,
                "Client B never observed the shared cooldown.");
            Assert.That(gateOneOnB.CooldownRemainingSeconds, Is.GreaterThan(Waygate.CooldownSeconds * 0.5f));

            // B walks onto gate one holding interact during the cooldown: rejected.
            scriptedB.Intent = new RaiderIntent { Move = new Vector2(0f, -1f), Interact = true };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(raiderBOnServer.transform.position, GateOnePos) < Waygate.UseRadius * 0.5f,
                "B never reached gate one.",
                15f);
            scriptedB.Intent = new RaiderIntent { Interact = true };
            yield return _harness.WaitForServerTicks(server, 30);
            Assert.That(raiderBOnServer.GetComponent<LayerMember>().ServerLayer, Is.EqualTo(LayerMember.Surface),
                "A use during the shared cooldown was accepted.");
            Assert.That(NetworkSessionHarness.PlanarDistance(raiderBOnServer.transform.position, GateOnePos), Is.LessThan(Waygate.UseRadius),
                "B moved away from gate one although the use was rejected.");
            // B's own view agrees once reconciled (it predicted nothing: its view said 'cooling down').
            Assert.That(NetworkSessionHarness.PlanarDistance(raiderB.transform.position, GateOnePos), Is.LessThan(Waygate.UseRadius + 0.5f));
            scriptedB.Intent = new RaiderIntent();
        }
    }
}
