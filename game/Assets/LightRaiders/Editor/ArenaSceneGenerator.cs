using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Generates (or regenerates) the bootstrap arena scene with the networked
    /// session objects wired up. Idempotent: reruns rebuild the Arena,
    /// NetworkSession and RaiderCameraRig roots in place (and drop the scene
    /// template's default camera).
    /// </summary>
    public static class ArenaSceneGenerator
    {
        /* Root-object naming contract: the idempotency sweep in GenerateAll tears
         * down scene roots by these names before the rebuild recreates them. */
        private const string ArenaRootName = "Arena";
        private const string NetworkSessionRootName = "NetworkSession";
        private const string RaiderCameraRigName = "RaiderCameraRig";
        /* The default from NewSceneSetup.DefaultGameObjects, destroyed once the
         * generator-owned rig replaces it; "Directional Light" deliberately kept. */
        private const string DefaultCameraName = "Main Camera";

        /// <summary>
        /// Generates the Raider prefab and the bootstrap arena scene.
        /// Also CLI-invocable: -executeMethod LightRaiders.Editor.ArenaSceneGenerator.GenerateAll
        /// </summary>
        [MenuItem("Light Raiders/Generate Session Assets")]
        public static void GenerateAll()
        {
            /* Projectile first: both RaiderPrefabGenerator and HostileEmitterPrefabGenerator
             * wire a serialized reference to the Projectile prefab asset and fail-fast if it
             * is absent. DummyTarget is independent (no cross-prefab references). */
            ProjectilePrefabGenerator.Generate();
            DummyTargetPrefabGenerator.Generate();
            LootBagPrefabGenerator.Generate();
            RaiderPrefabGenerator.Generate();
            HostileEmitterPrefabGenerator.Generate();

            Scene scene;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionAssetPaths.BootstrapArenaScene) != null)
            {
                scene = EditorSceneManager.OpenScene(SessionAssetPaths.BootstrapArenaScene, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, SessionAssetPaths.BootstrapArenaScene);
            }

            /* Idempotency: tear down previously generated roots before rebuilding.
             * The default camera falls under the sweep too, so both scene branches
             * converge and a committed scene loses its stale default camera on the
             * next regeneration. */
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name == ArenaRootName || rootGo.name == NetworkSessionRootName
                    || rootGo.name == RaiderCameraRigName || rootGo.name == DefaultCameraName
                    || rootGo.name == HudGenerator.ScreenHudRootName || rootGo.name == HudGenerator.WorldBarsRootName)
                    Object.DestroyImmediate(rootGo);
            }

            Transform[] spawnPoints = BuildArena(out Transform[] targetPosts, out Transform[] emitterPosts);
            NetworkManager networkManager = BuildNetworkSession(spawnPoints);
            BuildCameraRig(networkManager);
            BuildTargetSpawner(networkManager, targetPosts);
            BuildEmitterSpawner(networkManager, emitterPosts);
            HudGenerator.BuildHud(networkManager);

            /* Build-settings registration is deliberately unnecessary: editor and MPPM
             * play modes use the currently open scene, and there are no standalone builds yet. */
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static Transform[] BuildArena(out Transform[] targetPosts, out Transform[] emitterPosts)
        {
            Material floorMaterial = RaiderPrefabGenerator.CreateOrUpdateMaterial(SessionAssetPaths.ArenaFloorMaterial, new Color(0.55f, 0.55f, 0.55f));
            Material obstacleMaterial = RaiderPrefabGenerator.CreateOrUpdateMaterial(SessionAssetPaths.ArenaObstacleMaterial, new Color(0.30f, 0.30f, 0.32f));

            GameObject arena = new GameObject(ArenaRootName);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(arena.transform);
            floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            Vector3[] obstaclePositions =
            {
                new Vector3(6f, 1f, 4f),
                new Vector3(-8f, 1.5f, -6f),
                new Vector3(0f, 1f, -10f),
                new Vector3(-5f, 0.75f, 9f),
                new Vector3(10f, 1.25f, -3f)
            };
            Vector3[] obstacleScales =
            {
                new Vector3(2f, 2f, 2f),
                new Vector3(4f, 3f, 1.5f),
                new Vector3(3f, 2f, 3f),
                new Vector3(1.5f, 1.5f, 5f),
                new Vector3(2.5f, 2.5f, 2f)
            };
            for (int i = 0; i < obstaclePositions.Length; i++)
            {
                GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = "Obstacle" + (i + 1);
                obstacle.transform.SetParent(arena.transform);
                obstacle.transform.localPosition = obstaclePositions[i];
                obstacle.transform.localScale = obstacleScales[i];
                obstacle.GetComponent<Renderer>().sharedMaterial = obstacleMaterial;
            }

            Vector3[] spawnPositions =
            {
                new Vector3(15f, 1f, 15f),
                new Vector3(-15f, 1f, 15f),
                new Vector3(15f, 1f, -15f),
                new Vector3(-15f, 1f, -15f)
            };
            Transform[] spawnPoints = new Transform[spawnPositions.Length];
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                GameObject spawnPoint = new GameObject("SpawnPoint" + (i + 1));
                spawnPoint.transform.SetParent(arena.transform);
                spawnPoint.transform.localPosition = spawnPositions[i];
                spawnPoints[i] = spawnPoint.transform;
            }

            /* Dummy-target posts, at floor level (y=0) so the 2-tall target capsule
             * spans y 0..2 and a flat shot at muzzle height (y=1) strikes it. Placed
             * in open floor clear of the obstacles, along +z where the corner spawns
             * have a shooting lane. */
            Vector3[] targetPostPositions =
            {
                new Vector3(0f, 0f, 10f),
                new Vector3(10f, 0f, 10f),
                new Vector3(-10f, 0f, 10f)
            };
            targetPosts = new Transform[targetPostPositions.Length];
            for (int i = 0; i < targetPostPositions.Length; i++)
            {
                GameObject targetPost = new GameObject("TargetPost" + (i + 1));
                targetPost.transform.SetParent(arena.transform);
                targetPost.transform.localPosition = targetPostPositions[i];
                targetPosts[i] = targetPost.transform;
            }

            /* Hostile-emitter posts, at floor level (y=0) so the 2-tall emitter
             * pillar spans y 0..2 and its muzzle sits at y=1. Placed so each emitter
             * is ~9.9 m (well inside its 15 m range) from the nearest +z corner spawn
             * (15,15)/(-15,15), so a Raider spawning there is shot at immediately; the
             * -z corner spawns sit ~24 m out and draw fire only once a Raider closes
             * in. Clear of the obstacle footprints. */
            Vector3[] emitterPostPositions =
            {
                new Vector3(8f, 0f, 8f),
                new Vector3(-8f, 0f, 8f)
            };
            emitterPosts = new Transform[emitterPostPositions.Length];
            for (int i = 0; i < emitterPostPositions.Length; i++)
            {
                GameObject emitterPost = new GameObject("EmitterPost" + (i + 1));
                emitterPost.transform.SetParent(arena.transform);
                emitterPost.transform.localPosition = emitterPostPositions[i];
                emitterPosts[i] = emitterPost.transform;
            }

            return spawnPoints;
        }

        private static NetworkManager BuildNetworkSession(Transform[] spawnPoints)
        {
            GameObject session = new GameObject(NetworkSessionRootName);

            /* Transport defaults are kept: port 7770; the CLIENT connect address
             * defaults to localhost, but the SERVER binds all interfaces (Tugboat's
             * _ipv4BindAddress default of empty means 0.0.0.0), so the session is
             * LAN-exposed — hence the possible Windows firewall prompt. Acceptable
             * for this local slice. */
            session.AddComponent<Tugboat>();

            NetworkManager networkManager = session.AddComponent<NetworkManager>();
            /* Explicit assignment: a null SpawnablePrefabs at play time is a hard error,
             * and relying on edit-mode auto-assign would log noise every generation. */
            DefaultPrefabObjects defaultPrefabObjects = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(SessionAssetPaths.DefaultPrefabObjects);
            /* Fail fast: this method runs under -executeMethod, where a silently
             * half-built scene would still exit 0. */
            if (defaultPrefabObjects == null)
                throw new System.InvalidOperationException("Expected a DefaultPrefabObjects asset at '" + SessionAssetPaths.DefaultPrefabObjects + "' but none was found — check FishNet's prefab-generation settings (Fish-Networking > Configuration) and rerun generation.");
            networkManager.SpawnablePrefabs = defaultPrefabObjects;

            PlayerSpawner spawner = session.AddComponent<PlayerSpawner>();
            NetworkObject raiderPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(SessionAssetPaths.RaiderPrefab);
            if (raiderPrefab == null)
                throw new System.InvalidOperationException("Expected the Raider prefab (with a NetworkObject) at '" + SessionAssetPaths.RaiderPrefab + "' but none was found — RaiderPrefabGenerator.Generate should have created it earlier in this run; rerun generation and check the log.");
            spawner.SetPlayerPrefab(raiderPrefab);
            spawner.Spawns = spawnPoints;

            session.AddComponent<SessionBootstrap>();

            /* Death consequences (#18): the RaiderRespawner (server-only) drops a loot
             * bag and respawns a dead Raider - owned by the same connection - after a
             * delay. It shares the session NetworkManager, Raider prefab, and spawn
             * points with PlayerSpawner but does NOT spawn on connect; PlayerSpawner
             * still owns the initial spawn. */
            RaiderRespawner respawner = session.AddComponent<RaiderRespawner>();
            NetworkObject lootBagPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(SessionAssetPaths.LootBagPrefab);
            if (lootBagPrefab == null)
                throw new System.InvalidOperationException("Expected the LootBag prefab (with a NetworkObject) at '" + SessionAssetPaths.LootBagPrefab + "' but none was found — LootBagPrefabGenerator.Generate should have created it earlier in this run; rerun generation and check the log.");
            respawner.SetNetworkManager(networkManager);
            respawner.SetRaiderPrefab(raiderPrefab);
            respawner.SetLootBagPrefab(lootBagPrefab);
            respawner.SetSpawns(spawnPoints);

            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(respawner);

            return networkManager;
        }

        private static void BuildCameraRig(NetworkManager networkManager)
        {
            GameObject rigGo = new GameObject(RaiderCameraRigName);
            // Sole camera and sole AudioListener: the sweep removed the scene default.
            rigGo.tag = "MainCamera";
            /* Edit-mode pose from the component's own constants (Awake only runs in
             * play mode): as if following a Raider at the arena origin. */
            rigGo.transform.SetPositionAndRotation(
                RaiderCameraRig.FollowOffset,
                Quaternion.Euler(RaiderCameraRig.PitchDegrees, 0f, 0f));
            rigGo.AddComponent<Camera>();
            rigGo.AddComponent<AudioListener>();
            RaiderCameraRig rig = rigGo.AddComponent<RaiderCameraRig>();
            rig.SetNetworkManager(networkManager);
            EditorUtility.SetDirty(rig);

            /* Debug-only prediction feel-test harness (#20 / ADR-0007): drives the
             * transport latency simulator so the CSP with/without difference is
             * feelable in MPPM (which otherwise runs at ~0ms). Lives on the rig
             * GameObject with the other client-local wiring; F9 toggles per window. */
            LatencyToggle latency = rigGo.AddComponent<LatencyToggle>();
            latency.SetNetworkManager(networkManager);
            EditorUtility.SetDirty(latency);
        }

        private static void BuildTargetSpawner(NetworkManager networkManager, Transform[] posts)
        {
            /* Server-side spawner lives on the NetworkSession GameObject next to
             * SessionBootstrap, sharing the session NetworkManager. It is torn down
             * and rebuilt with that root by GenerateAll's idempotency sweep. */
            DummyTargetSpawner spawner = networkManager.gameObject.AddComponent<DummyTargetSpawner>();

            NetworkObject targetPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(SessionAssetPaths.DummyTargetPrefab);
            if (targetPrefab == null)
                throw new System.InvalidOperationException("Expected the DummyTarget prefab (with a NetworkObject) at '" + SessionAssetPaths.DummyTargetPrefab + "' but none was found — DummyTargetPrefabGenerator.Generate should have created it earlier in this run; rerun generation and check the log.");

            spawner.SetNetworkManager(networkManager);
            spawner.SetTargetPrefab(targetPrefab);
            spawner.SetPosts(posts);
            EditorUtility.SetDirty(spawner);
        }

        private static void BuildEmitterSpawner(NetworkManager networkManager, Transform[] posts)
        {
            /* Same placement contract as BuildTargetSpawner: a server-side spawner on
             * the NetworkSession GameObject, sharing the session NetworkManager, torn
             * down and rebuilt by GenerateAll's idempotency sweep. */
            HostileEmitterSpawner spawner = networkManager.gameObject.AddComponent<HostileEmitterSpawner>();

            NetworkObject emitterPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(SessionAssetPaths.HostileEmitterPrefab);
            if (emitterPrefab == null)
                throw new System.InvalidOperationException("Expected the HostileEmitter prefab (with a NetworkObject) at '" + SessionAssetPaths.HostileEmitterPrefab + "' but none was found — HostileEmitterPrefabGenerator.Generate should have created it earlier in this run; rerun generation and check the log.");

            spawner.SetNetworkManager(networkManager);
            spawner.SetEmitterPrefab(emitterPrefab);
            spawner.SetPosts(posts);
            EditorUtility.SetDirty(spawner);
        }
    }
}
