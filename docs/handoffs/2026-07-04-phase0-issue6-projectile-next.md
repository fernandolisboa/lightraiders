# Handoff: Phase 0 development — issue #6 (projectile), then #7 closes the phase

Written 2026-07-04 at the end of the session that shipped issues #4 (camera) and #5 (aim) and merged everything through PR #12. Supersedes `2026-07-04-phase0-issue3-done-issue4-next.md` as the session entry point.

## State

- **Issues #2–#5 all DONE and merged to `main`** (PRs #8–#12; #9 was re-serialization housekeeping). The graybox now has: auto-host/auto-join session bootstrap, server-authoritative WASD movement through the intent seam, a per-client camera rig at fixed 55° pitch, and 360° mouse aim with server-set facing replicated to all clients plus a generated orange AimIndicator bar on the Raider prefab. Fernando playtested through #5 — all confirmed working in MPPM three-window sessions.
- **Test suite: 13/13 PlayMode tests green headlessly** (4 session + 4 movement + 2 camera + 3 aim). CLI invocation in `docs/testing.md`.
- **Tracker**: #6 (Projectile, AFK) is the only remaining implementation issue; #7 (MPPM acceptance playtest, HITL) closes PRD #1 and Phase 0. Read `gh issue view 6` and the PRD's projectile decisions in `gh issue view 1` (server validates a per-Raider cooldown, spawns a networked projectile on a flat straight line at constant speed, despawns on lifetime; no damage/health; plain spawned network objects, no pooling, no predicted spawning).

## New standing directive (napkin #7 — changes the cadence)

**Batch HITL.** Fernando: "I trust your testing for the smaller things; let's proceed with a much larger change that I can test." Do NOT stop for a playtest after #6 — run it end-to-end on green tests, ask for the PR merge OK (the permission layer requires his word per merge; self-merges are declined), and roll straight into #7, which IS the milestone playtest: capsule moves and shoots a projectile in a networked session with a second client connected. One interruption, phase closes.

## What to read first, in order

1. `.claude/napkin.md` — directives 1–7 and all landmines; Environment #5 (Unity Editor MCP broken on this machine — Editor.log + CLI only, never retry the MCP tools).
2. This document.
3. `gh issue view 6`, `gh issue view 7`, and issue #1's Implementation/Testing Decisions (projectile bullets).
4. `docs/testing.md` — CLI gate mechanics.

## Session workflow that has worked (keep it)

Ultracode is on for this project's sessions when Fernando enables it (`/effort ultracode`). The proven shape per issue:

1. **Explore/plan/verify workflow** (one Workflow call): 4 parallel read-only explorers with disjoint slices → 1 planner over their combined reports → 3 adversarial plan verifiers (fishnet-api / test-design / conventions lenses, structured verdicts). Every run so far caught MAJOR defects before implementation (internal-API misuse, vacuous test designs, overflow holes).
2. **Implement** via one general-purpose agent with the corrected plan inline (small commits, no push, no Unity runs, never hand-author .meta — one agent tried; the metas were deleted and Unity's committed instead).
3. **Review workflow** (one Workflow call): 3 finder lenses (bugs / test-vacuity / conventions) with structured findings → dedup → one adversarial verifier per finding (default-refute). Confirmed-only findings get fixed; rejects have all been genuine false positives.
4. **Apply fixes inline** (they're small and precisely specified by then).
5. **CLI gate**: close Unity gracefully if open (`(Get-Process -Id <main>).CloseMainWindow()`, cascades to MPPM clones in ~25s; only when the title bar shows no unsaved marker), `GenerateAll` via `-executeMethod`, verify prefab/scene structure by grepping the YAML, run PlayMode tests (`-runTests`, expect 13 existing + new), scan logs (sanctioned noise: the harness's expected "SpawnablePrefabs is null" error, FishNet's own CS0618s in UpgradeFromMirrorMenu.cs, Unity licensing/AI spam), commit regenerated assets + Unity-emitted metas, push, PR with the review trail, stop for merge OK.

## Facts issue #6 will need

- **Fire intent**: `RaiderIntent.FirePressed` already rides the per-tick unreliable ServerRpc; `RaiderInputIntentProvider` must map it (the `Player/Attack` action exists in `Assets/InputSystem_Actions.inputactions`, bound to left mouse button — an action-based read needs enabling like `Player/Move`; the aim path reads `Mouse.current` directly as precedent). Edge-detection matters: FirePressed is level-state at 30Hz; the server enforces the cooldown anyway (PRD), so level-semantics + server cooldown is the simple correct combo.
- **Server spawning**: `ServerManager.Spawn(nob)` (no owner needed for projectiles); `NetworkManager.GetPooledInstantiated(prefab, position, rotation, asServer: true)` is the FishNet-idiomatic instantiate (PlayerSpawner.cs shows the pattern). Projectile prefab needs its own generator function in `RaiderPrefabGenerator` (or a sibling) + auto-registration via the existing `RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs()` call. **Prefab changes → MPPM restart landmine applies.**
- **Projectile motion**: server-side straight line at constant speed (transform translate per tick or a NetworkTransform-replicated server mover mirroring RaiderMovement's shape); despawn on lifetime via `ServerManager.Despawn(nob)` or `nob.Despawn()`. No damage/health/hit reactions (out of scope, Phase 1).
- **Spawn origin/direction**: from the shooter's position along its server-side facing (`transform.forward` — issue #5 made facing server-authoritative, so the server already owns the truth).
- **Tests** (established conventions): in-process multi-NM harness, per-test geometry, scripted intents, network-visible assertions (projectile appears in `Objects.Spawned` on server AND observer, count-based cooldown assertion — hold FirePressed for N seconds, assert spawn count ≈ N/cooldown not N*30, despawn-on-lifetime with tick/realtime tolerance), anti-vacuity positive controls, tick-gated holds (`TimeManager.Tick`) instead of wall-clock waits where processing must be guaranteed.
- **CountRaiders-style helpers** live on `NetworkSessionHarness` (also `FindOwnedRaider`, `FindOnView`, `PlanarDistance`, `PlanarAngle`, `SettleServerRaider`); a projectile counter will fit the same pattern (filter `Objects.Spawned` by a `Projectile`-marker component — mind the glossary, CONTEXT.md has no projectile term, plain `Projectile` is fine, never `Bullet`/`Player*`).

## Landmines (full list in napkin; the ones that bit this session)

- Default struct values can be *valid* domain values: `AimPoint=(0,0,0)` meant "face world origin" and snapped every fresh spawn — the NaN sentinel pattern in `RaiderMovement._lastReceivedIntent` is the fix precedent. FirePressed's default `false` is safe, but check cooldown-timer defaults similarly.
- `Infinity < epsilon` is `false` — overflow-harden guards with `float.IsFinite(sqrMagnitude)`.
- Wall-clock test holds pass vacuously under editor stalls — gate on `TimeManager.Tick` advancement (helper precedent `WaitForServerTicks` in `RaiderAimTests`).
- Scene regeneration churns fileIDs (destroy-and-rebuild) — expected, commit it, don't chase byte-stability; the prefab IS byte-stable.
- The permission layer declines direct main pushes, self-merges of unreviewed PRs, and chained destructive git — feature-branch PRs with one user OK per merge is the working groove.

## Exit criteria

Phase 0 closes when #7's acceptance playtest passes: **a capsule moves and shoots a projectile in a networked session with a second client connected** (main editor + 2 MPPM virtual players, all three windows agreeing). After #6 merges, prep #7's checklist for Fernando (restart virtual players — the projectile prefab is new), run it as the single batched HITL, close #7 and #1, then `/handoff` for the PRD/Issues pipeline of Phase 1 (core feel: damage, health, Shield, prediction go/no-go).

## Suggested skills

- The Workflow tool (ultracode) for explore/plan/verify and review/verify — the per-issue shape above; scripts from this session are under the session workflows dir if reference is needed, but writing fresh ones from the shape description is fine.
- `verify` before committing the runtime slice if anything feels under-exercised beyond the PlayMode suite.
- `diagnose` if FishNet/MPPM misbehaves.
- `handoff` when #7 closes Phase 0.
