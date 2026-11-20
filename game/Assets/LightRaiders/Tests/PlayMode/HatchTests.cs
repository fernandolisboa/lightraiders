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
    /// PlayMode tests for Hatches and the Hatch Key placeholder (#39). One Hatch
    /// on the Surface; spawn A stands on it, spawn B 4 m away (outside reach) as
    /// a second observer. Covers: no key → no channel; damage mid-channel →
    /// cancelled, key kept; uninterrupted channel → key consumed and extracted.
    /// </summary>
    [TestFixture]
    public class HatchTests
    {
        private const string HatchPrefabPath = "Assets/LightRaiders/Prefabs/Hatch.prefab";
        private static readonly Vector3 HatchPos = new Vector3(10f, 0f, 10f);

        private NetworkSessionHarness _harness;
        private List<GameObject> _sceneObjects;
        private Transform[] _spawns;
        private Transform _post;

        [SetUp]
        public void SetUp()
        {
            _harness = new NetworkSessionHarness();
            _sceneObjects = new List<GameObject>();

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "SurfaceFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);
            _sceneObjects.Add(floor);

            GameObject post = new GameObject("HatchPost");
            post.transform.position = HatchPos;
            _sceneObjects.Add(post);
            _post = post.transform;

            GameObject spawnA = new GameObject("TestSpawnA");
            spawnA.transform.position = HatchPos + Vector3.up;
            GameObject spawnB = new GameObject("TestSpawnB");
            spawnB.transform.position = HatchPos + new Vector3(0f, 1f, 4f);
            _sceneObjects.Add(spawnA);
            _sceneObjects.Add(spawnB);
            _spawns = new[] { spawnA.transform, spawnB.transform };
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

        private static Hatch FindHatch(NetworkManager manager) => Hatch.FindNearest(HatchPos, manager, 0.5f);

        [UnityTest]
        public IEnumerator Channel_NeedsAKey_CancelsOnDamageKeepingTheKey_AndCompletesByConsumingItAndExtracting()
        {
            NetworkObject hatchPrefab = null;
#if UNITY_EDITOR
            hatchPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(HatchPrefabPath);
#endif
            Assert.IsNotNull(hatchPrefab, "Hatch prefab missing - run GenerateAll first.");

            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            PostSpawner hatches = server.gameObject.AddComponent<PostSpawner>();
            hatches.SetPrefab(hatchPrefab);
            hatches.SetPosts(new[] { _post }, new[] { LayerMember.Surface });
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");
            hatches.SetNetworkManager(server);

            NetworkManager clientA = _harness.CreateNetworkManager(withSpawner: false);
            GameObject overlayGo = new GameObject("ExtractionClientA");
            _sceneObjects.Add(overlayGo);
            ExtractionClient overlayA = overlayGo.AddComponent<ExtractionClient>();
            clientA.ClientManager.StartConnection();
            overlayA.SetNetworkManager(clientA);
            yield return _harness.WaitUntil(() => NetworkSessionHarness.FindOwnedRaider(clientA) != null, "Client A never saw its owned Raider.");
            NetworkManager clientB = _harness.CreateNetworkManager(withSpawner: false);
            clientB.ClientManager.StartConnection();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(clientB) != null && FindHatch(clientB) != null,
                "Client B never saw its Raider and the Hatch.");

            Hatch hatchOnServer = FindHatch(server);
            Assert.IsNotNull(hatchOnServer);
            Hatch hatchOnB = FindHatch(clientB);

            NetworkObject raiderA = NetworkSessionHarness.FindOwnedRaider(clientA);
            int raiderAId = raiderA.ObjectId;
            NetworkObject raiderAOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderAId);
            HatchKeys keysOnServer = raiderAOnServer.GetComponent<HatchKeys>();
            Assert.That(keysOnServer.Count, Is.EqualTo(HatchKeys.StartingKeys), "A Raider must insert with one key.");
            yield return _harness.WaitUntil(() => raiderA.GetComponent<HatchKeys>().Count == HatchKeys.StartingKeys,
                "The key count never replicated to the owner.");

            ScriptedRaiderIntentProvider scriptedA = new ScriptedRaiderIntentProvider();
            raiderA.GetComponent<RaiderMovement>().SetIntentProvider(scriptedA);
            NetworkSessionHarness.FindOwnedRaider(clientB).GetComponent<RaiderMovement>().SetIntentProvider(new ScriptedRaiderIntentProvider());
            yield return _harness.SettleServerRaider(raiderAOnServer);

            // 1) Without a key, interacting starts nothing (tick-gated negative control).
            keysOnServer.ServerSetCount(0);
            scriptedA.Intent = new RaiderIntent { Interact = true };
            yield return _harness.WaitForServerTicks(server, 20);
            Assert.That(hatchOnServer.IsChannelling, Is.False, "A Raider without a key started a channel.");
            scriptedA.Intent = new RaiderIntent();
            yield return _harness.WaitForServerTicks(server, 5);

            // 2) With a key, a press starts the channel; B observes it.
            keysOnServer.ServerSetCount(1);
            scriptedA.Intent = new RaiderIntent { Interact = true };
            yield return _harness.WaitUntil(() => hatchOnServer.IsChannelling && hatchOnServer.ChannellerId == raiderAId, "A's press never started a channel.");
            scriptedA.Intent = new RaiderIntent();
            yield return _harness.WaitUntil(() => hatchOnB.IsChannelling && hatchOnB.ChannellerId == raiderAId, "B never observed A's channel.");

            // Damage mid-channel cancels it and keeps the key.
            yield return _harness.WaitForServerTicks(server, 10);
            Assert.That(hatchOnServer.IsChannelling, "The channel ended early on its own.");
            raiderAOnServer.GetComponent<Health>().ApplyDamageOnServer(1);
            yield return _harness.WaitUntil(() => !hatchOnServer.IsChannelling, "Damage did not cancel the channel.");
            Assert.That(keysOnServer.Count, Is.EqualTo(1), "A cancelled channel consumed the key.");
            Assert.That(raiderAOnServer.IsSpawned, "A cancelled channel extracted the Raider.");

            // 3) An uninterrupted channel consumes the key and extracts A.
            int keysLeftAtCompletion = -1;
            hatchOnServer.ServerChannelCompleted += (raider, keysLeft) => keysLeftAtCompletion = keysLeft;
            scriptedA.Intent = new RaiderIntent { Interact = true };
            yield return _harness.WaitUntil(() => hatchOnServer.IsChannelling, "A's second press never started a channel.");
            scriptedA.Intent = new RaiderIntent();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderAId) == null,
                "An uninterrupted channel never extracted A.",
                Hatch.ChannelSeconds + 10f);
            Assert.That(keysLeftAtCompletion, Is.EqualTo(0), "Completing the channel did not consume the key.");
            yield return _harness.WaitUntil(() => overlayA.IsExtracted, "A's client never got the Extracted notice.");
            Assert.That(overlayA.LastRoute, Is.EqualTo(ExtractionRoute.Hatch));
            Assert.That(hatchOnServer.IsChannelling, Is.False);
        }
    }
}
