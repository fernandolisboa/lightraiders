# Handoff: Phase 1 — #21 closed, #20 CSP BUILT + felt; only the adopt/defer decision remains

Written 2026-07-06 at the end of the session that closed the #21 acceptance playtest and built the #20 client-side-prediction config. Supersedes `2026-07-06-phase1-impl-done-hitl-next.md` as the Phase-1 session entry point.

## State

- **#21 MPPM acceptance playtest — PASSED and CLOSED.** The full graybox combat loop feels right across all three windows (move/aim/fire, walls consume shots, crimson dummy takes Shield→Health→breaks→respawns, purple emitters shoot back, die→amber loot bag→respawn owned by the same connection with the camera reattached, world-space + screen HUD bars). Outcome recorded on the issue.
- **#20 prediction — BUILT on branch `feat/issue-20-prediction` (pushed, NOT merged), and Fernando has FELT it.** The full PlayMode suite is green **38/38** via the CLI gate. What is left is **one decision**: adopt or defer, recorded in ADR-0007.
- **#14 (PRD)** closes when #20 resolves.
- **#27** is the tracking issue for the flaky aim test (`OwnerAim_FacingIsObservedByOtherClient`) — the one allowed red when gating. It passed on the final #20 run.

### What the #20 build actually is (on `feat/issue-20-prediction`)

- `RaiderMovement` rewritten from the Phase-0 server-authoritative RPC shape (`NetworkBehaviour` + `TimeManager.OnTick` + `ServerRpcSendIntent` + NetworkTransform) to **FishNet CSP**: a `TickNetworkBehaviour` with `[Replicate] PerformReplicate(ReplicateData)` + `[Reconcile] PerformReconcile(ReconcileData)`. **Position AND facing are predicted** (ADR-0001 decoupling preserved); reconcile drives the transform with the CharacterController `enabled=false → write → enabled=true` dance. **NetworkTransform is dropped** for the Raider (reconcile owns position + rotation).
- **Fire stays SERVER-ONLY (ADR-0005):** `FirePressed` rides `ReplicateData` but is consumed only on the server branch of the replicate (`if (IsServerInitialized)`); `RaiderWeapon` / `Projectile` are untouched, no client-predicted projectiles.
- **The one non-obvious wiring bug, found and fixed mid-gate:** the NetworkObject's **`_enablePrediction`** flag must be **true** or FishNet runs the owner/server replicate but never distributes reconcile/state to non-owner **observers** — spectators and cross-view damage freeze. The first CLI run was 4-red for exactly this (all observer/cross-view assertions); the second was 38/38 after setting the flag in `RaiderPrefabGenerator`. Confirmed against the in-repo demo prefab (`_enablePrediction: 1`).
- The **intent seam is preserved**: `RaiderIntent` is still the provider contract, adapted into `ReplicateData` in `BuildMoveData`; `IRaiderIntentProvider` / `ScriptedRaiderIntentProvider` / `SetIntentProvider` all survive, which is why the existing suite reads the still-authoritative server surface unchanged.
- **`LatencyToggle`** (new debug MonoBehaviour on the scene camera rig) drives `NetworkManager.TransportManager.LatencySimulator`: **F9** on/off, **F10/F11** ±20ms, on-screen label. Host doubles latency (100ms one-way ≈ 200ms felt RTT in the main editor window). Also usable on the old build for an A/B.
- **Tests:** new `RaiderPredictionTests` (2 tests) guard owner-local prediction + reconcile convergence at 0ms and under 80ms simulated latency. Existing suites unchanged except one honesty comment.

## The pending decision — this is the whole next step

**Fernando has already run the feel-test.** The remaining action is to record his verdict in `docs/adr/0007-client-side-prediction.md` (STATUS is currently **BUILT**, Decision **OPEN**; the Evaluated section is filled, only **Felt** and **Decided** are blank). Ask him for adopt or defer, then:

- **If ADOPT:**
  1. Run the per-issue **subagent code-review** (multiple specialized reviewers) on the branch diff vs `main` — this is the quality gate that has NOT yet run for #20.
  2. Apply any real findings; re-run the CLI gate (must stay 38/38, only #27 allowed red).
  3. Fill ADR-0007 (STATUS: ACCEPTED, Felt + Decided), commit on the branch.
  4. Merge `feat/issue-20-prediction` to `main`, close #20, then close #14 (PRD). Regenerate + commit assets if the review changed any generator.
- **If DEFER:**
  1. Fill ADR-0007 (STATUS: DEFERRED, Felt + Decided + the reopening condition — PvP milestone reopens by definition), commit on the branch.
  2. Close #20 as deferred with a pointer to the branch + ADR as the resurrection point; close #14.
  3. Do NOT merge the movement rewrite to `main` — the branch is the preserved artifact.

Either path: **do not merge or run the code-review before the human verdict** — #20 is a HITL gate, not the autonomous-merge path.

## What to read first, in order

1. `.claude/napkin.md` and the `project-phase-status` / `feedback-pr-autonomy` memories.
2. This document, then `gh issue view 20` and `docs/adr/0007-client-side-prediction.md`.
3. `docs/research/issue-20-prediction-build-plan.md` (the design the build followed) and `CONTEXT.md` + ADR-0005 (server-authority is non-negotiable) / ADR-0001 (facing decoupled from motion).
4. The branch diff: `git diff main..feat/issue-20-prediction` — the real code under review. Key files: `Runtime/RaiderMovement.cs`, `Runtime/LatencyToggle.cs`, `Editor/RaiderPrefabGenerator.cs`, `Tests/PlayMode/RaiderPredictionTests.cs`.

## Non-negotiable principles

- **Server authority (ADR-0005) survives CSP:** the server's replicate is the truth a reconcile snaps the owner to; fire is never client-predicted. Do not weaken this to make prediction smoother.
- **Facing stays decoupled from motion (ADR-0001).** The build predicts both independently; keep them separate.
- **CONTEXT.md glossary is law**; ADRs 0001–0006 are closed.

## Landmines

- **MPPM + networked-prefab edits:** the Raider prefab changed this session — **restart the MPPM virtual players** before any further playtest or clones fail with "Prefab Id not found." Keep Domain Reload on.
- **`_enablePrediction` on the NetworkObject** is load-bearing for observer sync — see above. If a future regen or FishNet upgrade drops it, spectators freeze silently (tests catch it: the observer/cross-view assertions go red).
- **CLI gate needs the editor CLOSED** (project-folder lock). Generation + tests run via Unity batch (`docs/testing.md`). Editor MCP is broken on this machine — Editor.log + CLI only. Note: launching `Unity.exe` via PowerShell `&` returns immediately (GUI app); use `Start-Process -Wait -PassThru` to block and capture the real exit code (0 pass / 2 test failures / 3 other).
- **Generated assets are committed:** any generator change needs `GenerateAll` (editor closed) + commit of the regenerated prefab/scene/materials/`DefaultPrefabObjects.asset` + `.meta`. Comment-only / test-only changes need no regen. Benign LF↔CRLF churn on materials / `ProjectSettings.asset` / `DefaultPrefabObjects.asset` — leave those out unless they carry a real content diff (URP's `_Color`→`_BaseColor` sync on LootBag.mat is one real-but-benign line).
- **Two editors installed** (`6000.3.19f1` and `6000.5.2f1`) — use `6000.3.19f1`, matching `ProjectVersion.txt`.

## Exit criteria (Phase 1 closes)

ADR-0007 records the felt decision; if adopted, the branch is code-reviewed and merged with the suite green (only #27 allowed red) and #20 closes; if deferred, the branch + ADR are preserved and #20 closes deferred. Then #14 closes and `/handoff` into Phase 2.

## Environment gotchas

Windows-native, PowerShell; Unity 6000.3.19f1 + URP; FishNet 4.7.2; MPPM. Editor path is hardcoded in `docs/testing.md`. Git flows through feature-branch PRs; direct pushes to `main` are denied. Subagent code-review is the quality gate before an autonomous merge — but #20's merge waits on Fernando's HITL verdict first.

---

KICKOFF PROMPT (paste to start the next session):

Read docs/handoffs/2026-07-06-issue20-csp-built-decision-pending.md and pick up Phase 1 from there. #21 is closed (passed) and the #20 client-side-prediction config is built on branch feat/issue-20-prediction with the full PlayMode suite green 38/38; I have already felt the with/without-latency side-by-side. The only thing left is my adopt/defer call on ADR-0007. If I tell you ADOPT: run the subagent code-review on the branch diff vs main, apply any real findings, keep the suite green (only the #27 flake allowed red), fill ADR-0007 as ACCEPTED, then merge the branch, close #20, and close #14. If I tell you DEFER: fill ADR-0007 as DEFERRED with the PvP-milestone reopening condition, preserve the branch, close #20 as deferred, and close #14. Do not merge or run the code-review before I give the verdict — #20 is a HITL gate. Landmine: restart the MPPM virtual players before any playtest (the Raider prefab changed), and the CLI gate needs the editor closed.
