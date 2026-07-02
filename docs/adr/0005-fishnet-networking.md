# FishNet for networking, server-authoritative from day one

Light Raiders' netcode needs client-side prediction and lag compensation eventually (certain PvP), but must cost nothing while small. We chose FishNet over Unity's own Netcode for GameObjects and Photon Fusion: it is the only stack shipping client-side prediction free and lag compensation in cheap Pro (~$60 lifetime), with no CCU fees, no vendor lock-in, and server-authority by design. All gameplay code is written server-authoritative from the first line — the client is never trusted for damage, loot, or inventory — even while the co-op phase runs on player-hosted sessions.

Hosting path: co-op phase on player-host + relay (~$0); at the PvP milestone, migrate the same codebase to headless Linux dedicated servers on usage-billed orchestration (Edgegap-class, ~$0.02–0.07/server-hour), because player-hosted PvP with loot at stake is uncheatable-host territory.

## Considered Options

- **Netcode for GameObjects** — best tutorial/AI coverage, proven for co-op (Lethal Company). Rejected: no built-in client-side prediction or lag compensation in 2026 — a wall exactly at the PvP milestone, recreating the retrofit ADR-0003 exists to prevent.
- **Photon Fusion** — the most shooter-complete netcode out of the box; free ≤100 CCU. Rejected for perpetual per-CCU rent that scales with success ($125/mo at 500 CCU, $500/mo at 2,000), Photon Cloud lock-in, paying Photon and the server host once dedicated servers arrive, and a tick-based model reported as conceptually harder for beginners. Decided with eyes open after a full cost walk-through; revisit only if FishNet assembly work stalls the project.

See `docs/research/reference-research.md` §4 for the cited landscape.
