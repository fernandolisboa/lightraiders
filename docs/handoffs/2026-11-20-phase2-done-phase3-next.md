# Handoff: Phase 2 (Layers + Matins skeleton) done; Phase 3 (The Choir) next

Written 2026-11-20, after the batched Unity verification session. Supersedes the Phase-1 handoffs (`2026-07-06-phase1-impl-done-hitl-next.md`, and the unmerged PR #29) as the session entry point.

## State

- **`main` holds all of Phase 1 + #20 CSP + Phase 2.** Merged 2026-11-20 in stack order: #33 (#20 client-side prediction, ADR-0007 adopted) → #41 (#34 Layer core) → #42 (#35 doors) → #43 (#36 Matins graybox) → #44 (#37 Waygate) → #45 (#38 Exfil + extraction) → #46 (#39 Hatches). PRD #31 and acceptance issue #40 are closed.
- **Verified on Fernando's machine (Editor 6000.3.29f1):** EditMode all green, PlayMode **44/44**, MPPM playtest on Matins passed its full checklist (issue #40 comment has the record).
- **Generated assets are committed** (Matins scene, Waygate/ExfilSite/Hatch prefabs, materials, LayerCondition observer asset) in c5982f8. CI does not run generators: any PR that changes a generator must also commit the regenerated output.
- **Editor is 6000.3.29f1** (6.3 LTS; bumped from 19f1 for two Unity security advisories). Installed at `D:\Unity\Editors\6000.3.29f1`. `docs/testing.md` CLI paths match.

## What Phase 2 built (read the code, not this list, for detail)

| Concept | Where |
|---|---|
| Layer id per networked object, server truth | `Runtime/LayerMember.cs` (`StampBeforeSpawn`, `ServerLayerOf`) |
| Per-connection Layer + observer rebuilds | `Runtime/LayerService.cs` (beside the NetworkManager) |
| Observer rule: same Layer only (ADR-0008) | `Runtime/LayerCondition.cs` + `Observers/LayerCondition.asset` |
| Layer metadata, Small/Large tag (data only until Phase 3 AI) | `Runtime/LayerInfo.cs` |
| Doors, arrival slots, fade | `Door.cs`, `ArrivalSlots.cs`, `ScreenFade.cs`, `RaiderMovement.Transit` |
| Waygate pair with shared cooldown | `Waygate.cs`, `WaygateSpawner.cs` |
| Exfil call → countdown → extract zone | `ExfilSite.cs`, `ExtractionService.cs`, `ExtractionClient.cs` (F8 = debug rejoin) |
| Hatch + Hatch Key, 5 s channel | `Hatch.cs`, `HatchKeys.cs`; cancels on `Health.Damaged`, range, Layer change |
| Graybox Matins (3 districts, 8 interiors, Undercroft, posts) | `Editor/MatinsGenerator.cs` (`BuildMap` is testable without a scene) |
| HUD lines (Layer, Waygate, Hatch, Exfil) | `Runtime/RaidHud.cs` |

Interact priority on E: door > Waygate > Hatch > Exfil, with a reconciled release latch and a tick-countdown cooldown in `RaiderMovement`.

## Next: Phase 3 — The Choir

From `docs/vertical-slice-plan.md`: the five units (Chorister swarm, Power patrol+bolt, Herald flee+summon, Dominion armored beam, Throne hunt), the Escalation director (spawn budget rising with time; Exfil calls spike it; Heralds feed it), Choir Remnant drops. Exit: a raid has a dramatic arc.

Pipeline step: **PRD first** (`/to-prd`), grounded in `CONTEXT.md` + ADRs, then `/to-issues`. Phase-2 hooks Phase 3 must consume:

- **Small/Large interior tag** (`LayerInfo.Size`): AI may enter Large interiors only (PRD #31 story 21, ADR-0004).
- **Exfil call** is "loud" only visually today; the Escalation spike hooks into `ExfilSite`'s Idle→Called transition.
- **`HostileEmitter`** is Phase-1 scaffolding (Side.Hostile, stationary). Choir units replace it; keep its team rule and server-only simulation.
- **Layer scoping**: AI objects must be stamped with `LayerMember.StampBeforeSpawn` before spawn, or they replicate to nobody / everybody wrongly.

## Non-negotiables

- Server-authoritative everything (ADR-0005); clients send intent only.
- CSP shape for owned Raiders (ADR-0007): replicate/reconcile in `RaiderMovement`. AI is server-simulated, not predicted.
- One physics scene, Layers separated by space (ADR-0008, ≥100 m margin, enforced by `MatinsLayoutTests`).
- Domain language from `CONTEXT.md` in code and docs.
- Per-issue 8-step flow from `CLAUDE.md`.

## Landmines (all hit for real)

1. **In-process test harness shares one physics scene across server + clients.** Every Raider exists once per peer at the same pose. Any server physics query (raycast, overlap) must ignore colliders that belong to another `NetworkManager`. Fixed in `ArrivalSlots` and `Projectile.TrySweepHit`; AI perception/targeting in Phase 3 must do the same, or two-client tests silently fail.
2. **FishNet asmdef references.** Calling `ServerObjects.RebuildObservers` needs `GameKit.Dependencies` in `LightRaiders.Runtime.asmdef` (CS0012 otherwise). New FishNet APIs may pull in more GameKit types.
3. **Agents cannot compile.** No Unity in the cloud sandbox and CI is paused, so every PR is unverified until Fernando runs Unity. Fernando batches verification into one session per phase (do not ask him to test per PR). Keep a running checklist issue like #40.
4. **Stale MPPM clones** after networked prefab changes fail with "Prefab Id not found": restart virtual players.
5. **Editor layout can come back empty** after Safe Mode or a version change: Window > Layouts > Default.
6. **Flaky test #27** (`OwnerAim_FacingIsObservedByOtherClient`) is still open; read its message before calling a red run a regression.
7. `ReplicateState.IsReplayed` is obsolete (warning only; `ContainsReplayed` is the replacement).

## Open loose ends

- **PR #30 (GameCI + #27 diagnostics) is paused.** New Unity Hub (Licensing Client) no longer produces `Unity_lic.ulf` (`%LOCALAPPDATA%\Unity\licenses` holds a different format), and Fernando's Unity login asks for an email verification code, which GameCI cannot answer. Needs research on the current Personal-license path before `docs/ci.md` is rewritten. Secrets `UNITY_EMAIL`/`UNITY_PASSWORD` already exist; without `UNITY_LICENSE` the job skips.
- **PR #29** (old Phase-1 handoff) is superseded by this document and can be closed.
- **PR #47** (character design briefs) is a separate art track (Phase 6 per the slice plan).

## Exit criteria for Phase 3

A Phase-3 PRD (issue) agreed with Fernando, sliced into issues with dependencies, built, and verified in one batched Unity session: a raid on Matins has a quiet start, rising pressure from Escalation, and a Throne-driven exit.
