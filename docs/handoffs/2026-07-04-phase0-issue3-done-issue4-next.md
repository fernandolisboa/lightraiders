# Handoff: Phase 0 development — merge PR #10, then issue #4 (Camera)

Written 2026-07-04 at the end of the session that closed issue #2 and implemented issue #3. Supersedes `2026-07-03-phase0-development-kickoff.md` as the session entry point (that doc's workflow facts still apply and are not repeated here).

## State

- **Issue #2 (Bootstrap networked session): DONE.** PR #8 merged, PR #9 (re-serialization follow-up) merged, issue closed. HITL passed: editor play-mode host, MPPM three-window check, CLI test run.
- **Issue #3 (Server-authoritative WASD movement): implemented, validated, NOT merged.** PR #10 (https://github.com/fernandolisboa/lightraiders/pull/10) is open on branch `feat/issue-3-server-authoritative-movement` awaiting Fernando's merge — the permission layer declines agent self-merges of unreviewed PRs, so **one explicit user OK (or a GitHub merge click) is the only pending action**. Full scope, review trail, and the MPPM-restart note are in the PR body; 8/8 PlayMode tests green headlessly, console clean.
- **This handoff file is committed on the PR #10 branch** (main pushes are gated the same way).
- The local repo sits on `feat/issue-3-server-authoritative-movement` with a clean tree; local `main` equals `origin/main`.
- Tracker graph: after #3 merges → **#4 Camera is next** (already unblocked by #2; read `gh issue view 4`), then #5 Aim (needs #3), #6 Projectile, #7 MPPM acceptance (closes PRD #1 and Phase 0).

## Why the session stopped here

Issue #4's implementation will regenerate `Raider.prefab` / `BootstrapArena.unity` via the editor generators; branching it off main before PR #10 merges guarantees ugly YAML conflicts with #10's regenerated assets. Merge first, then branch.

## What to read first, in order

1. `.claude/napkin.md` — every session; note Environment #5 (Unity Editor MCP is broken on this machine — use `%LOCALAPPDATA%\Unity\Editor\Editor.log` and the CLI batch runs instead, never retry the MCP tools).
2. This document.
3. `gh pr view 10` and `gh issue view 4` (and `gh issue view 1` for the PRD's camera decisions: per-client, non-networked, angled top-down rig at fixed pitch following the locally owned capsule).
4. `docs/testing.md` — CLI invocation, exit codes, editor-closed constraint, MPPM landmines.

## Established patterns issue #4 must reuse (all on the PR #10 branch)

- **Per-issue 8-step flow** with fresh subagents per step (CLAUDE.md); plan review before implementation has caught real defects both times — do not skip it.
- **First-party code** in `game/Assets/LightRaiders/` under the three existing asmdefs; scenes/prefabs only via the idempotent editor generators (`RaiderPrefabGenerator`, `ArenaSceneGenerator`); **never hand-author .meta files** (one implementer tried; the metas were deleted and Unity generated them at the CLI run — commit Unity's, always).
- **Intent seam** (`IRaiderIntentProvider` / `ScriptedRaiderIntentProvider`) for anything input-driven; tests inject scripted providers *before* baselining (hardware-provider bleed hygiene).
- **Test conventions**: `NetworkSessionHarness` (per-test ports from 7801, `CreateNetworkManager(withSpawner, spawns)`), in-test geometry (the default test scene is empty!), network-visible assertions only, inequality thresholds, positive controls for negative tests. Camera is per-client and non-networked — issue #4's tests may be simpler (no second client needed for most), but keep the same discipline.
- **Serialized FishNet fields** (`_clientAuthoritative` etc.) set via SerializedObject with throwing null-guards naming the field.

## Landmines (new/confirmed this session)

- **CLI runs require every Unity process closed** (main editor AND MPPM virtual-player clones). Graceful close works: `(Get-Process Unity).CloseMainWindow()` on the main editor cascades to clones within ~20 s; verify with `Get-Process Unity` before launching batch runs. Never delete `Library` locks.
- **`AddComponent<NetworkManager>` in play mode logs an unavoidable FishNet error** (OnValidate fires before `SpawnablePrefabs` can be assigned) — the harness `LogAssert.Expect`s it once per manager; assign `SpawnablePrefabs` *before* any `SerializedObject.ApplyModifiedProperties` call or the error fires twice.
- **CharacterController + NetworkTransform**: `ComponentConfigurationType.CharacterController` auto-disables the CC on non-server instances; the root CC is the Raider's *sole* collider (visual child collider is removed by the generator).
- **CC slides along faces instead of stalling** — obstacle-collision tests need homing intent plus a convergence window before stall snapshots (see `Raider_IsBlockedByObstacle`).
- **Editor GUI sessions re-serialize generated assets** (URP `_Color` sync, scene fileID churn). Expected; absorb as a small commit/PR, don't investigate.
- **Direct pushes to `main` and same-session self-merges of unreviewed PRs are declined by the permission layer** — route everything through feature-branch PRs and get one explicit user OK per merge.

## Exit criteria (unchanged)

Phase 0 closes when issue #7's MPPM acceptance playtest passes: a capsule moves and shoots a projectile in a networked session with a second client connected. After #3 merges, movement is real; #4 gives each client its own camera; #5/#6 add aim and projectile.

## Suggested skills

- `tdd` for issue #4's implement step if the camera logic grows beyond a follow transform (likely overkill — the 8-step flow's own test step may suffice).
- `/code-review` at step 6 alongside the specialized review subagents.
- `diagnose` if FishNet/MPPM misbehaves at runtime.
- `handoff` again when stopping.
