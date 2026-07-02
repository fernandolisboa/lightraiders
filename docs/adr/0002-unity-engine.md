# Unity as the engine

Built by a solo, full-time developer with zero prior game-dev experience, targeting an angled top-down game with non-pixelated 3D graphics and eventual online PvPvE multiplayer. We chose Unity (C#) because it wins on the three things this situation needs most: the Asset Store (characters, environments, VFX bought rather than made), the deepest pool of tutorials and AI training data for AI-assisted development, and a mature first-party multiplayer stack (Netcode for GameObjects, Relay, Lobby) for when networking lands.

## Considered Options

- **Godot 4** — close second: free, open source, lighter, gentler first language. Rejected on thinner 3D asset ecosystem and a high-level multiplayer API that gets shaky at extraction-shooter player counts.
- **Unreal 5** — best graphics ceiling, deep built-in networking. Rejected: steepest learning curve, team-oriented tooling, overpowered for angled top-down.
- **Phaser** — rejected outright: 2D web framework with no real 3D rendering, incompatible with the stated art bar and a downloadable multiplayer title.
