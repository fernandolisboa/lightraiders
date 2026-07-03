# Handoff: Phase 0 development — start at issue #2

Written 2026-07-03 at the end of the session that completed the PRD and Issues pipeline phases. Supersedes `2026-07-02-phase0-environment-and-fishnet.md` (whose open items — MPPM resolution, validation, commits — all closed). Everything is committed and pushed to `main`; the working tree is clean; there is no state outside this repo and the GitHub tracker.

## Two rule changes that invalidate parts of older docs

Both landed late on 2026-07-02 and are recorded as napkin directives — **the napkin wins over anything older**:

1. **Development is fully AI-driven** (napkin directive 5). Fernando does not study Unity/game-dev and takes no courses; agents write all code and configuration. He directs, clicks Editor GUI steps agents can't reach, and playtests. The runbook §7 learning path is superseded; the vertical-slice plan's Phase 0 and "Week one" sections were rewritten accordingly.
2. **Git is autonomous** (napkin directive 6). No commit/push/PR go-aheads — run the CLAUDE.md per-issue 8-step flow end to end, including PR merge and issue close when green. Confirm only genuinely destructive/irreversible operations. Older handoffs saying "commits only with go-ahead" are dead on this point.

## State

- **Pipeline position**: Domain ✅ → PRD ✅ → Issues ✅ → **Development starts now**.
- **Tracker** (all conventions in `docs/agents/issue-tracker.md`, labels in `docs/agents/triage-labels.md`): #1 is the Phase 0 PRD; #2–#7 are its tracer-bullet slices with real dependency links. **#2 (Bootstrap networked session, HITL) is the only unblocked issue — start there.** Graph: #2 → {#3 Movement, #4 Camera} → #5 Aim (after #3) → #6 Projectile → #7 MPPM acceptance (closes #1 and Phase 0).
- **Environment, all validated end-to-end on 2026-07-02**: Unity 6000.3.19f1 LTS (installed at `C:\Program Files\Unity\Hub\Editor\6000.3.19f1`), URP project at `game/`, FishNet 4.7.2 at `game/Assets/FishNet`, MPPM 2.0.2 with 2 virtual players (three-window sync confirmed via FishNet's prediction demo), Active Input Handling = Both, VS Code with C# Dev Kit + Unity extension. Repo hygiene done: LFS active (binaries already round-tripped through it), Unity `.gitignore` inside `game/`, Force Text + Visible Meta Files confirmed.
- **No first-party game code exists yet.** `game/Assets` holds only template content and FishNet.

## Read first, in order

1. `.claude/napkin.md` — every session; directives 5–6 and all landmines.
2. This document.
3. `gh issue view 1` and `gh issue view 2 --comments` — the PRD and the first slice. The PRD's Implementation/Testing Decisions sections are the spec; issue bodies carry acceptance criteria.
4. `CONTEXT.md` — glossary discipline (Raider, not player-character; a Phase 0 session is NOT a "raid"); skim ADRs 0001/0003/0005 for the area being touched.

## Executing issue #2 (and the flow for every issue after it)

Run the CLAUDE.md per-issue 8-step flow — explore, plan, review plan, fix plan, implement, multi-agent code review, fix, validate/merge/close — each step in a fresh specialized subagent, autonomously through merge. Work on a branch, PR against `main`. Fernando's involvement in #2 is exactly its three HITL checkpoints (menu click, MPPM look, first CLI test run); park questions per the napkin protocol, never loop.

Facts the implementing agents need that aren't in the issue bodies:

- **Scenes and prefabs are authored via editor scripting** (menu actions that generate/update them) — never hand-written Unity YAML, never hand-written `.meta` files. After external file edits, Unity refocus triggers recompile; package-manifest edits need Package Manager opened (or project reopen) to resolve.
- **CLI test runs**: Unity batch mode locks the project folder — **the editor must be closed** during headless test runs; establishing and documenting the exact invocation is part of #2's acceptance criteria. Editor GUI work and CLI test runs therefore alternate; batch Fernando's GUI checkpoints to minimize switches.
- **Unity package versions**: never trust docs pages for version numbers — query `https://packages.unity.com/<package-id>` (the MPPM "3.0.0" that doesn't exist came from Unity's own docs redirect).
- **MPPM landmine**: restart virtual players after editing any networked prefab ("Prefab Id not found", unfixed Unity limitation); keep Domain Reload enabled.
- **Pink = expected**: FishNet demo materials are BiRP-era and render magenta under URP; never modify third-party assets. First-party materials must be URP from birth.
- **FishNet**: 4.7.2 docs at fish-networking.gitbook.io are canonical; pre-v4 tutorials will not compile; on FishNet updates delete `Assets/FishNet` entirely before re-import.
- **Editor logs**: `%LOCALAPPDATA%\Unity\Editor\Editor.log` (editor), `game/Logs/` (per-subsystem), `game/Library/PackageCache` + `game/Packages/packages-lock.json` (package resolution ground truth).

## Environment gotchas (Windows-native, Git Bash tooling)

- The Bash tool's command transport eats one level of backslash escapes in inline strings — when a test needs exact bytes (JSON with `\\`, Windows paths), write the payload to a file with the Write tool and redirect stdin; don't pipe inline literals.
- `winget install` updates the registry PATH only — running processes don't see new tools until restart. Workaround used for jq: binary copied to `~/bin` (already on PATH). New CLI tools installed mid-session may need the same.
- Anything under `C:\Program Files` needs admin rights this shell lacks — expect access-denied on writes/deletes there.
- Unity Hub's headless CLI can return stale/empty results seconds after an install/uninstall — re-query before believing an anomaly.

## Suggested skills

- **`tdd`** during step 5 (implement) of each issue — the PRD's testing decisions (PlayMode, in-process FishNet host, intent injection, network-visible assertions only) map directly onto red-green-refactor.
- **`/code-review`** at step 6, alongside the CLAUDE.md-mandated specialized review subagents (bugs, security, quality, ADR/CONTEXT adherence).
- **`diagnose`** if FishNet/MPPM behavior goes sideways (reproduce → minimize → hypothesize; the FirstGearGames Discord is the mandatory support channel before GitHub issues).
- **`handoff`** again when #7 closes Phase 0.

## Exit criteria

Phase 0 (from `docs/vertical-slice-plan.md`, unchanged): a capsule moves and shoots a projectile in a networked session with a second client connected — met when issue #7's acceptance playtest passes and #1 closes.
