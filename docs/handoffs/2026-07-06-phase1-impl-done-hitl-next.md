# Handoff: Phase 1 core-feel — all implementation slices done; HITL gates #21 then #20 remain

Written 2026-07-06 at the end of the session that shipped issues #15–#19 (PRs #22–#26). Supersedes the Phase-0 handoffs as the session entry point for Phase 1.

## State

- **Phase 1 implementation slices #15–#19 are all DONE and merged to `main`:**
  - **#15** (PR #22) — Projectile collision: per-tick segment raycast sweep, server-only (ADR-0005), shots consumed by world geometry.
  - **#16** (PR #23) — Damage model: replicated `Health` (`SyncVar<int>` Shield 50 / Health 100, Shield-before-Health, no regen, `Died` event), a `Side {Raider, Hostile}` team rule (friendly fire off), graybox `DummyTarget` + `DummyTargetSpawner` (respawn ~3s).
  - **#17** (PR #24) — `HostileEmitter` fires the shared projectile at the nearest Raider on a ~1.5s cadence within 15m (Side.Hostile); Raiders now carry `Health` (Side.Raider) so they take damage.
  - **#19** (PR #25) — Graybox HUD: client-local `RaiderHud` (screen Shield/Health bars) + `WorldSpaceHealthBars` (dynamic orphan-free bars over every damageable), reading only replicated Health; `HudGenerator` authors the Canvas.
  - **#18** (PR #26) — Raider death: on zero Health the server removes the Raider, drops a `LootBag` (~60s lifetime) at the death spot, and after ~3s respawns it at a spawn point **owned by the same connection** (`ServerManager.Spawn(nob, owner)`) so input + camera reattach. Pieces: `LootBag`, `RaiderDeathHandler` (on the Raider, bridges `Health.Died`), `RaiderRespawner` (on NetworkSession; does NOT spawn on connect — PlayerSpawner still owns the initial spawn).
- **Test suite: 26 PlayMode tests, all green** except one perpetually-flaky Phase-0 test — see Landmines. CLI gate mechanics in `docs/testing.md`.
- `main` is clean. Each slice ran the full per-issue flow (implement → 3 parallel code-review subagents → fix → merge + close) with autonomous merges (no merge-approval stops — see the pr-autonomy memory / napkin directive #6).

## What remains, in order

Only the two **HITL** gates and the PRD are open. **Do #21 before #20** (Fernando judges prediction against a baseline he has already felt).

### 1. #21 — MPPM three-window acceptance playtest (HITL — Fernando runs it)

This is the Phase-1 milestone: does the graybox combat loop *feel* right, agreed across all three windows.

- ⚠️ **MPPM LANDMINE — restart the virtual players first.** Many networked prefabs changed/were added this session (the Raider gained Health + RaiderDeathHandler; DummyTarget, HostileEmitter, and LootBag are new). Stale clones fail with "Prefab Id not found." Restart the MPPM virtual players; keep Domain Reload on.
- Press Play in the main editor (it hosts server+client; the virtual players auto-join as clients). Watch all three windows for:
  - Move (WASD), 360° aim (mouse), fire (LMB) — a projectile flies and is **consumed by walls/obstacles** (no ghosting).
  - Fire at a **crimson dummy target** — its Shield bar drops, then Health, it breaks (despawns), and respawns ~3s later at full.
  - The **purple hostile emitters shoot back** — your Raider's HUD Shield/Health drop (Shield first).
  - **Die → an amber loot bag drops → you respawn ~3s later** at a spawn point, full bars, **camera still following you**, and you can move/aim/fire again.
  - **World-space bars** float over every damageable (targets, emitters, other Raiders); your own **screen HUD** bars track your Shield/Health.
  - All three windows agree.
- Record the outcome on #21 and close it.

### 2. #20 — Prediction go/no-go: BUILD the config (decision already made; see the #20 comment dated 2026-07-06)

Fernando's call: don't defer — **build the FishNet CSP prediction config** for a real side-by-side, then judge adopt/defer. Agent work:

- **Reference**: `Assets/FishNet/Demos/Prediction/CharacterController/Scripts/CharacterControllerPrediction.cs` — a `TickNetworkBehaviour` with `[Replicate]`/`[Reconcile]`, `ReplicateData`/`ReconcileData` structs, and the CC `enabled = false/true` dance around reconcile position writes. This is the template for a prediction-enabled `RaiderMovement`.
- **Scope/risk (large, invasive)**: replaces the current server-authoritative shape (owner→ServerRpc intent → server CC sim → NetworkTransform). CSP drives the transform via reconcile (drop NetworkTransform for movement), reworks the intent seam into Replicate input, and reworks the movement/aim/fire test suite. Keep it on a branch until adopted.
- **Latency simulator is a prerequisite** for a feelable comparison — MPPM at ~0ms can't show CSP's benefit. Wire FishNet's latency simulation so the with/without difference is real.
- Record the outcome as **ADR-0007** (evaluated / felt / decided / reopening condition — the PvP milestone reopens by definition). If adopted, the config merges with the full suite green; if deferred after feeling it, preserve on a branch / document to resurrect.

### 3. #14 — the PRD closes when #20 and #21 both close.

## Landmines

- **Flaky test tracked as issue #27**: `RaiderAimTests.OwnerAim_FacingIsObservedByOtherClient` intermittently fails with "Client B never observed A's facing (timed out after 15s)". It fails even in isolation and flips run-to-run; it is a Phase-0 two-client settle flake, unrelated to any combat change. **Every "1 failed" in this session's gate runs was this test.** When gating, confirm the sole failure is #27 before treating the suite as red.
- **MPPM + networked-prefab edits** break running virtual players (restart them) — see #21 above.
- **CLI gate needs the editor CLOSED** (project-folder lock); generation + tests run via Unity batch (`docs/testing.md`). Editor MCP is broken on this machine — Editor.log + CLI only.
- **Generated assets are committed**: after any generator change, run `Light Raiders/Generate Session Assets` (`-executeMethod ...GenerateAll`) and commit the regenerated prefabs/scene/materials/`DefaultPrefabObjects.asset` + Unity-emitted `.meta` files. Comment-only or test-only changes need no regeneration.
- **`ProjectSettings.asset`** shows as modified on `git add` due to LF↔CRLF only (no content) — leave it out of commits.

## What to read first, in order

1. `.claude/napkin.md` (directives + landmines) and the `project-phase-status` / `feedback-pr-autonomy` memories.
2. This document.
3. `gh issue view 21`, `gh issue view 20` (and its 2026-07-06 comment), `gh issue view 14`.
4. `CONTEXT.md` (glossary is law) and `docs/adr/0003`, `0005`.
5. For #20: the FishNet CC-prediction demo path above; `game/Assets/LightRaiders/Runtime/RaiderMovement.cs` (the current model being replaced).

## Exit criteria (Phase 1 closes)

#21's playtest passes (the full combat loop feels right across three windows) AND #20's prediction decision is felt + recorded as ADR-0007 with the suite green in the adopted configuration. Then close #14 and `/handoff` into Phase 2.

## Environment gotchas

- Windows-native, PowerShell; Unity 6000.3.19f1 + URP; FishNet 4.7.2; MPPM. Editor path is hardcoded in the `docs/testing.md` commands — update on an editor upgrade.
- Subagent code-review is the quality gate, then merge + close autonomously — do NOT wait for a merge-approval OK (Fernando was explicit; the auto-mode classifier allows `gh pr merge` given that standing directive, but has blocked autonomous *new-issue* creation unless Fernando asks for it).

---

KICKOFF PROMPT (paste to start the next session):

Read docs/handoffs/2026-07-06-phase1-impl-done-hitl-next.md and pick up Phase 1 from there. All implementation slices (#15–#19) are merged; only the two HITL gates remain. If I have already run the #21 MPPM acceptance playtest, help me record and close it; then start #20 — build the FishNet CSP prediction-enabled RaiderMovement config on a branch (reference the in-repo CharacterController prediction demo), wire a latency simulator so the side-by-side is feelable, keep the full PlayMode suite green, and draft ADR-0007 for my adopt/defer call. If I have NOT yet done the #21 playtest, remind me it is mine to run (restart the MPPM virtual players first — networked prefabs changed) and prep whatever you can for #20 meanwhile. The only expected test failure is the flaky aim test tracked in issue #27; treat the suite as green if that is the sole red.
