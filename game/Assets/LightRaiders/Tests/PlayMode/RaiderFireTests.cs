using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    /// PlayMode tests for server-authoritative fire: cooldown-validated
    /// Projectile spawn and lifetime despawn. The default test scene is empty,
    /// so each test builds its own floor and spawn points. Every test injects a
    /// zero-default ScriptedRaiderIntentProvider into each owned raider before
    /// baselining, so the prefab's hardware provider never feeds real mouse
    /// state into a non-batch editor run.
    /// Struct trap: RaiderIntent is a struct - "new RaiderIntent { FirePressed
    /// = true }" resets AimPoint to (0,0,0), a VALID world point that snaps
    /// facing to world origin. Any test that converged facing first must carry
    /// the AimPoint in every subsequent intent assignment.
    /// </summary>
    [TestFixture]
    public class RaiderFireTests
    {
        /* Duplicated from SessionAssetPaths (same rule as NetworkSessionHarness):
         * this assembly cannot reference the editor-only LightRaiders.Editor
         * asmdef where those constants live. */
        private const string ProjectilePrefabPath = "Assets/LightRaiders/Prefabs/Projectile.prefab";

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
        public IEnumerator OwnerFire_SpawnsProjectileOnServer()
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

            // Zero-intent injection before baselining; see the fixture comment.
            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(serverRaider);
            Vector3 basePos = serverRaider.transform.position;

            // Converge facing on a due +X target first so the spawn direction is known.
            Vector3 aim = basePos + new Vector3(10f, 0f, 0f);
            scripted.Intent = new RaiderIntent { AimPoint = aim };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarAngle(serverRaider.transform.forward, aim - basePos) < 5f,
                "Server facing never converged on the aim point.");

            // Anti-vacuity baseline: nothing has fired yet.
            Assert.That(
                NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned),
                Is.EqualTo(0),
                "A projectile existed before any fire intent.");

            // Struct trap: AimPoint must be carried or it resets to (0,0,0), a valid point.
            scripted.Intent = new RaiderIntent { AimPoint = aim, FirePressed = true };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned) >= 1,
                "Fire intent never spawned a projectile on the server.");
            /* Level-state off immediately: the ~8-tick cooldown >> one frame, so
             * normally exactly one spawn - but assert on the FIRST projectile
             * only, keeping this test stall-proof; exact-count discipline
             * belongs to FireHeld_RespectsCooldown. */
            scripted.Intent = new RaiderIntent { AimPoint = aim };

            /* Spawn pose + constant speed, reconstructed in one frame: the
             * server transform is read directly, so the tolerance is
             * float/settle slack only. */
            NetworkObject projectile = NetworkSessionHarness.FindProjectile(server.ServerManager.Objects.Spawned);
            Projectile p = projectile.GetComponent<Projectile>();
            // The raider is stationary, so recomputing the muzzle now is valid.
            Vector3 muzzle = serverRaider.transform.position
                + serverRaider.transform.forward * RaiderWeapon.MuzzleForwardOffset
                + Vector3.up * RaiderWeapon.MuzzleHeight;
            /* TimeManager increments Tick AFTER OnTick handlers run, so an
             * out-of-handler read is "next tick"; and the projectile's handler,
             * subscribed mid-invocation at spawn, first moves it at SpawnTick+1
             * - moves so far = Tick - SpawnTick - 1. Safe as uint: the poll
             * that observed the projectile ran after the spawning tick's
             * iteration completed, so Tick >= SpawnTick + 1. */
            uint elapsedMoves = server.TimeManager.Tick - p.SpawnTick - 1;
            Vector3 expected = muzzle + serverRaider.transform.forward
                * (Projectile.Speed * elapsedMoves * (float)server.TimeManager.TickDelta);
            Assert.That(
                Vector3.Distance(projectile.transform.position, expected),
                Is.LessThan(0.25f),
                "Projectile is not at muzzle + facing * speed * elapsedTicks - wrong origin, direction, or speed.");

            /* Travels along the aim bearing, flat. 0.5m is ~2 ticks of travel
             * against a 45-tick lifetime - no expiry race. 6 degrees = the
             * 5-degree facing convergence budget + 1 slack. */
            Vector3 p1 = projectile.transform.position;
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(projectile.transform.position, p1) > 0.5f,
                "Projectile never traveled on the server.");
            Vector3 p2 = projectile.transform.position;
            Assert.That(
                NetworkSessionHarness.PlanarAngle(p2 - p1, aim - basePos),
                Is.LessThan(6f),
                "Projectile bearing deviated from the aim direction.");
            Assert.That(
                Mathf.Abs(p2.y - p1.y),
                Is.LessThan(0.01f),
                "Projectile did not fly a flat straight line.");
        }

        [UnityTest]
        public IEnumerator OwnerFire_ProjectileIsObservedByOtherClient()
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
                () => NetworkSessionHarness.FindOwnedRaider(clientA) != null && NetworkSessionHarness.FindOwnedRaider(clientB) != null,
                "Clients never saw their owned Raiders.");

            NetworkObject raiderOnA = NetworkSessionHarness.FindOwnedRaider(clientA);
            NetworkObject raiderOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderOnA.ObjectId);
            Assert.IsNotNull(raiderOnServer, "Server view has no instance of A's Raider.");

            // Zero-intent injection before baselining; see the fixture comment.
            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            raiderOnA.GetComponent<RaiderMovement>().SetIntentProvider(scripted);
            /* B's owned raider gets a neutral provider too: in an interactive
             * editor run with the mouse button held, B's raider would poll the
             * real Attack action and fire, satisfying the counts below with
             * B's projectile and masking a broken A-side fire pipe. */
            ScriptedRaiderIntentProvider neutralProviderB = new ScriptedRaiderIntentProvider();
            NetworkSessionHarness.FindOwnedRaider(clientB).GetComponent<RaiderMovement>().SetIntentProvider(neutralProviderB);

            yield return _harness.SettleServerRaider(raiderOnServer);

            Assert.That(
                NetworkSessionHarness.CountProjectiles(clientB.ClientManager.Objects.Spawned),
                Is.EqualTo(0),
                "A projectile existed on client B before any fire intent.");

            scripted.Intent = new RaiderIntent { FirePressed = true };
            // Positive control: the spawn must exist on the server before B can see it.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned) >= 1,
                "Fire intent never spawned a projectile on the server.");
            scripted.Intent = new RaiderIntent();

            // 15s: 30Hz ticks plus 2-tick interpolation on the observing client.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(clientB.ClientManager.Objects.Spawned) >= 1,
                "Client B never saw the projectile.",
                15f);

            /* Replicated motion: the projectile visibly TRAVELS on the
             * observer, not merely exists. 0.5m is ~2 ticks of travel against
             * the 45-tick lifetime - no expiry race. */
            NetworkObject projectileOnB = NetworkSessionHarness.FindProjectile(clientB.ClientManager.Objects.Spawned);
            Vector3 snapshot = projectileOnB.transform.position;
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarDistance(projectileOnB.transform.position, snapshot) > 0.5f,
                "Client B never observed the projectile traveling.",
                15f);
        }

        [UnityTest]
        public IEnumerator FireHeld_RespectsCooldown()
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

            // Zero-intent injection before baselining; see the fixture comment.
            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(serverRaider);

            uint cooldownTicks = RaiderWeapon.CooldownTicks(server.TimeManager);
            Assert.That(
                cooldownTicks,
                Is.GreaterThanOrEqualTo(2),
                "Precondition: cooldown below 2 ticks makes the spacing assert vacuous.");
            Assert.That(
                NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned),
                Is.EqualTo(0),
                "A projectile existed before any fire intent.");

            /* FirePressed arrives every tick (per-tick resend), so proving
             * consecutive spawn spacing >= cooldown proves the ~7 intents
             * inside each window produced zero spawns. Spacing is measured in
             * server-recorded spawn ticks: immune to editor stalls in both
             * directions (a wall-clock "count stayed 1" hold would be
             * stall-flaky - tick catch-up bursts legally spawn more). */
            scripted.Intent = new RaiderIntent { FirePressed = true };

            /* Lifetime (45 ticks) >> the two cooldown windows needed
             * (~16 ticks), so nothing despawns while collecting; the
             * dictionary retains entries regardless. */
            Dictionary<int, uint> spawnTicks = new Dictionary<int, uint>();
            yield return _harness.WaitUntil(() =>
            {
                RecordSpawnTicks(server, spawnTicks);
                return spawnTicks.Count >= 3;
            }, "Held fire never produced three projectiles.");

            scripted.Intent = new RaiderIntent();

            /* AC: the cooldown-limited count must be observed from a connected
             * client too - the throttled count (not one-per-intent) is what
             * replicates. All recorded projectiles are still well inside their
             * 45-tick lifetimes here. */
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(client.ClientManager.Objects.Spawned) == spawnTicks.Count,
                "Client view never converged on the server's cooldown-limited spawn count.",
                15f);

            List<uint> sortedSpawnTicks = new List<uint>(spawnTicks.Values);
            sortedSpawnTicks.Sort();
            for (int i = 1; i < sortedSpawnTicks.Count; i++)
            {
                /* Lower bound = cooldown enforcement (the AC). Upper bound =
                 * built-in positive control that intents flowed every tick and
                 * autofire resumed exactly when allowed; +2 covers
                 * owner-tick/server-tick phase jitter on the in-process
                 * transport. */
                Assert.That(
                    sortedSpawnTicks[i] - sortedSpawnTicks[i - 1],
                    Is.InRange(cooldownTicks, cooldownTicks + 2),
                    "Spawn spacing violates the cooldown (lower bound) or autofire stalled (upper bound).");
            }
        }

        [UnityTest]
        public IEnumerator Projectile_DespawnsAfterLifetime()
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

            // Zero-intent injection before baselining; see the fixture comment.
            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(serverRaider);

            uint lifetimeTicks = Projectile.LifetimeTicks(server.TimeManager);
            Dictionary<int, uint> spawnTicks = new Dictionary<int, uint>();

            scripted.Intent = new RaiderIntent { FirePressed = true };
            /* Spawn ticks are recorded inside this first wait: the poll that
             * first observes the projectile records its SpawnTick before the
             * wait returns, so the timing assert below has data even if an
             * editor stall longer than the whole lifetime hits right after
             * this observation. */
            yield return _harness.WaitUntil(() =>
            {
                RecordSpawnTicks(server, spawnTicks);
                return spawnTicks.Count >= 1;
            }, "Fire intent never spawned a projectile on the server.");
            scripted.Intent = new RaiderIntent();

            /* Positive control on the client view (anti-vacuity for the
             * despawn wait below). Every later poll keeps recording so a
             * stall-latched second spawn is tracked and the timing assert
             * stays exact. */
            yield return _harness.WaitUntil(() =>
            {
                RecordSpawnTicks(server, spawnTicks);
                return NetworkSessionHarness.CountProjectiles(client.ClientManager.Objects.Spawned) >= 1;
            }, "Client never saw the projectile.", 15f);

            // Lifetime is 1.5s + generous slack; never an exact-time wait.
            yield return _harness.WaitUntil(() =>
            {
                RecordSpawnTicks(server, spawnTicks);
                return NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned) == 0;
            }, "Projectile never despawned on the server.", 10f);

            uint maxSpawnTick = 0;
            foreach (uint spawnTick in spawnTicks.Values)
            {
                if (spawnTick > maxSpawnTick)
                    maxSpawnTick = spawnTick;
            }

            /* Frame polling can only observe a despawn LATE, never early, so
             * >= is the honest direction; this single assert proves the
             * projectile survived until its expiry tick (no tick-gated hold
             * needed, avoiding stall-overshoot flakiness). The earliest
             * possible out-of-handler observation of an on-time despawn is
             * despawnTick + 1 (Tick post-increments after handlers), so the +1
             * keeps correct code passing while still catching a one-tick-early
             * despawn bug. */
            Assert.That(
                server.TimeManager.Tick,
                Is.GreaterThanOrEqualTo(maxSpawnTick + lifetimeTicks + 1),
                "Projectile despawned before its lifetime elapsed.");

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(client.ClientManager.Objects.Spawned) == 0,
                "Projectile never despawned on the client.",
                15f);
        }

        [UnityTest]
        public IEnumerator NonOwnerFire_DoesNotSpawnProjectile()
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
                () => NetworkSessionHarness.FindOwnedRaider(clientA) != null && NetworkSessionHarness.FindOwnedRaider(clientB) != null,
                "Clients never saw their owned Raiders.");

            NetworkObject raiderA = NetworkSessionHarness.FindOwnedRaider(clientA);
            NetworkObject raiderAOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderA.ObjectId);
            Assert.IsNotNull(raiderAOnServer, "Server view has no instance of A's Raider.");
            NetworkObject raiderAOnB = NetworkSessionHarness.FindOnView(clientB.ClientManager.Objects.Spawned, raiderA.ObjectId);
            Assert.IsNotNull(raiderAOnB, "Client B's view has no instance of A's Raider.");

            NetworkObject raiderB = NetworkSessionHarness.FindOwnedRaider(clientB);
            NetworkObject raiderBOnServer = NetworkSessionHarness.FindOnView(server.ServerManager.Objects.Spawned, raiderB.ObjectId);
            Assert.IsNotNull(raiderBOnServer, "Server view has no instance of B's Raider.");

            /* Neutral scripted providers on both OWNED instances before
             * baselining, so neither polls the prefab's hardware provider
             * during the test. */
            ScriptedRaiderIntentProvider neutralProviderA = new ScriptedRaiderIntentProvider();
            raiderA.GetComponent<RaiderMovement>().SetIntentProvider(neutralProviderA);
            ScriptedRaiderIntentProvider ownProvider = new ScriptedRaiderIntentProvider();
            raiderB.GetComponent<RaiderMovement>().SetIntentProvider(ownProvider);

            yield return _harness.SettleServerRaider(raiderAOnServer);
            yield return _harness.SettleServerRaider(raiderBOnServer);

            /* The component only sends intent when IsOwner, so this provider on
             * A's Raider in B's view is never polled; FishNet's RequireOwnership
             * is the second line of defense, deliberately untested here. */
            ScriptedRaiderIntentProvider hostileProvider = new ScriptedRaiderIntentProvider();
            hostileProvider.Intent = new RaiderIntent { FirePressed = true };
            raiderAOnB.GetComponent<RaiderMovement>().SetIntentProvider(hostileProvider);

            /* Tick-gated hold: a wall-clock window could pass vacuously through
             * an editor stall before any tick processed the hostile intent. */
            yield return _harness.WaitForServerTicks(server, 15);
            Assert.That(
                NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned),
                Is.EqualTo(0),
                "A non-owner's fire intent spawned a projectile.");

            /* Positive control AFTER the hold, attributing the spawn: the count
             * was 0 throughout the hostile window and becomes >= 1 only once
             * the legitimate owner fires. */
            ownProvider.Intent = new RaiderIntent { FirePressed = true };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned) >= 1,
                "Positive control failed: B's own fire never spawned a projectile.");
            ownProvider.Intent = new RaiderIntent();
        }

        [UnityTest]
        public IEnumerator ClientDirectSpawn_IsRejected()
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

            /* Neutral provider so the raider's hardware provider cannot fire
             * real projectiles into the count==0 asserts below. */
            ScriptedRaiderIntentProvider scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(scripted);

            yield return _harness.SettleServerRaider(serverRaider);

            NetworkObject projectilePrefab = null;
#if UNITY_EDITOR
            projectilePrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(ProjectilePrefabPath);
#endif
            if (projectilePrefab == null)
                Assert.Fail("Projectile prefab missing — run 'Light Raiders > Generate Session Assets' or the documented -executeMethod first.");

            /* Instantiate of a NetworkObject reference returns the component;
             * track the GameObject in the fixture's scene objects so teardown
             * destroys it even on assert-failure paths. */
            NetworkObject instance = Object.Instantiate(projectilePrefab);
            _sceneObjects.Add(instance.gameObject);

            /* The client is CONNECTED, so FishNet's spawn gate takes the
             * predicted-spawning branch, and the expectation itself is the
             * anti-vacuity control: the test fails if FishNet never logs the
             * rejection. The loose regex tolerates message-prefix changes. */
            LogAssert.Expect(LogType.Warning, new Regex("Cannot spawn object because server is not active"));
            client.ServerManager.Spawn(instance);

            yield return _harness.WaitForServerTicks(server, 15);
            /* CountProjectiles reads network views, so the local un-networked
             * clone can never satisfy it - that is the point. */
            Assert.That(
                NetworkSessionHarness.CountProjectiles(server.ServerManager.Objects.Spawned),
                Is.EqualTo(0),
                "A client's direct Spawn call produced a projectile on the server.");
            Assert.That(
                NetworkSessionHarness.CountProjectiles(client.ClientManager.Objects.Spawned),
                Is.EqualTo(0),
                "A client's direct Spawn call produced a projectile in the client's network view.");

            Object.Destroy(instance.gameObject);
        }

        [UnityTest]
        public IEnumerator ProjectileHittingWall_DespawnsAtSurface()
        {
            Shooter shooter = new Shooter();
            yield return ArrangePlusXShooter(shooter);

            /* World geometry across the flight path: a non-networked cube with
             * its default BoxCollider, 6 m ahead of the muzzle. Not thin - this
             * test isolates "consumed at the surface", the thin-obstacle sweep is
             * covered separately below. */
            const float nearFaceDistance = 6f;
            const float thickness = 1f;
            BuildWallAcrossPath(shooter.Muzzle, nearFaceDistance, thickness);
            float wallNearX = shooter.Muzzle.x + nearFaceDistance;
            float wallFarX = wallNearX + thickness;

            Assert.That(
                NetworkSessionHarness.CountProjectiles(shooter.Server.ServerManager.Objects.Spawned),
                Is.EqualTo(0),
                "A projectile existed before any fire intent.");

            // One shot only: stop firing the instant it spawns so the ~8-tick cooldown never adds a second.
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim, FirePressed = true };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(shooter.Server.ServerManager.Objects.Spawned) >= 1,
                "Fire intent never spawned a projectile on the server.");
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim };

            // Observer positive control: the shot must be seen by the other view before it is consumed.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(shooter.Client.ClientManager.Objects.Spawned) >= 1,
                "Observer client never saw the projectile.",
                15f);

            /* Track the furthest point reached while the shot is alive. Frame
             * polling can only sample BEFORE the despawn tick destroys the
             * object, so the last sample sits within one tick of travel (~0.4 m)
             * short of the surface. */
            float maxReachedX = float.NegativeInfinity;
            yield return _harness.WaitUntil(
                () =>
                {
                    NetworkObject live = NetworkSessionHarness.FindProjectile(shooter.Server.ServerManager.Objects.Spawned);
                    if (live != null)
                        maxReachedX = Mathf.Max(maxReachedX, live.transform.position.x);
                    return NetworkSessionHarness.CountProjectiles(shooter.Server.ServerManager.Objects.Spawned) == 0;
                },
                "Projectile never despawned - the wall did not consume it.");

            /* Consumed by the WALL, not by lifetime: had it tunneled it would
             * have flown the full ~18 m range (well past the far face); had it
             * despawned early it would never have reached the near face. */
            Assert.That(
                maxReachedX,
                Is.LessThan(wallFarX + 0.5f),
                "Projectile passed through the wall (tunneling) instead of stopping at the surface.");
            Assert.That(
                maxReachedX,
                Is.GreaterThan(wallNearX - 1f),
                "Projectile despawned before reaching the wall - not a surface hit.");

            // Consumed identically on the observer.
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(shooter.Client.ClientManager.Objects.Spawned) == 0,
                "Projectile never despawned on the observer client.",
                15f);
        }

        [UnityTest]
        public IEnumerator ProjectileThroughThinObstacle_StillDespawns()
        {
            Shooter shooter = new Shooter();
            yield return ArrangePlusXShooter(shooter);

            /* Deliberately thinner than one tick of travel (0.4 m at Speed 12 /
             * 30 Hz): a per-tick endpoint sample would step clean over this slab,
             * so only a true segment sweep registers the hit. This is the
             * anti-tunneling acceptance criterion. */
            const float nearFaceDistance = 6f;
            const float thickness = 0.05f;
            BuildWallAcrossPath(shooter.Muzzle, nearFaceDistance, thickness);
            float wallNearX = shooter.Muzzle.x + nearFaceDistance;
            float wallFarX = wallNearX + thickness;

            Assert.That(
                NetworkSessionHarness.CountProjectiles(shooter.Server.ServerManager.Objects.Spawned),
                Is.EqualTo(0),
                "A projectile existed before any fire intent.");

            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim, FirePressed = true };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(shooter.Server.ServerManager.Objects.Spawned) >= 1,
                "Fire intent never spawned a projectile on the server.");
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim };

            float maxReachedX = float.NegativeInfinity;
            yield return _harness.WaitUntil(
                () =>
                {
                    NetworkObject live = NetworkSessionHarness.FindProjectile(shooter.Server.ServerManager.Objects.Spawned);
                    if (live != null)
                        maxReachedX = Mathf.Max(maxReachedX, live.transform.position.x);
                    return NetworkSessionHarness.CountProjectiles(shooter.Server.ServerManager.Objects.Spawned) == 0;
                },
                "Projectile never despawned - the thin obstacle was tunneled through.");

            Assert.That(
                maxReachedX,
                Is.LessThan(wallFarX + 0.5f),
                "Projectile tunneled through the thin obstacle instead of being consumed by it.");
            Assert.That(
                maxReachedX,
                Is.GreaterThan(wallNearX - 1f),
                "Projectile despawned before reaching the thin obstacle - not a surface hit.");
        }

        [UnityTest]
        public IEnumerator ProjectileInOpenSpace_FliesPastObstacleZone_ThenDespawnsAtLifetime()
        {
            Shooter shooter = new Shooter();
            yield return ArrangePlusXShooter(shooter);

            /* Same +X setup as the collision tests but with NO wall: the
             * positive control proving the shots above are consumed by the wall,
             * not by anything intrinsic to the firing setup. The shot must fly
             * clean through the zone where those walls stood and only expire on
             * lifetime (~17.6 m = Speed * (LifetimeSeconds - one tick)). */
            const float obstacleZoneX = 6f;

            Assert.That(
                NetworkSessionHarness.CountProjectiles(shooter.Server.ServerManager.Objects.Spawned),
                Is.EqualTo(0),
                "A projectile existed before any fire intent.");

            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim, FirePressed = true };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountProjectiles(shooter.Server.ServerManager.Objects.Spawned) >= 1,
                "Fire intent never spawned a projectile on the server.");
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim };

            float maxReachedX = float.NegativeInfinity;
            yield return _harness.WaitUntil(
                () =>
                {
                    NetworkObject live = NetworkSessionHarness.FindProjectile(shooter.Server.ServerManager.Objects.Spawned);
                    if (live != null)
                        maxReachedX = Mathf.Max(maxReachedX, live.transform.position.x);
                    return NetworkSessionHarness.CountProjectiles(shooter.Server.ServerManager.Objects.Spawned) == 0;
                },
                "Projectile never despawned on its lifetime in open space.",
                10f);

            // Flew well past where the walls stood (no false hit), and covered ~the full lifetime range.
            Assert.That(
                maxReachedX,
                Is.GreaterThan(shooter.Muzzle.x + obstacleZoneX + 1f),
                "Projectile despawned inside the open obstacle zone - a phantom hit with no collider present.");
            Assert.That(
                maxReachedX - shooter.Muzzle.x,
                Is.InRange(15f, 18.5f),
                "Projectile did not travel its full lifetime range in open space.");
        }

        /// <summary>Handles for a settled, +X-facing shooter shared by the collision tests.</summary>
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
        /// settled onto the floor and converged to face due +X. Populates the
        /// passed Shooter so each collision test can place geometry relative to
        /// the known muzzle and fire down a known bearing.
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

            // Zero-intent injection before baselining; see the fixture comment.
            shooter.Scripted = new ScriptedRaiderIntentProvider();
            ownedRaider.GetComponent<RaiderMovement>().SetIntentProvider(shooter.Scripted);

            yield return _harness.SettleServerRaider(shooter.ServerRaider);

            // Converge facing on a due +X target so the flight path is a known bearing.
            Vector3 basePos = shooter.ServerRaider.transform.position;
            shooter.Aim = basePos + new Vector3(10f, 0f, 0f);
            shooter.Scripted.Intent = new RaiderIntent { AimPoint = shooter.Aim };
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.PlanarAngle(shooter.ServerRaider.transform.forward, shooter.Aim - basePos) < 5f,
                "Server facing never converged on the aim point.");

            // Muzzle from the settled, converged pose - the Raider is stationary from here.
            shooter.Muzzle = shooter.ServerRaider.transform.position
                + shooter.ServerRaider.transform.forward * RaiderWeapon.MuzzleForwardOffset
                + Vector3.up * RaiderWeapon.MuzzleHeight;
        }

        /// <summary>
        /// Builds a non-networked cube (world geometry) straddling the +X flight
        /// path, its near face <paramref name="nearFaceDistance"/> ahead of the
        /// muzzle. Tall and deep enough (3 m) to catch the shot despite the 5-deg
        /// facing-convergence budget; its default BoxCollider is what the sweep hits.
        /// </summary>
        private GameObject BuildWallAcrossPath(Vector3 muzzle, float nearFaceDistance, float thickness)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestWall";
            wall.transform.position = new Vector3(muzzle.x + nearFaceDistance + thickness * 0.5f, 1f, muzzle.z);
            wall.transform.localScale = new Vector3(thickness, 3f, 3f);
            _sceneObjects.Add(wall);
            return wall;
        }

        /// <summary>
        /// Records the SpawnTick of every projectile currently in the server
        /// view, keyed by ObjectId. Called from within WaitUntil polls so spawn
        /// ticks are captured on the same frame the projectile is first seen.
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
