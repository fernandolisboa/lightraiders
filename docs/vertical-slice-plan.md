# Light Raiders — Vertical Slice Plan

Interview completed 2026-07-02. Domain language lives in `/CONTEXT.md`; irreversible decisions in `/docs/adr/`; market/tech research in `/docs/research/reference-research.md`. This document is the build plan.

## What the slice is

A networked co-op PvE extraction raid you can put in friends' hands and answer one question: **is the loop fun?** Drop into Matins, fight the Choir, loot, and get out — solo or in a squad of up to 3.

| Parameter | Decision |
|---|---|
| Map | **Matins** (working name): sun-bleached town at the edge of the lit zone. 3–4 Surface districts, ~15–20 interior Layers, one Undercroft network, one Waygate pair, 2 Exfil sites, 2–3 Hatches |
| Players | 1–3 co-op, friends only (invite/code), host + relay |
| Raid end | Escalation only — no timer; ~20–30 min natural length; Thrones end arguments |
| Enemies | Chorister, Power, Herald, Dominion, Throne |
| Weapons | Pistol, SMG, shotgun, assault rifle, marksman rifle + bolt-caster and beam projector (Choir-tech). All projectile, no hitscan |
| Player kit | Health + Shield (Shield Cell refill only), slot inventory, 3 backpack tiers, channeled healing, 2–3 Relics |
| Economy | 6 loot families, Stash, Workbench, Recycler, sell-only Trader, Lumens. Full loot on death + Safe Pocket |
| Progression | ~12-node utility/economy Skill Tree, 1 story Project, ~5 Trials |
| The Fold | Menu screens only (stash/craft/recycle/trade/skills). Walkable Fold is post-slice |
| Target | ~6–9 months full-time, month 1 mostly learning |

## Explicitly deferred (decided, not forgotten)

PvP hostility (first post-slice milestone) · dedicated servers + anti-cheat (PvP milestone) · in-raid crafting at found benches (first post-slice) · Virtues, Principalities, Seraph · seamless landmark interiors · walkable Fold · large/huge maps and map events · buy-anything trader and player market · cosmetic skins & any shop (post-1.0; never gameplay-affecting) · difficulty ladder for death rules.

## Build phases

Ordered by risk-to-the-fun, not by convenience. Each phase ends with something playable.

**Phase 0 — Foundations (~4 weeks).** Install Unity 6 LTS + Unity Hub on the Windows host, Rider or VS, `git init` this repo. Learn: C# basics, Unity fundamentals (any current beginner course), then FishNet's own examples and docs. Exit criteria: a capsule moves and shoots a projectile in a networked session with a second machine/build connected.

**Phase 1 — Core feel, gray-boxed (~4–6 weeks).** Top-down camera rig, movement, 360° mouse aim, projectile combat vs dummy targets, server-authoritative from the first line (ADR 0005). Damage, health, shield, death → dropped-loot bag. All gray boxes and capsules. Exit: shooting feels good with 3 players in a graybox arena — *this is the slice's first go/no-go signal.*

**Phase 2 — Layers and the map skeleton (~4 weeks).** The Layer system: door transitions, per-Layer replication scoping (players in other Layers don't replicate — the anti-wallhack freebie), small-vs-Large interior AI rule. Graybox Matins: districts, interiors, Undercroft, Waygate pair with shared cooldown, Exfil sites, Hatches. Exit: a full raid path is walkable — insert → loot rooms → Undercroft shortcut → extract.

**Phase 3 — The Choir (~5–6 weeks).** The five units with distinct behaviors (swarm, patrol+bolt, flee+summon, armored beam, hunt), the Escalation director (spawn budget rising with time; Exfil calls spike it; Heralds feed it), Choir Remnant drops. Exit: a raid has a dramatic arc — quiet start, rising pressure, Throne-driven exit.

**Phase 4 — Loot and economy (~4 weeks).** Six loot families, containers, slot inventory + backpack tiers, Safe Pocket, Stash, Workbench recipes (including both Choir-tech weapons), Recycler, Trader + Lumens, Shield Cells/healing consumables as the attrition sink. Exit: the meta-loop closes — raid → recycle/craft/sell → better-equipped raid.

**Phase 5 — Progression and purpose (~3 weeks).** Skill Tree (~12 utility/economy nodes), the story Project (multi-raid, delivers the first lore beat of the Hymn), ~5 Trials, death rules end-to-end. Exit: "why raid tonight?" has three answers (Project, Trials, tree).

**Phase 6 — Art and juice (~4–5 weeks).** Replace grayboxes: Synty environments/characters, animation pack or Mixamo, VFX for Choir bolts/Choir-tech weapons (light-themed), roofless interior dressing, audio pass (directional audio matters at top-down — research §3), UI polish. Exit: screenshots you're not embarrassed by; the Light *looks* tempting.

**Phase 7 — Playtest loop (ongoing, ~2 weeks dedicated).** Friends playtests, weekly builds, tune Escalation/economy/death sting. Exit criteria for the whole slice: strangers-to-the-project friends *ask to play again*. That's the green light for the PvP milestone and the 2–4 year road; its absence is a cheap, honest stop sign.

## Asset shopping list (from research §5; buy lazily, per phase)

- **Phase 0–5: $0.** Gray boxes + Quaternius CC0 packs (Ultimate Space Kit has animated characters) for anything that needs a shape.
- **Phase 6: ~$150–300 total.** Synty Humble bundle when one runs (~$30 for 8–16 packs — a sci-fi bundle ran through March 2026), POLYGON Sci-Fi Space for modular interiors ($149.99, watch 50% sales), Animation Base Locomotion ($69.99) or Mixamo (free), one VFX pack (Polygon Arsenal $40 or Sci-Fi Arsenal ~$15–30; Piloto's Holy & Paladin kit ~$40 fits the Light perfectly).
- Synty caveats (research §5.1): characters ship without animations; interiors exist mainly in Sci-Fi Space/Horror; expect to write the little door-transition polish yourself (we don't need roof-cutaway shaders — Layers replaced them).

## Standing risks

1. **Scope creep is the killer** — the deferred list above is the contract; anything joining the slice must evict something.
2. **Netcode stall** — if FishNet assembly stalls the project for weeks despite AI assistance, the recorded fallback is Photon Fusion (ADR 0005 explicitly permits revisiting), accepted with its rent.
3. **Solo-multiplayer testing friction** — build a second-client workflow early (ParrelSync or multiplayer play mode) or every network feature costs double.
4. **The art bar** — "not pixelated, decent graphics" is achievable with Synty at top-down, but validate with free Synty samples at the real camera angle before spending.

## Week one, concretely

1. Install Unity Hub + Unity 6 LTS (Windows side), Rider/VS Code, and set up the Unity project in this repo; `git init`, commit the docs.
2. Start a beginner Unity/C# course and do it *in* this project's throwaway scenes.
3. Import FishNet (free) and run its example scenes; connect two local clients.
4. End of week: a capsule you control, on a top-down camera, in a scene a friend can join over Relay/localhost — however ugly.
