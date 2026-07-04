using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using NUnit.Framework;
using UnityEngine;
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
        private const string RaiderPrefabPath = "Assets/LightRaiders/Prefabs/Raider.prefab";
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

        /// <summary>
        /// Next port to hand out. Static so consecutive tests in one run never
        /// reuse a port that may still be closing.
        /// </summary>
        private static ushort _nextPort = 7801;

        private readonly ushort _port;
        private readonly List<NetworkManager> _createdManagers = new List<NetworkManager>();

        public NetworkSessionHarness()
        {
            _port = _nextPort;
            _nextPort++;
        }

        public NetworkManager CreateNetworkManager(bool withSpawner)
        {
            GameObject go = new GameObject("NetworkManager-" + _createdManagers.Count);
            // Inactive while configuring: NetworkManager.Awake must run after setup.
            go.SetActive(false);

            Tugboat tugboat = go.AddComponent<Tugboat>();
            tugboat.SetPort(_port);

            NetworkManager networkManager = go.AddComponent<NetworkManager>();

            /* Inspector-equivalent arrangement: allow several NetworkManagers to
             * coexist during a test and keep them scene-bound for easy teardown. */
            SerializedObject serializedManager = new SerializedObject(networkManager);
            serializedManager.FindProperty("_persistence").intValue = (int)NetworkManager.PersistenceType.AllowMultiple;
            serializedManager.FindProperty("_dontDestroyOnLoad").boolValue = false;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

#if UNITY_EDITOR
            /* Must be assigned before activation: a null SpawnablePrefabs in play
             * mode is a hard error that aborts NetworkManager initialization. */
            networkManager.SpawnablePrefabs = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath);
#endif

            if (withSpawner)
            {
                PlayerSpawner spawner = go.AddComponent<PlayerSpawner>();
                NetworkObject raiderPrefab = null;
#if UNITY_EDITOR
                raiderPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(RaiderPrefabPath);
#endif
                if (raiderPrefab == null)
                    Assert.Fail("Raider prefab missing — run 'Light Raiders > Generate Session Assets' or the documented -executeMethod first.");
                spawner.SetPlayerPrefab(raiderPrefab);
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

        public IEnumerator Teardown()
        {
            for (int i = _createdManagers.Count - 1; i >= 0; i--)
            {
                NetworkManager networkManager = _createdManagers[i];
                if (networkManager == null)
                    continue;

                if (networkManager.Initialized)
                {
                    if (networkManager.ClientManager.Started)
                        networkManager.ClientManager.StopConnection();
                    if (networkManager.ServerManager.Started)
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
