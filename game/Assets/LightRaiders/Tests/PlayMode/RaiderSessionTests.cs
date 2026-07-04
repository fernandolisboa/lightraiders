using System.Collections;
using FishNet.Managing;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace LightRaiders.Tests
{
    /// <summary>
    /// PlayMode tests covering the networked session lifecycle of the Raider
    /// stand-in. Assertions only use the network-visible surface:
    /// ServerManager/ClientManager Objects.Spawned.
    /// </summary>
    [TestFixture]
    public class RaiderSessionTests
    {
        private NetworkSessionHarness _harness;

        [SetUp]
        public void SetUp()
        {
            _harness = new NetworkSessionHarness();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _harness.Teardown();
        }

        [UnityTest]
        public IEnumerator Raider_SpawnsOnClientJoin()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager client = _harness.CreateNetworkManager(withSpawner: false);
            client.ClientManager.StartConnection();

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 1,
                "Server never saw exactly one Raider after client join.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(client.ClientManager.Objects.Spawned) == 1,
                "Client never saw its own Raider spawn.");
        }

        [UnityTest]
        public IEnumerator Raider_DespawnsOnClientDisconnect()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager client = _harness.CreateNetworkManager(withSpawner: false);
            client.ClientManager.StartConnection();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 1,
                "Server never saw the Raider spawn before disconnect.");

            client.ClientManager.StopConnection();

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 0,
                "Server never despawned the Raider after client disconnect.");
        }

        [UnityTest]
        public IEnumerator Raider_LateJoinerSeesExistingRaiders()
        {
            NetworkManager server = _harness.CreateNetworkManager(withSpawner: true);
            server.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => server.ServerManager.Started, "Server did not start.");

            NetworkManager firstClient = _harness.CreateNetworkManager(withSpawner: false);
            firstClient.ClientManager.StartConnection();
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(firstClient.ClientManager.Objects.Spawned) == 1,
                "First client never saw its own Raider.");

            NetworkManager lateJoiner = _harness.CreateNetworkManager(withSpawner: false);
            lateJoiner.ClientManager.StartConnection();

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(lateJoiner.ClientManager.Objects.Spawned) == 2,
                "Late joiner never saw both Raiders.");
            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(server.ServerManager.Objects.Spawned) == 2,
                "Server never saw both Raiders after late join.");
        }

        [UnityTest]
        public IEnumerator Raider_HostGetsOwnRaider()
        {
            NetworkManager host = _harness.CreateNetworkManager(withSpawner: true);
            host.ServerManager.StartConnection();
            yield return _harness.WaitUntil(() => host.ServerManager.Started, "Server did not start.");

            host.ClientManager.StartConnection();

            yield return _harness.WaitUntil(
                () => NetworkSessionHarness.CountRaiders(host.ServerManager.Objects.Spawned) == 1,
                "Host never received its own Raider.");
        }
    }
}
