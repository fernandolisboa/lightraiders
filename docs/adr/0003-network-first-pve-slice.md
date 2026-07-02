# Networked from day one; slice is co-op PvE; PvPvE is certain later

Light Raiders is ultimately a PvPvE extraction shooter — full PvPvE is a certainty, only its timing is open. Because retrofitting multiplayer into a single-player codebase is effectively a rewrite (state ownership and hit authority shape every gameplay system), all gameplay code is written against Unity's networking stack from the first line, while the codebase is small enough for that to be cheap. The vertical slice itself is scoped to the co-op PvE loop — drop in, fight the AI faction, loot, extract, solo or with 1–3 friends — with player-vs-player hostility deferred to the first post-slice milestone.

## Considered Options

- **Single-player slice, multiplayer later** — fastest to a playable loop, but the slice would be a disposable learning artifact rather than the foundation of the real game. Rejected because PvPvE is certain, not hypothetical.
- **PvPvE from day one** — truest to the vision, but matchmaking, server hosting, and cheating arrive before the game is fun. Rejected as sequencing, not as destination.
- **Single-player forever with bot "raiders"** — rejected; real PvP is core to the vision.
