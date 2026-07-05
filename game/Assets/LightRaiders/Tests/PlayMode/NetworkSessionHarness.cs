using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LightRaiders.Tests
{
    /// <summary>
    /// Builds isolated NetworkManager instances for PlayMode tests. One harness per
    /// test: all managers it creates share one port so a server and its clients can
    /// talk, while different tests use different ports to avoid cross-talk.
    /// </summary>
    public sealed class NetworkSessionHarness
    {
        /* Duplicated from SessionAssetPaths: this assembly cannot reference the
         * editor-only LightRaiders.Editor asmdef where those constants live. */
        private const string RaiderPrefabPath = "Assets/LightRaiders/Prefabs/Raider.prefab";
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

        /// <summary>
        /// Next port to hand out. Static so consecutive tests in one run never
        /// reuse a port that may still be closing. The 7801 base clears Tugboat's
        /// default 7770 so tests never collide with a live editor-hosted session.
        /// </summary>
        private static ushort _nextPort = 7801;

        private readonly ushort _port;
        private readonly List<NetworkManager> _createdManagers = new List<NetworkManager>();

        public NetworkSessionHarness()
        {
            _port = _nextPort;
            _nextPort++;
        }

        public NetworkManager CreateNetworkManager(bool withSpawner, Transform[] spawns = null)
        {
            /* Resolve the Raider prefab BEFORE creating any GameObject: failing
             * afterwards would leak an inactive manager object that teardown
             * never sees. */
            NetworkObject raiderPrefab = null;
            if (withSpawner)
            {
#if UNITY_EDITOR
                raiderPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(RaiderPrefabPath);
#endif
                if (raiderPrefab == null)
                    Assert.Fail("Raider prefab missing — run 'Light Raiders > Generate Session Assets' or the documented -executeMethod first.");
            }

            GameObject go = new GameObject("NetworkManager-" + _createdManagers.Count);
            // Inactive while configuring: NetworkManager.Awake must run after setup.
            go.SetActive(false);

            Tugboat tugboat = go.AddComponent<Tugboat>();
            tugboat.SetPort(_port);

            /* In the editor, AddComponent synchronously fires FishNet's
             * OnValidate -> Reset -> ValidateSpawnablePrefabs BEFORE the harness
             * can assign SpawnablePrefabs below, and in play mode that logs one
             * error. Declare it expected so the runner doesn't fail the test;
             * the real assignment happens before activation, so Awake passes. */
            LogAssert.Expect(LogType.Error, new Regex("SpawnablePrefabs is null"));
            NetworkManager networkManager = go.AddComponent<NetworkManager>();

#if UNITY_EDITOR
            /* Must be assigned before activation (a null SpawnablePrefabs in play
             * mode is a hard error that aborts NetworkManager initialization) and
             * before the SerializedObject edits below, whose apply re-triggers
             * OnValidate and would log the same null-SpawnablePrefabs error again. */
            networkManager.SpawnablePrefabs = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath);

            /* Inspector-equivalent arrangement: allow several NetworkManagers to
             * coexist during a test and keep them scene-bound for easy teardown. */
            SerializedObject serializedManager = new SerializedObject(networkManager);
            SerializedProperty persistence = serializedManager.FindProperty("_persistence");
            Assert.IsNotNull(persistence, "FishNet renamed serialized field '_persistence' — update NetworkSessionHarness.");
            persistence.intValue = (int)NetworkManager.PersistenceType.AllowMultiple;
            SerializedProperty dontDestroyOnLoad = serializedManager.FindProperty("_dontDestroyOnLoad");
            Assert.IsNotNull(dontDestroyOnLoad, "FishNet renamed serialized field '_dontDestroyOnLoad' — update NetworkSessionHarness.");
            dontDestroyOnLoad.boolValue = false;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
#endif

            if (withSpawner)
            {
                PlayerSpawner spawner = go.AddComponent<PlayerSpawner>();
                spawner.SetPlayerPrefab(raiderPrefab);
                if (spawns != null)
                    spawner.Spawns = spawns;
            }

            go.SetActive(true);
            _createdManagers.Add(networkManager);
            return networkManager;
        }

        public IEnumerator WaitUntil(Func<bool> condition, string failureMessage, float timeoutSeconds = 10f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail(failureMessage + " (timed out after " + timeoutSeconds + "s)");
                yield return null;
            }
        }

        public static int CountRaiders(IReadOnlyDictionary<int, NetworkObject> spawned)
        {
            int count = 0;
            foreach (NetworkObject networkObject in spawned.Values)
            {
                if (networkObject != null && networkObject.GetComponent<Raider>() != null)
                    count++;
            }

            return count;
        }

        public static NetworkObject FindOwnedRaider(NetworkManager clientNetworkManager)
        {
            foreach (NetworkObject networkObject in clientNetworkManager.ClientManager.Objects.Spawned.Values)
            {
                if (networkObject != null && networkObject.IsOwner && networkObject.GetComponent<Raider>() != null)
                    return networkObject;
            }

            return null;
        }

        public static NetworkObject FindOnView(IReadOnlyDictionary<int, NetworkObject> spawned, int objectId)
        {
            return spawned.TryGetValue(objectId, out NetworkObject networkObject) ? networkObject : null;
        }

        public static int CountProjectiles(IReadOnlyDictionary<int, NetworkObject> spawned)
        {
            int count = 0;
            foreach (NetworkObject networkObject in spawned.Values)
            {
                if (networkObject != null && networkObject.GetComponent<Projectile>() != null)
                    count++;
            }

            return count;
        }

        public static NetworkObject FindProjectile(IReadOnlyDictionary<int, NetworkObject> spawned)
        {
            foreach (NetworkObject networkObject in spawned.Values)
            {
                if (networkObject != null && networkObject.GetComponent<Projectile>() != null)
                    return networkObject;
            }

            return null;
        }

        public static int CountDamageables(IReadOnlyDictionary<int, NetworkObject> spawned)
        {
            int count = 0;
            foreach (NetworkObject networkObject in spawned.Values)
            {
                if (networkObject != null && networkObject.GetComponent<Health>() != null)
                    count++;
            }

            return count;
        }

        public static NetworkObject FindDamageable(IReadOnlyDictionary<int, NetworkObject> spawned)
        {
            foreach (NetworkObject networkObject in spawned.Values)
            {
                if (networkObject != null && networkObject.GetComponent<Health>() != null)
                    return networkObject;
            }

            return null;
        }

        public static float PlanarDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        /// <summary>
        /// Angle in degrees between two directions projected onto the ground plane.
        /// Callers must pass non-degenerate directions (unit forwards / >=10m
        /// offsets): Vector2.Angle returns 0 for zero vectors.
        /// </summary>
        public static float PlanarAngle(Vector3 a, Vector3 b)
        {
            return Vector2.Angle(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        /// <summary>
        /// Waits for gravity to settle the server-side capsule onto the floor so
        /// baselines taken afterwards are never polluted by the initial fall.
        /// </summary>
        public IEnumerator SettleServerRaider(NetworkObject serverRaider)
        {
            yield return WaitUntil(
                () => serverRaider.transform.position.y < 0.5f,
                "Server Raider never settled onto the floor.");
        }

        /// <summary>
        /// Waits until the server's TimeManager has advanced by the given tick
        /// count, guaranteeing the current intent was actually processed that
        /// many times (wall-clock waits cannot guarantee any tick ran).
        /// </summary>
        public IEnumerator WaitForServerTicks(NetworkManager server, uint tickCount)
        {
            uint targetTick = server.TimeManager.Tick + tickCount;
            yield return WaitUntil(
                () => server.TimeManager.Tick >= targetTick,
                "Server ticks never advanced.");
        }

        public IEnumerator Teardown()
        {
            for (int i = _createdManagers.Count - 1; i >= 0; i--)
            {
                NetworkManager networkManager = _createdManagers[i];
                if (networkManager == null)
                    continue;

                if (networkManager.Initialized)
                {
                    /* Unconditional: Started only reports fully-started, so gating
                     * on it would skip connections stuck in Starting. StopConnection
                     * on a never-started connection is safe in FishNet/Tugboat — it
                     * just returns false. */
                    networkManager.ClientManager.StopConnection();
                    networkManager.ServerManager.StopConnection(true);

                    // Best effort: give the transport a moment to close sockets.
                    float deadline = Time.realtimeSinceStartup + 5f;
                    while ((networkManager.ClientManager.Started || networkManager.ServerManager.Started) && Time.realtimeSinceStartup < deadline)
                        yield return null;
                }

                UnityEngine.Object.Destroy(networkManager.gameObject);
            }

            _createdManagers.Clear();
            yield return null;
            yield return null;
        }
    }
}
