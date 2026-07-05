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
    /// PlayMode tests for the hostile emitter (issue #17): a stationary,
    /// server-authoritative graybox that fires the shared Projectile at the nearest
    /// Raider within range on a fixed cadence, carrying the Hostile side of the #16
    /// team rule. Every assertion is on network-visible state (replicated
    /// Shield/Health and Objects.Spawned across server + client views); the emitter
    /// is always the delivery mechanism, never a direct damage call. Shares the
    /// harness, the neutral-scripted-provider discipline (so no owned Raider ever
    /// polls real hardware input), and the RaiderIntent struct trap conventions with
    /// RaiderFireTests / RaiderCombatTests.
    /// </summary>
    [TestFixture]
    public class HostileEmitterTests
    {
        /* Duplicated from SessionAssetPaths (same rule as NetworkSessionHarness):
         * this assembly cannot reference the editor-only LightRaiders.Editor asmdef. */
        private const string HostileEmitterPrefabPath = "Assets/LightRaiders/Prefabs/HostileEmitter.prefab";
        private const string DummyTargetPrefabPath = "Assets/LightRaiders/Prefabs/DummyTarget.prefab";

        private NetworkSessionHarness _harness;
        private List<GameObject> _sceneObjects;

        [SetUp]
        public void SetUp()
        {
            _harness = new NetworkSessionHarness();
            _sceneObjects = new List<GameObject>();

            // Top of the floor sits at y=0; wide enough (60x60) for the out-of-range spawns below.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);
            _sceneObjects.Add(floor);
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
        public IEnumerator EmitterFiresAtRaiderInRange_OnCadence()
        {
            // One neutral Raider 5 m from the emitter - comfortably inside the 15 m range.
            NetworkManager server = StartServer(new Vector3(5f, 1f, 0f));
            RaiderHandle raider = new RaiderHandle();
            yield return AddNeutralRaider(server, 1, raider);

            uint periodTicks = HostileEmitter.FirePeriodTicks(server.TimeManager);
            Assert.That(periodTicks, Is.GreaterThanOrEqualTo(2), "Precondition: a fire period below 2 ticks makes the spacing assert vacuous.");

            // Anti-vacuity: nothing has fired before the emitter exists.
            Assert.That(
                NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned),
                Is.EqualTo(0),
                "A projectile existed before the emitter spawned.");

            SpawnEmitterAt(server, new Vector3(0f, 0f, 0f));

            /* FirePeriodSeconds >> one tick, so proving consecutive spawn spacing
             * equals the period proves the emitter did NOT fire on the intervening
             * ticks. Spacing is measured in server-recorded spawn ticks (immune to
             * editor stalls in both directions), exactly as FireHeld_RespectsCooldown
             * measures the Raider weapon. */
            Dictionary<int, uint> spawnTicks = new Dictionary<int, uint>();
            yield return _harness.WaitUntil(() =>
            {
                RecordSpawnTicks(server, spawnTicks);
                return spawnTicks.Count >= 3;
            }, "The emitter never produced three projectiles on the cadence.", 20f);

            // The cadence-limited count replicates to a connected client too.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(raider.Client.ClientManager.Objects.Spawned) >= 1,
                "The observer client never saw an emitter projectile.",
                15f);

            List<uint> sorted = new List<uint>(spawnTicks.Values);
            sorted.Sort();
            for (int i = 1; i < sorted.Count; i++)
            {
                /* Lower bound = the cadence is honored; upper bound = a built-in
                 * positive control that the emitter kept firing exactly when allowed.
                 * +2 covers tick-phase jitter on the in-process transport. */
                Assert.That(
                    sorted[i] - sorted[i - 1],
                    Is.InRange(periodTicks, periodTicks + 2),
                    "Emitter spawn spacing violates the fire period (lower) or the emitter stalled (upper).");
            }
        }

        [UnityTest]
        public IEnumerator EmitterHoldsFire_WhenNoRaiderInRange()
        {
            // The only Raider sits 20 m out - beyond the 15 m range.
            NetworkManager server = StartServer(new Vector3(20f, 1f, 0f));
            RaiderHandle raider = new RaiderHandle();
            yield return AddNeutralRaider(server, 1, raider);

            Assert.That(
                NetworkSessionHarness.PlanarDistance(raider.Server.transform.position, Vector3.zero),
                Is.GreaterThan(HostileEmitter.RangeMeters),
                "Precondition: the lone Raider must start beyond range for the hold to be meaningful.");

            NetworkObject idleEmitter = SpawnEmitterAt(server, new Vector3(0f, 0f, 0f));
            Assert.IsNotNull(idleEmitter, "Emitter failed to spawn.");

            /* Tick-gated hold that also polls: a bug that fires a single shot which
             * then despawns within its lifetime would be missed by an end-only count,
             * so track the max projectile count ever observed across the window. Two
             * full fire periods guarantee a firing emitter would have produced a
             * shot. */
            uint holdTicks = HostileEmitter.FirePeriodTicks(server.TimeManager) * 2 + 5;
            uint targetTick = server.TimeManager.Tick + holdTicks;
            int maxProjectiles = 0;
            yield return _harness.WaitUntil(() =>
            {
                maxProjectiles = Mathf.Max(maxProjectiles, NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned));
                return server.TimeManager.Tick >= targetTick;
            }, "Hold window never completed.", 20f);
            Assert.That(maxProjectiles, Is.EqualTo(0), "The emitter fired at a Raider beyond its range.");

            /* Positive control: a SECOND emitter placed within range of the same
             * Raider does fire - proving the hold above was caused by range, not by an
             * untargetable Raider or a dead fire path. */
            SpawnEmitterAt(server, new Vector3(14f, 0f, 0f));   // 6 m from the Raider at (20,0,0)
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned) >= 1,
                "Positive control failed: an in-range emitter never fired at the same Raider.",
                20f);
        }

        [UnityTest]
        public IEnumerator EmitterTargets_NearestRaider()
        {
            /* Two Raiders on DIFFERENT bearings from the emitter at the origin: near
             * on +X (5 m), far on +Z (12 m), both inside the 15 m range. Different
             * bearings are load-bearing - if they were collinear, a shot aimed at the
             * far Raider would still strike the near one first (opposite side consumes
             * it), making "far untouched" vacuous. */
            NetworkManager server = StartServer(new Vector3(5f, 1f, 0f), new Vector3(0f, 1f, 12f));
            RaiderHandle near = new RaiderHandle();
            yield return AddNeutralRaider(server, 1, near);
            RaiderHandle far = new RaiderHandle();
            yield return AddNeutralRaider(server, 2, far);

            Vector3 emitterPos = new Vector3(0f, 0f, 0f);
            float nearDist = NetworkSessionHarness.PlanarDistance(near.Server.transform.position, emitterPos);
            float farDist = NetworkSessionHarness.PlanarDistance(far.Server.transform.position, emitterPos);
            Assert.That(nearDist, Is.LessThan(HostileEmitter.RangeMeters), "Precondition: the near Raider must be in range.");
            Assert.That(farDist, Is.LessThan(HostileEmitter.RangeMeters), "Precondition: the far Raider must also be in range (else 'targets nearest' is vacuous).");
            Assert.That(nearDist, Is.LessThan(farDist), "Precondition: the near Raider must actually be nearer.");

            SpawnEmitterAt(server, emitterPos);

            // The nearer Raider takes damage (positive control that the emitter fired).
            yield return _harness.WaitUntil(
                () => near.ServerHealth.Shield < Health.MaxShield,
                "The nearest Raider never took damage - the emitter did not target it.",
                20f);

            /* Hold a full period and confirm the farther Raider was never touched:
             * Health never regenerates, so a single stray hit would leave a permanent
             * mark this catches. */
            yield return _harness.WaitForServerTicks(server, HostileEmitter.FirePeriodTicks(server.TimeManager) + 5);
            Assert.That(far.ServerHealth.Shield, Is.EqualTo(Health.MaxShield), "The emitter damaged the farther Raider's Shield - it did not target the nearest.");
            Assert.That(far.ServerHealth.CurrentHealth, Is.EqualTo(Health.MaxHealth), "The emitter damaged the farther Raider's Health - it did not target the nearest.");
        }

        [UnityTest]
        public IEnumerator EmitterHit_DepletesRaiderShieldThenHealth_AcrossViews()
        {
            /* Victim 5 m from the emitter (owned by the first client); a second
             * Raider 25 m out (owned by the OBSERVER client) so it is never the
             * target. The observer client replicates the victim as a non-owned body -
             * that is the third view the AC requires, alongside the server and the
             * victim's own client. */
            NetworkManager server = StartServer(new Vector3(5f, 1f, 0f), new Vector3(25f, 1f, 0f));
            RaiderHandle victim = new RaiderHandle();
            yield return AddNeutralRaider(server, 1, victim);
            RaiderHandle observerRaider = new RaiderHandle();
            yield return AddNeutralRaider(server, 2, observerRaider);

            NetworkManager observerClient = observerRaider.Client;
            int victimId = victim.Owned.ObjectId;

            // The observer client must have replicated the victim before we baseline it.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOnView(observerClient.ClientManager.Objects.Spawned, victimId) != null,
                "The observer client never replicated the victim Raider.",
                15f);
            Health observerVictim = NetworkSessionHarness.FindOnView(observerClient.ClientManager.Objects.Spawned, victimId).GetComponent<Health>();
            Health ownVictim = victim.Owned.GetComponent<Health>();   // the victim's own client view

            // Anti-vacuity baselines on all three views BEFORE the emitter exists.
            Assert.That(victim.ServerHealth.Shield, Is.EqualTo(Health.MaxShield), "Victim did not start at full Shield on the server.");
            Assert.That(victim.ServerHealth.CurrentHealth, Is.EqualTo(Health.MaxHealth), "Victim did not start at full Health on the server.");
            yield return _harness.WaitUntil(
                () => observerVictim.Shield == Health.MaxShield && observerVictim.CurrentHealth == Health.MaxHealth,
                "The observer never saw the victim at full Shield/Health.",
                15f);
            yield return _harness.WaitUntil(
                () => ownVictim.Shield == Health.MaxShield && ownVictim.CurrentHealth == Health.MaxHealth,
                "The victim's own view never showed full Shield/Health.",
                15f);

            SpawnEmitterAt(server, new Vector3(0f, 0f, 0f));

            /* Shield-before-Health as a stall-immune invariant (same technique as
             * RaiderCombatTests.FireAtTarget): the moment server Health first drops,
             * the Shield is already fully gone. A health-first bug fails this at
             * once. */
            yield return _harness.WaitUntil(() => victim.ServerHealth.CurrentHealth < Health.MaxHealth, "Victim Health never dropped on the server.", 20f);
            Assert.That(victim.ServerHealth.Shield, Is.EqualTo(0), "Health dropped while the Shield remained - not Shield-before-Health.");

            // The depletion replicates to the observer client...
            yield return _harness.WaitUntil(
                () => observerVictim.Shield == 0 && observerVictim.CurrentHealth < Health.MaxHealth,
                "The observer never saw the victim's depleted Shield/Health.",
                15f);
            // ...and to the victim's own client view.
            yield return _harness.WaitUntil(
                () => ownVictim.Shield == 0 && ownVictim.CurrentHealth < Health.MaxHealth,
                "The victim's own view never saw its depleted Shield/Health.",
                15f);
        }

        [UnityTest]
        public IEnumerator EmitterShots_DoNotDamageHostileSideObjects()
        {
            // One Raider 10 m out on +X, the only valid target.
            NetworkManager server = StartServer(new Vector3(10f, 1f, 0f));
            RaiderHandle victim = new RaiderHandle();
            yield return AddNeutralRaider(server, 1, victim);

            /* A Hostile-side dummy target squarely between the emitter and the Raider:
             * the shot must pass THROUGH it (friendly fire off) and still reach the
             * Raider. If the team rule were broken, the emitter would either target
             * the dummy or its shot would consume on it. */
            NetworkObject dummy = SpawnTargetAt(server, new Vector3(5f, 0f, 0f));
            Health dummyHealth = dummy.GetComponent<Health>();
            Assert.That(dummyHealth.Side, Is.EqualTo(Side.Hostile), "Precondition: the dummy target must be Hostile-side.");

            NetworkObject emitter = SpawnEmitterAt(server, new Vector3(0f, 0f, 0f));
            Health emitterHealth = emitter.GetComponent<Health>();
            Assert.That(emitterHealth.Side, Is.EqualTo(Side.Hostile), "Precondition: the emitter must be Hostile-side.");

            /* Drive it past the Shield into Health (>= 3 hits), so the shots have
             * demonstrably traveled through the Hostile dummy several times. Health
             * never regenerates, so a single stray friendly hit on the dummy or the
             * emitter would leave a permanent mark the end asserts catch. */
            yield return _harness.WaitUntil(
                () => victim.ServerHealth.CurrentHealth < Health.MaxHealth,
                "The emitter never damaged the Raider through the friendly Hostile target - team rule or aiming broke.",
                20f);

            Assert.That(dummyHealth.Shield, Is.EqualTo(Health.MaxShield), "An emitter shot depleted a friendly Hostile target's Shield.");
            Assert.That(dummyHealth.CurrentHealth, Is.EqualTo(Health.MaxHealth), "An emitter shot damaged a friendly Hostile target's Health.");
            Assert.That(emitterHealth.Shield, Is.EqualTo(Health.MaxShield), "An emitter shot depleted the firing emitter's own Shield.");
            Assert.That(emitterHealth.CurrentHealth, Is.EqualTo(Health.MaxHealth), "An emitter shot damaged the firing emitter's own Health.");
        }

        // ----- helpers -----

        /// <summary>Handles for a settled, neutral Raider owned by one client, resolved on every view.</summary>
        private sealed class RaiderHandle
        {
            public NetworkManager Client;
            public NetworkObject Owned;         // the client's owned Raider instance
            public NetworkObject Server;        // the server-view instance
            public Health ServerHealth;         // the server-view Health (server truth)
            public ScriptedRaiderIntentProvider Scripted;
        }

        /// <summary>Starts a server whose PlayerSpawner assigns the given spawn points in connection order.</summary>
        private NetworkManager StartServer(params Vector3[] spawnPositions)
        {
            Transform[] spawns = new Transform[spawnPositions.Length];
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                GameObject go = new GameObject("TestSpawn" + i);
                go.transform.position = spawnPositions[i];
                _sceneObjects.Add(go);
                spawns[i] = go.transform;
            }

            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: spawns);
            server.ServerManager.StartConnection();
            // The Started flag is polled by the first AddNeutralRaider; a bare start here would race the first client's connect.
            return server;
        }

        /// <summary>
        /// Connects one client, waits until the server holds <paramref
        /// name="expectedServerRaiderCount"/> Raiders (so PlayerSpawner's
        /// connection-order spawn assignment is deterministic), injects a neutral
        /// scripted provider, settles the server capsule onto the floor, and resolves
        /// the handle across views. Callers invoke this sequentially with increasing
        /// counts, exactly like the RaiderFireTests two-client pattern.
        /// </summary>
        private IEnumerator AddNeutralRaider(NetworkManager server, int expectedServerRaiderCount, RaiderHandle handle)
        {
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            handle.Client = _harness.CreateNetworkManager(withSpawner: false);
            handle.Client.ClientManager.StartConnection();

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == expectedServerRaiderCount,
                "Server never saw " + expectedServerRaiderCount + " Raider(s).");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(handle.Client) != null,
                "Client never saw its owned Raider.");

            handle.Owned = NetworkSessionHarness.FindOwnedRaider(handle.Client);
            handle.Server = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, handle.Owned.ObjectId);
            Assert.IsNotNull(handle.Server, "Server view has no instance for the client-owned Raider.");
            handle.ServerHealth = handle.Server.GetComponent<Health>();
            Assert.IsNotNull(handle.ServerHealth, "The Raider carries no Health - #17 makes Raiders damageable.");

            // Neutral provider before anything else, so the owned Raider never polls real hardware input.
            handle.Scripted = new ScriptedRaiderIntentProvider();
            handle.Owned.GetComponent<RaiderMovement>().SetIntentProvider(handle.Scripted);

            yield return _harness.SettleServerRaider(handle.Server);
        }

        /// <summary>Server-spawns a HostileEmitter at the given position and returns the server instance.</summary>
        private NetworkObject SpawnEmitterAt(NetworkManager server, Vector3 position)
        {
            NetworkObject nob = server.GetPooledInstantiated(LoadEmitterPrefab(), position, Quaternion.identity, true);
            server.ServerManager.Spawn(nob);
            return nob;
        }

        /// <summary>Server-spawns a DummyTarget (Side.Hostile) at the given position and returns the server instance.</summary>
        private NetworkObject SpawnTargetAt(NetworkManager server, Vector3 position)
        {
            NetworkObject nob = server.GetPooledInstantiated(LoadTargetPrefab(), position, Quaternion.identity, true);
            server.ServerManager.Spawn(nob);
            return nob;
        }

        private NetworkObject LoadEmitterPrefab() => LoadPrefab(HostileEmitterPrefabPath, "HostileEmitter");

        private NetworkObject LoadTargetPrefab() => LoadPrefab(DummyTargetPrefabPath, "DummyTarget");

        private static NetworkObject LoadPrefab(string path, string label)
        {
            NetworkObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
#endif
            if (prefab == null)
                Assert.Fail(label + " prefab missing — run 'Light Raiders > Generate Session Assets' or the documented -executeMethod first.");
            return prefab;
        }

        /// <summary>
        /// Records the SpawnTick of every projectile currently in the server view,
        /// keyed by ObjectId, so despawns never lose an already-counted spawn. Called
        /// from inside WaitUntil polls (same helper shape as RaiderFireTests).
        /// </summary>
        private static void RecordSpawnTicks(NetworkManager server, Dictionary<int, uint> spawnTicks)
        {
            foreach (NetworkObject networkObject in server.ServerManager.Objects.Spawned.Values)
            {
                if (networkObject == null)
                    continue;
                Projectile projectile = networkObject.GetComponent<Projectile>();
                if (projectile != null)
                    spawnTicks[networkObject.ObjectId] = projectile.SpawnTick;
            }
        }
    }
}
