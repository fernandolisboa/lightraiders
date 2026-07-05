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
    /// PlayMode tests for the damage model (issue #16): Shield-before-Health
    /// depletion, no regeneration, target death + respawn, and friendly fire off.
    /// Every assertion is on network-visible state (replicated Shield/Health and
    /// Objects.Spawned across views); nothing calls a damage method directly - the
    /// projectile is always the delivery mechanism. Shares the +X-shooter and
    /// struct-trap conventions with RaiderFireTests.
    /// </summary>
    [TestFixture]
    public class RaiderCombatTests
    {
        /* Duplicated from SessionAssetPaths (same rule as NetworkSessionHarness):
         * this assembly cannot reference the editor-only LightRaiders.Editor asmdef. */
        private const string DummyTargetPrefabPath = "Assets/LightRaiders/Prefabs/DummyTarget.prefab";

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
        public IEnumerator FireAtTarget_DepletesShieldThenHealth()
        {
            Shooter shooter = new Shooter();
            yield return ArrangePlusXShooter(shooter);

            NetworkObject serverTarget = SpawnTargetAt(shooter.Server, new Vector3(shooter.Muzzle.x + 4f, 0f, shooter.Muzzle.z));
            Health serverHealth = serverTarget.GetComponent<Health>();

            // Baseline: full bars on the server.
            Assert.That(serverHealth.Shield, Is.EqualTo(Health.MaxShield), "Target did not start at full Shield.");
            Assert.That(serverHealth.CurrentHealth, Is.EqualTo(Health.MaxHealth), "Target did not start at full Health.");

            // Observer sees the target at full bars (anti-vacuity for the client asserts below).
            yield return _harness.WaitUntil(
                () => TargetOnView(shooter.Client, out Health h) && h.Shield == Health.MaxShield && h.CurrentHealth == Health.MaxHealth,
                "Observer never saw the target at full Shield/Health.",
                15f);

            // Autofire at the target.
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim, FirePressed = true };

            /* Shield-before-Health as a stall-immune invariant: observe the moment
             * Health first drops and assert the Shield is already fully gone. Health
             * only ever falls once the Shield is 0, so this holds however a hit
             * spills across the boundary, and needs no transient (Shield==0 while
             * Health==Max) state to be caught between frame polls. A health-first
             * bug drops Health while Shield > 0 and fails this immediately. */
            yield return _harness.WaitUntil(() => serverHealth.CurrentHealth < Health.MaxHealth, "Target Health never dropped.", 15f);
            Assert.That(serverHealth.Shield, Is.EqualTo(0), "Health dropped while the Shield remained - not Shield-before-Health.");
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim };

            // The depletion replicates to the observer.
            yield return _harness.WaitUntil(
                () => TargetOnView(shooter.Client, out Health h) && h.Shield == 0 && h.CurrentHealth < Health.MaxHealth,
                "Observer never saw the target's depleted Shield/Health.",
                15f);
        }

        [UnityTest]
        public IEnumerator TargetBars_DoNotRegenerate()
        {
            Shooter shooter = new Shooter();
            yield return ArrangePlusXShooter(shooter);

            NetworkObject serverTarget = SpawnTargetAt(shooter.Server, new Vector3(shooter.Muzzle.x + 4f, 0f, shooter.Muzzle.z));
            Health serverHealth = serverTarget.GetComponent<Health>();

            // Damage past the Shield into Health, then stop firing.
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim, FirePressed = true };
            yield return _harness.WaitUntil(() => serverHealth.CurrentHealth < Health.MaxHealth, "Target Health never dropped.", 15f);
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim };

            // Let every in-flight shot resolve so nothing damages the target during the hold.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(shooter.Server.ServerManager.Objects.Spawned) == 0,
                "In-flight projectiles never cleared.",
                15f);

            int heldShield = serverHealth.Shield;
            int heldHealth = serverHealth.CurrentHealth;
            // Anti-vacuity: the bars really are damaged, so "unchanged" is a meaningful hold.
            Assert.That(heldHealth, Is.LessThan(Health.MaxHealth), "Precondition: Health should be damaged before the no-regen hold.");

            // Tick-gated hold: neither bar rises (nor changes) on its own.
            yield return _harness.WaitForServerTicks(shooter.Server, 30);
            Assert.That(serverHealth.Shield, Is.EqualTo(heldShield), "Shield changed with no new damage (regenerated?).");
            Assert.That(serverHealth.CurrentHealth, Is.EqualTo(heldHealth), "Health changed with no new damage (regenerated?).");

            // Same held state on the observer, held across ticks.
            Assert.That(TargetOnView(shooter.Client, out Health clientHealth), Is.True, "Observer has no instance of the target.");
            yield return _harness.WaitUntil(
                () => clientHealth.Shield == heldShield && clientHealth.CurrentHealth == heldHealth,
                "Observer never converged on the held Shield/Health.",
                15f);
            yield return _harness.WaitForServerTicks(shooter.Server, 15);
            Assert.That(clientHealth.Shield, Is.EqualTo(heldShield), "Observer Shield changed during the hold.");
            Assert.That(clientHealth.CurrentHealth, Is.EqualTo(heldHealth), "Observer Health changed during the hold.");
        }

        [UnityTest]
        public IEnumerator TargetAtZeroHealth_DespawnsAndRespawns()
        {
            Shooter shooter = new Shooter();
            yield return ArrangePlusXShooter(shooter);

            // A spawner owning one post directly in the +X lane.
            Vector3 postPosition = new Vector3(shooter.Muzzle.x + 4f, 0f, shooter.Muzzle.z);
            GameObject postGo = new GameObject("TestTargetPost");
            postGo.transform.position = postPosition;
            _sceneObjects.Add(postGo);

            GameObject spawnerGo = new GameObject("TestTargetSpawner");
            _sceneObjects.Add(spawnerGo);
            DummyTargetSpawner spawner = spawnerGo.AddComponent<DummyTargetSpawner>();
            spawner.SetTargetPrefab(LoadTargetPrefab());
            spawner.SetPosts(new Transform[] { postGo.transform });
            spawner.SetNetworkManager(shooter.Server);   // server already started -> spawns immediately

            // Target appears on server and observer.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountDamageables(shooter.Server.ServerManager.Objects.Spawned) == 1,
                "Spawner never spawned a target on the server.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountDamageables(shooter.Client.ClientManager.Objects.Spawned) == 1,
                "Observer never saw the spawned target.",
                15f);

            // Fire until it dies and despawns everywhere.
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim, FirePressed = true };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountDamageables(shooter.Server.ServerManager.Objects.Spawned) == 0,
                "Target never despawned at zero Health.",
                15f);
            uint despawnObservedTick = shooter.Server.TimeManager.Tick;
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountDamageables(shooter.Client.ClientManager.Objects.Spawned) == 0,
                "Target despawn never reached the observer.",
                15f);

            // Then it respawns at the post, at full Health, on all views.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountDamageables(shooter.Server.ServerManager.Objects.Spawned) == 1,
                "Target never respawned on the server.",
                15f);
            uint respawnObservedTick = shooter.Server.TimeManager.Tick;

            /* Delayed, not instant: the gap between observing despawn and observing
             * respawn is at least half the respawn delay. Stall-immune in both
             * directions - a tick catch-up burst only inflates the observed gap,
             * and the half-delay margin absorbs poll lag on the despawn side while
             * still catching an instant-respawn regression. */
            uint respawnTicks = DummyTargetSpawner.RespawnTicks(shooter.Server.TimeManager);
            Assert.That(
                respawnObservedTick - despawnObservedTick,
                Is.GreaterThanOrEqualTo(respawnTicks / 2),
                "Target respawned too soon - the respawn delay was not honored.");

            NetworkObject respawned = NetworkSessionHarness.FindDamageable(shooter.Server.ServerManager.Objects.Spawned);
            Assert.That(
                NetworkSessionHarness.PlanarDistance(respawned.transform.position, postPosition),
                Is.LessThan(0.1f),
                "Target did not respawn at its post.");
            Health respawnedHealth = respawned.GetComponent<Health>();
            Assert.That(respawnedHealth.Shield, Is.EqualTo(Health.MaxShield), "Respawned target did not have full Shield.");
            Assert.That(respawnedHealth.CurrentHealth, Is.EqualTo(Health.MaxHealth), "Respawned target did not have full Health.");

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountDamageables(shooter.Client.ClientManager.Objects.Spawned) == 1,
                "Respawn never reached the observer.",
                15f);
        }

        [UnityTest]
        public IEnumerator RaiderFire_PassesThroughRaider_AndDamagesTargetBehind()
        {
            /* Custom spawns so a SECOND Raider stands squarely in the shooter's +X
             * lane: PlayerSpawner assigns Spawns[0] to the first client (the
             * shooter) and Spawns[1] to the second (the blocker). */
            GameObject shooterSpawnGo = new GameObject("TestShooterSpawn");
            shooterSpawnGo.transform.position = new Vector3(10f, 1f, 10f);
            _sceneObjects.Add(shooterSpawnGo);
            GameObject blockerSpawnGo = new GameObject("TestBlockerSpawn");
            blockerSpawnGo.transform.position = new Vector3(14f, 1f, 10f);
            _sceneObjects.Add(blockerSpawnGo);
            Transform[] laneSpawns = { shooterSpawnGo.transform, blockerSpawnGo.transform };

            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true, spawns: laneSpawns);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager clientA = _harness.CreateNetworkManager(withSpawner: false);
            clientA.ClientManager.StartConnection();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 1,
                "Server never saw the shooter Raider.");
            NetworkManager clientB = _harness.CreateNetworkManager(withSpawner: false);
            clientB.ClientManager.StartConnection();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 2,
                "Server never saw both Raiders.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(clientA) != null && NetworkSessionHarness.FindOwnedRaider(clientB) != null,
                "Clients never saw their owned Raiders.");

            NetworkObject shooterOwned = NetworkSessionHarness.FindOwnedRaider(clientA);
            NetworkObject shooterServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, shooterOwned.ObjectId);
            NetworkObject blockerOwned = NetworkSessionHarness.FindOwnedRaider(clientB);
            NetworkObject blockerServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, blockerOwned.ObjectId);
            Assert.IsNotNull(shooterServer, "Server view has no instance of the shooter Raider.");
            Assert.IsNotNull(blockerServer, "Server view has no instance of the blocker Raider.");

            // Neutral scripted providers on both owned instances so hardware never fires.
            ScriptedRaiderIntentProvider shooterScripted = new ScriptedRaiderIntentProvider();
            shooterOwned.GetComponent<RaiderMovement>().SetIntentProvider(shooterScripted);
            ScriptedRaiderIntentProvider blockerScripted = new ScriptedRaiderIntentProvider();
            blockerOwned.GetComponent<RaiderMovement>().SetIntentProvider(blockerScripted);

            yield return _harness.SettleServerRaider(shooterServer);
            yield return _harness.SettleServerRaider(blockerServer);

            // Precondition: spawn assignment is as intended; fail loudly (not vacuously) if it ever changes.
            Assert.That(
                NetworkSessionHarness.PlanarDistance(shooterServer.transform.position, new Vector3(10f, 0f, 10f)),
                Is.LessThan(1f),
                "Shooter Raider is not at the expected spawn (PlayerSpawner assignment changed?).");
            Assert.That(
                NetworkSessionHarness.PlanarDistance(blockerServer.transform.position, new Vector3(14f, 0f, 10f)),
                Is.LessThan(1f),
                "Blocker Raider is not at the expected spawn (PlayerSpawner assignment changed?).");

            // Converge the shooter's facing on +X.
            Vector3 basePos = shooterServer.transform.position;
            Vector3 aim = basePos + new Vector3(10f, 0f, 0f);
            shooterScripted.Intent = new RaiderIntent { AimPoint = aim };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarAngle(shooterServer.transform.forward, aim - basePos) < 2f,
                "Shooter facing never converged on +X.");

            Vector3 muzzle = shooterServer.transform.position
                + shooterServer.transform.forward * RaiderWeapon.MuzzleForwardOffset
                + Vector3.up * RaiderWeapon.MuzzleHeight;

            // Target BEYOND the blocker in the same lane.
            NetworkObject serverTarget = SpawnTargetAt(server, new Vector3(muzzle.x + 6f, 0f, muzzle.z));
            Health targetHealth = serverTarget.GetComponent<Health>();

            // Precondition: the blocker genuinely sits between the muzzle and the target.
            Assert.That(
                blockerServer.transform.position.x,
                Is.InRange(muzzle.x, muzzle.x + 6f),
                "Blocker Raider is not between the muzzle and the target - the pass-through is not being exercised.");
            Assert.That(
                Mathf.Abs(blockerServer.transform.position.z - muzzle.z),
                Is.LessThan(0.5f),
                "Blocker Raider is off the shot lane - the pass-through would be vacuous.");
            Assert.That(targetHealth.Shield, Is.EqualTo(Health.MaxShield), "Target did not start at full Shield.");

            /* Fire: the shot must pass THROUGH the friendly Raider and damage the
             * target behind it. If friendly fire were on, the Raider would consume
             * the shot and the target would never be touched. */
            shooterScripted.Intent = new RaiderIntent { AimPoint = aim, FirePressed = true };
            yield return _harness.WaitUntil(
                () => targetHealth.Shield < Health.MaxShield,
                "Target behind a friendly Raider never took damage - the shot was consumed by the Raider.",
                15f);
            shooterScripted.Intent = new RaiderIntent { AimPoint = aim };

            // The friendly Raider is untouched: both Raiders still spawned, and Raiders carry no Health this slice.
            Assert.That(
                NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned),
                Is.EqualTo(2),
                "A Raider despawned - the shot affected a friendly.");
            Assert.IsNull(
                blockerServer.GetComponent<Health>(),
                "Precondition: Raiders carry no Health this slice, so 'no friendly damage' is proven by the pass-through.");
        }

        /// <summary>Handles for a settled, +X-facing single shooter shared by the combat tests.</summary>
        private sealed class Shooter
        {
            public NetworkManager Server;
            public NetworkManager Client;
            public NetworkObject ServerRaider;
            public ScriptedRaiderIntentProvider Scripted;
            public Vector3 Aim;      // carried in every intent - the RaiderIntent struct trap resets AimPoint otherwise
            public Vector3 Muzzle;   // server-truth muzzle, computed after facing converged
        }

        /// <summary>
        /// Server + one client, the owned Raider driven by a scripted provider,
        /// settled and converged to face due +X. Converges tighter (2 deg) than the
        /// fire tests so the ~0.5 m-radius target capsule at ~4-6 m is a reliable hit.
        /// </summary>
        private IEnumerator ArrangePlusXShooter(Shooter shooter)
        {
            shooter.Server = _harness.CreateNetworkManager(withSpawner: true, spawns: _spawns);
            shooter.Server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => shooter.Server.ServerManager.Started, "Server did not start.");

            shooter.Client = _harness.CreateNetworkManager(withSpawner: false);
            shooter.Client.ClientManager.StartConnection();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(shooter.Server.ServerManager.Objects.Spawned) == 1,
                "Server never saw the Raider spawn.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.FindOwnedRaider(shooter.Client) != null,
                "Client never saw its owned Raider.");

            NetworkObject ownedRaider = NetworkSessionHarness.FindOwnedRaider(shooter.Client);
            shooter.ServerRaider = NetworkSessionHarness.FindOnView(shooter.Server.ServerManager.Objects.Spawned, ownedRaider.ObjectId);
            Assert.IsNotNull(shooter.ServerRaider, "Server view has no instance for the client-owned Raider.");

            shooter.Scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(shooter.Scripted);

            yield return _harness.SettleServerRaider(shooter.ServerRaider);

            Vector3 basePos = shooter.ServerRaider.transform.position;
            shooter.Aim = basePos + new Vector3(10f, 0f, 0f);
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarAngle(shooter.ServerRaider.transform.forward, shooter.Aim - basePos) < 2f,
                "Server facing never converged on the aim point.");

            shooter.Muzzle = shooter.ServerRaider.transform.position
                + shooter.ServerRaider.transform.forward * RaiderWeapon.MuzzleForwardOffset
                + Vector3.up * RaiderWeapon.MuzzleHeight;
        }

        /// <summary>Server-spawns a DummyTarget (Side.Hostile) at the given position and returns the server instance.</summary>
        private NetworkObject SpawnTargetAt(NetworkManager server, Vector3 position)
        {
            NetworkObject nob = server.GetPooledInstantiated(LoadTargetPrefab(), position, Quaternion.identity, true);
            server.ServerManager.Spawn(nob);
            return nob;
        }

        private NetworkObject LoadTargetPrefab()
        {
            NetworkObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(DummyTargetPrefabPath);
#endif
            if (prefab == null)
                Assert.Fail("DummyTarget prefab missing — run 'Light Raiders > Generate Session Assets' or the documented -executeMethod first.");
            return prefab;
        }

        /// <summary>Finds the single target on a client view and exposes its Health; false if not yet replicated.</summary>
        private static bool TargetOnView(NetworkManager client, out Health health)
        {
            NetworkObject target = NetworkSessionHarness.FindDamageable(client.ClientManager.Objects.Spawned);
            health = target != null ? target.GetComponent<Health>() : null;
            return health != null;
        }
    }
}
