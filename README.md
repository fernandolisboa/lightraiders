# Light Raiders

A PvPvE extraction shooter with an angled top-down view: Raiders descend into a world lit by a salvation that worked too well, stealing light from among the Choir — and everything they carry is at risk until they extract.

**Status: pre-code.** The design interview is complete and documented; Phase 0 (engine setup and learning) is just beginning. Built solo, full-time, with AI assistance.

## The game in one paragraph

Humanity's last machine sang the Hymn, and the Light ignited — a never-setting radiance that preserves, transforms, and hypnotizes everyone who works near it. The Choir emerged from it. Players are Raiders: they leave the shadowed Fold, insert into the lit world, fight the Choir, loot, and extract — or die and lose what they carried (only the Safe Pocket survives). The first playable milestone is a networked co-op PvE vertical slice for 1–3 friends; PvP hostility is the first post-slice milestone, once the slice proves the loop is fun.

## Tech

| | |
|---|---|
| Engine | Unity 6 LTS ([ADR 0002](docs/adr/0002-unity-engine.md)) |
| Netcode | FishNet, server-authoritative from the first line ([ADR 0005](docs/adr/0005-fishnet-networking.md)) |
| Format | Angled top-down, 3D graphics ([ADR 0001](docs/adr/0001-angled-top-down-format.md)) |
| First milestone | Network-first co-op PvE slice ([ADR 0003](docs/adr/0003-network-first-pve-slice.md)) |
| Interiors | Discrete Layers, not seamless ([ADR 0004](docs/adr/0004-discrete-layer-interiors.md)) |
| Business model | Premium Early Access ([ADR 0006](docs/adr/0006-premium-early-access.md)) |

## Where things are

| Path | What it is |
|---|---|
| [`CONTEXT.md`](CONTEXT.md) | The domain glossary — canonical terms for everything (Raider, the Fold, Lumen, the Choir, Layers, Escalation…). Use these words exactly. |
| [`docs/adr/`](docs/adr/) | Architecture Decision Records. Decided and closed — don't re-litigate. |
| [`docs/vertical-slice-plan.md`](docs/vertical-slice-plan.md) | The build plan: 8 phases, exit criteria, the deferred-scope contract, standing risks. |
| [`docs/research/`](docs/research/) | Cited market and tech research behind the decisions. |
| [`docs/handoffs/`](docs/handoffs/) | Session handoffs — each work phase starts by reading the latest one. |
| [`docs/agents/`](docs/agents/) | Agent-skill config: issue tracker conventions, triage labels, domain-doc rules. |
| [`CLAUDE.md`](CLAUDE.md) | The development workflow: phase pipeline and the 8-step per-issue flow. |

## The plan at a glance

Eight build phases, ordered by risk-to-the-fun, each ending with something playable:

0. **Foundations** — Unity/C#/FishNet learning; a networked capsule that moves and shoots
1. **Core feel** — top-down camera, movement, 360° aim, projectile combat, all graybox
2. **Layers & map** — the Layer system and a graybox of Matins, the slice map
3. **The Choir** — five enemy units and the Escalation director
4. **Loot & economy** — inventory, Stash, Workbench, Recycler, Trader, Lumens
5. **Progression** — Skill Tree, the first story Project, Trials, death rules
6. **Art & juice** — replace grayboxes; VFX, audio, UI polish
7. **Playtest loop** — friends playtests, weekly builds, tuning

Slice exit criteria: strangers-to-the-project friends **ask to play again**.

## Working in this repo

- Issues and PRDs live in [GitHub Issues](https://github.com/fernandolisboa/lightraiders/issues); conventions in [`docs/agents/issue-tracker.md`](docs/agents/issue-tracker.md).
- Everything in this repo is written in English.
- The deferred-scope list in the slice plan is a contract: anything joining the slice must evict something.
