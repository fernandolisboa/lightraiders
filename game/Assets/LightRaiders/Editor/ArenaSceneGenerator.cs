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
    /// session objects wired up. Idempotent: reruns rebuild the Arena and
    /// NetworkSession roots in place.
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
            RaiderPrefabGenerator.Generate();

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
                    || rootGo.name == RaiderCameraRigName || rootGo.name == DefaultCameraName)
                    Object.DestroyImmediate(rootGo);
            }

            Transform[] spawnPoints = BuildArena();
            NetworkManager networkManager = BuildNetworkSession(spawnPoints);
            BuildCameraRig(networkManager);

            /* Build-settings registration is deliberately unnecessary: editor and MPPM
             * play modes use the currently open scene, and there are no standalone builds yet. */
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static Transform[] BuildArena()
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

            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(spawner);

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
        }
    }
}
