using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace LightRaiders
{
    /// <summary>
    /// Hatch Key placeholder (#39, PRD #31 Q6): every Raider inserts with one
    /// key, held as a server-written, replicated count. The real item replaces
    /// this with the Phase 4 inventory. A respawned or rejoined Raider is a fresh
    /// object, so it starts with a fresh key.
    /// </summary>
    public sealed class HatchKeys : NetworkBehaviour
    {
        public const int StartingKeys = 1;

        private readonly SyncVar<int> _count = new();

        public int Count => _count.Value;

        public override void OnStartServer() => _count.Value = StartingKeys;

        /// <summary>Server: removes one key if any is held.</summary>
        public bool ServerTryConsume()
        {
            if (!IsServerInitialized || _count.Value <= 0)
                return false;
            _count.Value--;
            return true;
        }

        /// <summary>Server, tests and future pickups: sets the count directly.</summary>
        public void ServerSetCount(int count)
        {
            if (IsServerInitialized)
                _count.Value = count < 0 ? 0 : count;
        }
    }
}
