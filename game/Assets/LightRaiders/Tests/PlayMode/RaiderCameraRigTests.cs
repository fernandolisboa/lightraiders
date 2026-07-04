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
    /// PlayMode tests for the per-client camera rig. The default test scene is
    /// empty, so each test builds its own floor and spawn points (no obstacle:
    /// these tests never steer toward one). Follow assertions compare the rig's
    /// implied target point (position minus FollowOffset) against Raider
    /// positions in FULL 3D so a Y-axis follow bug cannot slip through.
    /// </summary>
    [TestFixture]
    public class RaiderCameraRigTests
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
        public IEnumerator Rig_AcquiresAndFollowsOwnedRaiderAfterSpawn()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager client = _harness.CreateNetworkManager(withSpawner: false);

            /* Production order: the generated scene wires the rig before any
             * connection exists, so create it BEFORE the client connects. */
            GameObject rigGo = new GameObject("RaiderCameraRigA");
            _sceneObjects.Add(rigGo);
            RaiderCameraRig rig = rigGo.AddComponent<RaiderCameraRig>();
            rig.SetNetworkManager(client);

            // Structural: camera state is client-local and never networked (ADR-0001).
            Assert.IsNull(rigGo.GetComponent<NetworkObject>(), "Camera rig must not carry a NetworkObject.");
            /* Upcast to MonoBehaviour: a direct 'rig is NetworkBehaviour' check on
             * the sealed component type triggers compiler warning CS0184. */
            Assert.IsFalse((MonoBehaviour)rig is FishNet.Object.NetworkBehaviour, "Camera rig must be a plain MonoBehaviour, not a NetworkBehaviour.");
            // ADR-0001 "angled" top-down: neither horizontal nor straight-down.
            Assert.That(RaiderCameraRig.PitchDegrees, Is.GreaterThan(0f).And.LessThan(90f), "Pitch must be strictly between horizontal and straight-down.");
            Assert.That(
                Quaternion.Angle(rig.transform.rotation, Quaternion.Euler(RaiderCameraRig.PitchDegrees, 0f, 0f)),
                Is.LessThan(0.1f),
                "Rig rotation is not the fixed authored pitch.");

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

            // Acquisition: the rig finds the owned Raider and snaps to its offset.
            yield return _harness.WaitUntil(
                () => Vector3.Distance(RigTargetPoint(rig), ownedRaider.transform.position) < 0.5f,
                "Rig never acquired the owned Raider.");

            Vector3 baseline = ownedRaider.transform.position;
            scripted.Intent = new RaiderIntent { Move = new Vector2(1f, 0f) };

            // 15s: 30Hz ticks plus 2-tick interpolation on the owning client.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, baseline) > 0.5f,
                "Owned Raider never moved in response to intent.",
                15f);

            /* Let the Raider cover real distance before asserting lock: a smooth
             * lag-follower only accrues its steady-state trail (~speed*tau)
             * after sustained motion; asserting at 0.5m of travel would pass
             * any follower that moved at all. */
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(ownedRaider.transform.position, baseline) > 3f,
                "Owned Raider stopped before covering the lock-check distance.",
                15f);

            /* Lock-on while moving, fixed pitch folded into the condition (a
             * tilt-while-moving rig must fail here): per-poll error is
             * ~5*frameDt plus up to one 30Hz tick of interpolation catch-up
             * (~0.17m); any frame under ~65ms satisfies 0.5, and WaitUntil
             * polls every frame. */
            yield return _harness.WaitUntil(
                () => Vector3.Distance(RigTargetPoint(rig), ownedRaider.transform.position) < 0.5f
                    && Quaternion.Angle(rig.transform.rotation, Quaternion.Euler(RaiderCameraRig.PitchDegrees, 0f, 0f)) < 0.1f,
                "Rig lost lock or bent its pitch while the owned Raider moved.");

            scripted.Intent = default;
            yield return WaitUntilStationary(ownedRaider.transform);

            // Regression tripwire: no future LookAt/tilt logic may bend the pitch.
            Assert.That(
                Quaternion.Angle(rig.transform.rotation, Quaternion.Euler(RaiderCameraRig.PitchDegrees, 0f, 0f)),
                Is.LessThan(0.1f),
                "Rig rotation drifted from the fixed authored pitch.");
            Assert.That(
                Vector3.Distance(RigTargetPoint(rig), ownedRaider.transform.position),
                Is.LessThan(0.5f),
                "Rig is not at the follow offset from the stationary Raider.");
        }

        [UnityTest]
        public IEnumerator Rig_NeverFollowsRemoteRaider()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager clientA = _harness.CreateNetworkManager(withSpawner: false);
            clientA.ClientManager.StartConnection();
            NetworkManager clientB = _harness.CreateNetworkManager(withSpawner: false);
            clientB.ClientManager.StartConnection();

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(clientA.ClientManager.Objects.Spawned) == 2
                    && NetworkSessionHarness.CountRaiders(clientB.ClientManager.Objects.Spawned) == 2,
                "Clients never saw both Raiders.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(clientA) != null && NetworkSessionHarness.FindOwnedRaider(clientB) != null,
                "Clients never saw their owned Raiders.");

            NetworkObject raiderA = NetworkSessionHarness.FindOwnedRaider(clientA);
            NetworkObject raiderAOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderA.ObjectId);
            Assert.IsNotNull(raiderAOnServer, "Server view has no instance of A's Raider.");

            NetworkObject raiderB = NetworkSessionHarness.FindOwnedRaider(clientB);
            NetworkObject raiderBOnA = NetworkSessionHarness.FindOnView(clientA.ClientManager.Objects.Spawned, raiderB.ObjectId);
            Assert.IsNotNull(raiderBOnA, "Client A's view has no instance of B's Raider.");
            NetworkObject raiderAOnB = NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderA.ObjectId);
            Assert.IsNotNull(raiderAOnB, "Client B's view has no instance of A's Raider.");
            NetworkObject raiderBOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderB.ObjectId);
            Assert.IsNotNull(raiderBOnServer, "Server view has no instance of B's Raider.");

            /* Zero-intent scripted providers on both OWNED instances before
             * settling, so neither polls the prefab's hardware provider. */
            ScriptedRaiderIntentProvider providerA = new ScriptedRaiderIntentProvider();
            raiderA.GetComponent<RaiderMovement>().SetIntentProvider(providerA);
            ScriptedRaiderIntentProvider providerB = new ScriptedRaiderIntentProvider();
            raiderB.GetComponent<RaiderMovement>().SetIntentProvider(providerB);

            yield return _harness.SettleServerRaider(raiderAOnServer);
            yield return _harness.SettleServerRaider(raiderBOnServer);

            // Rig for client A: it must follow A's Raider and nothing else.
            GameObject rigGo = new GameObject("RaiderCameraRigA");
            _sceneObjects.Add(rigGo);
            RaiderCameraRig rig = rigGo.AddComponent<RaiderCameraRig>();
            rig.SetNetworkManager(clientA);

            /* Second rig on client B. On the second-connecting client the REMOTE
             * Raider enumerates first in the spawned dictionary, so this rig is
             * the one that catches a dropped IsOwner filter regardless of which
             * client wins the connect race. */
            GameObject rigGoB = new GameObject("RaiderCameraRigB");
            _sceneObjects.Add(rigGoB);
            RaiderCameraRig rigB = rigGoB.AddComponent<RaiderCameraRig>();
            rigB.SetNetworkManager(clientB);

            yield return _harness.WaitUntil(
                () => Vector3.Distance(RigTargetPoint(rig), raiderA.transform.position) < 0.5f,
                "Rig A never acquired A's owned Raider.");
            yield return _harness.WaitUntil(
                () => Vector3.Distance(RigTargetPoint(rigB), raiderB.transform.position) < 0.5f,
                "Rig B never acquired B's owned Raider.");

            Vector3 rigBaseline = rig.transform.position;
            Vector3 baselineA = raiderA.transform.position;

            // Both Raiders move, in opposite directions.
            providerA.Intent = new RaiderIntent { Move = new Vector2(1f, 0f) };
            providerB.Intent = new RaiderIntent { Move = new Vector2(-1f, 0f) };

            // Positive control: A's Raider moves, proving ticks/RPCs flow.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(raiderA.transform.position, baselineA) > 0.5f,
                "Positive control failed: A's own Raider never moved.",
                15f);

            // Sustained-motion gate before lock asserts; see the other fixture test.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(raiderA.transform.position, baselineA) > 3f,
                "A's Raider stopped before covering the lock-check distance.",
                15f);

            yield return _harness.WaitUntil(
                () => Vector3.Distance(RigTargetPoint(rig), raiderA.transform.position) < 0.5f
                    && Quaternion.Angle(rig.transform.rotation, Quaternion.Euler(RaiderCameraRig.PitchDegrees, 0f, 0f)) < 0.1f,
                "Rig A lost lock or bent its pitch while A's Raider moved.");
            yield return _harness.WaitUntil(
                () => Vector3.Distance(RigTargetPoint(rigB), raiderB.transform.position) < 0.5f,
                "Rig B lost lock on B's owned Raider while it moved.");

            // Spawns sit ~28m apart and the Raiders are diverging.
            Assert.That(
                NetworkSessionHarness.PlanarDistance(RigTargetPoint(rig), raiderBOnA.transform.position),
                Is.GreaterThan(10f),
                "Rig A is tracking the remote Raider.");
            Assert.That(
                NetworkSessionHarness.PlanarDistance(RigTargetPoint(rigB), raiderAOnB.transform.position),
                Is.GreaterThan(10f),
                "Rig B is tracking the remote Raider.");

            providerA.Intent = default;
            providerB.Intent = default;
            yield return WaitUntilStationary(raiderA.transform);

            /* The rig displaced exactly as much as its own Raider did:
             * PlanarDistance of the two deltas is the planar magnitude of
             * (rigDelta - raiderADelta). */
            Vector3 rigDelta = rig.transform.position - rigBaseline;
            Vector3 raiderADelta = raiderA.transform.position - baselineA;
            Assert.That(
                NetworkSessionHarness.PlanarDistance(rigDelta, raiderADelta),
                Is.LessThan(0.5f),
                "Rig displacement diverged from its own Raider's displacement.");
        }

        /// <summary>
        /// The point the rig is implicitly aimed at: its position minus the
        /// fixed follow offset. Coincides with the followed Raider's root while
        /// the rig is locked on.
        /// </summary>
        private static Vector3 RigTargetPoint(RaiderCameraRig rig) => rig.transform.position - RaiderCameraRig.FollowOffset;

        /// <summary>
        /// Waits until the transform moves less than a centimeter over a fixed
        /// realtime window, so single-sample assertions afterwards are not
        /// frame-rate-sensitive. The window matters: a per-frame epsilon would
        /// trigger mid-motion at very high batchmode frame rates.
        /// </summary>
        private IEnumerator WaitUntilStationary(Transform target)
        {
            const float WindowSeconds = 0.25f;
            float deadline = Time.realtimeSinceStartup + 10f;
            Vector3 anchor = target.position;
            float anchorTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (Time.realtimeSinceStartup - anchorTime < WindowSeconds)
                    continue;
                if (Vector3.Distance(target.position, anchor) < 0.01f)
                    yield break;
                anchor = target.position;
                anchorTime = Time.realtimeSinceStartup;
            }

            Assert.Fail("Transform never came to rest (timed out after 10s).");
        }
    }
}
