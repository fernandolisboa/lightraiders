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
    /// PlayMode tests for Exfil and the shared extraction path (#38). One Exfil
    /// site on the Surface; spawn A stands on it, spawn B stands 20 m away -
    /// outside the 8 m zone. The countdown is shortened on the server instance so
    /// the test stays fast; the 45 s default is a tuning constant, not logic.
    /// </summary>
    [TestFixture]
    public class ExtractionTests
    {
        private const string ExfilPrefabPath = "Assets/LightRaiders/Prefabs/ExfilSite.prefab";
        private const float TestCountdownSeconds = 2f;
        private static readonly Vector3 SitePos = new Vector3(10f, 0f, 10f);

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

            GameObject post = new GameObject("ExfilPost");
            post.transform.position = SitePos;
            _sceneObjects.Add(post);
            _post = post.transform;

            GameObject spawnA = new GameObject("TestSpawnA");
            spawnA.transform.position = SitePos + Vector3.up;
            GameObject spawnB = new GameObject("TestSpawnB");
            spawnB.transform.position = SitePos + new Vector3(0f, 1f, -20f);
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

        private static ExfilSite FindSite(NetworkManager manager) => ExfilSite.FindNearest(SitePos, manager, 0.5f);

        [UnityTest]
        public IEnumerator Call_CountsDownForEverySurfaceObserver_ThenExtractsOnlyRaidersInTheZone_AndRejoinRespawns()
        {
            NetworkObject exfilPrefab = null;
#if UNITY_EDITOR
            exfilPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(ExfilPrefabPath);
#endif
            Assert.IsNotNull(exfilPrefab, "ExfilSite prefab missing - run GenerateAll first.");

            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            PostSpawner sites = server.gameObject.AddComponent<PostSpawner>();
            sites.SetPrefab(exfilPrefab);
            sites.SetPosts(new[] { _post }, new[] { LayerMember.Surface });
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");
            sites.SetNetworkManager(server);

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
                () => NetworkSessionHarness.FindOwnedRaider(clientB) != null && FindSite(clientB) != null && FindSite(clientA) != null,
                "A client never saw its Raider and the Exfil site.");

            ExfilSite siteOnServer = FindSite(server);
            Assert.IsNotNull(siteOnServer);
            Assert.That(siteOnServer.State, Is.EqualTo(ExfilState.Idle));
            siteOnServer.ServerSetCountdownSeconds(TestCountdownSeconds);

            NetworkObject raiderA = NetworkSessionHarness.FindOwnedRaider(clientA);
            int raiderAId = raiderA.ObjectId;
            NetworkObject raiderAOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderAId);
            NetworkObject raiderB = NetworkSessionHarness.FindOwnedRaider(clientB);
            NetworkObject raiderBOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderB.ObjectId);
            ScriptedRaiderIntentProvider scriptedA = new ScriptedRaiderIntentProvider();
            raiderA.GetComponent<RaiderMovement>().SetIntentProvider(scriptedA);
            raiderB.GetComponent<RaiderMovement>().SetIntentProvider(new ScriptedRaiderIntentProvider());
            yield return _harness.SettleServerRaider(raiderAOnServer);
            yield return _harness.SettleServerRaider(raiderBOnServer);

            // A calls the site; the countdown is visible to every Surface observer.
            scriptedA.Intent = new RaiderIntent { Interact = true };
            yield return _harness.WaitUntil(() => siteOnServer.State == ExfilState.Called, "A's interact never called the site.");
            uint departTick = siteOnServer.DepartTick;
            ExfilSite siteOnB = FindSite(clientB);
            yield return _harness.WaitUntil(
                () => siteOnB.State == ExfilState.Called && siteOnB.DepartTick == departTick,
                "Client B never observed the call and its countdown.");

            // Calling a site that is already counting down does nothing (A still holds interact).
            yield return _harness.WaitForServerTicks(server, 10);
            Assert.That(siteOnServer.DepartTick, Is.EqualTo(departTick), "A second call restarted the countdown.");
            scriptedA.Intent = new RaiderIntent();

            // Departure: A (in the zone) extracts everywhere; B (20 m out) stays.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderAId) == null,
                "A was never extracted on the server.",
                TestCountdownSeconds + 10f);
            Assert.That(siteOnServer.State, Is.EqualTo(ExfilState.Departing));
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderAId) == null
                    && NetworkSessionHarness.FindOwnedRaider(clientA) == null,
                "A's despawn never reached the clients.");
            yield return _harness.WaitUntil(() => overlayA.IsExtracted, "A's client never got the Extracted notice.");
            Assert.That(overlayA.LastRoute, Is.EqualTo(ExtractionRoute.Exfil));
            Assert.That(raiderBOnServer != null && raiderBOnServer.IsSpawned, "B, outside the zone, was extracted too.");
            Assert.IsNotNull(NetworkSessionHarness.FindOwnedRaider(clientB), "B lost its Raider.");

            // Debug rejoin: A gets a fresh Raider on the Surface and the overlay clears.
            overlayA.RequestRejoin();
            yield return _harness.WaitUntil(() => NetworkSessionHarness.FindOwnedRaider(clientA) != null, "A's rejoin request never respawned it.");
            NetworkObject rejoined = NetworkSessionHarness.FindOwnedRaider(clientA);
            NetworkObject rejoinedOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, rejoined.ObjectId);
            Assert.That(rejoinedOnServer.GetComponent<LayerMember>().ServerLayer, Is.EqualTo(LayerMember.Surface));
            yield return _harness.WaitUntil(() => !overlayA.IsExtracted, "The Extracted overlay never cleared after rejoining.");
        }
    }
}
