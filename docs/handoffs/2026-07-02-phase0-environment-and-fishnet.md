# Handoff: Phase 0 environment build-out — Unity project through FishNet, MPPM unresolved

Written 2026-07-02, continuing directly from `docs/handoffs/2026-07-02-windows-native-migration.md` (read that first — this doc assumes it and doesn't repeat it). This session executed the runbook's remaining setup steps: Unity Editor install, IDE choice, Unity project creation, first commit, and FishNet import. It stops mid-MPPM-setup — a model-availability tangent (Fable 5) ended the session before MPPM could be confirmed working, not any technical blocker in the actual task.

## State at handoff

- **Environment fully verified**: git 2.55.0, `gh` authenticated as fernandolisboa, git-lfs 3.7.1 with `git lfs install` run in this clone. Per `docs/research/phase0-setup-runbook.md` §1.
- **Unity 6000.3.19f1 LTS installed** (confirmed correct LTS patch, not a tech-stream version) with Windows Build Support (IL2CPP) + Documentation modules. A stray non-LTS 6000.5.2f1 that was pre-installed got uninstalled cleanly (registry entry + editor files gone; a couple of KB-sized metadata JSON files under `Program Files` couldn't be removed without admin rights — harmless, cosmetic only).
- **IDE: VS Code, not Rider.** Rider was installed, then uninstalled — ADR-0006 (paid Early Access) directly conflicts with JetBrains' non-commercial license ("commercial intent now or in the future" is disqualifying, and the paid-EA plan is a closed decision, not hypothetical). VS Code has C# Dev Kit (`ms-dotnettools.csdevkit`) and Microsoft's official Unity extension (`VisualStudioToolsForUnity.vstuc`, v1.2.2) installed.
- **Unity Hub**: signed in, active Personal license confirmed (activated 2016 — pre-existing account, not new).
- **Unity project created** at `game/` — Universal 3D (URP) template, targeting 6000.3.19f1. Critically, **Source control provider was set to None** during creation — the default GitHub-integration option in Unity Hub's New Project wizard would have created a *separate* GitHub repo (`fernandolisboa/game`) with its own nested `.git`, fragmenting the project. Watch for this if the project is ever recreated.
- Force Text serialization and Visible Meta Files confirmed directly from `ProjectSettings/EditorSettings.asset` (`m_SerializationMode: 2`) and `ProjectSettings/VersionControlSettings.asset` (`m_Mode: Visible Meta Files`) — both Unity 6 defaults, unchanged.
- **First commit made**: `86b21f0` "Add Unity 6.3 LTS project scaffold (URP) in game/" (64 files). Local only — not pushed to `origin/main`.
- **FishNet 4.7.2 imported** via the Asset Store (free tier, not Pro) — verified: version confirmed via `Assets/FishNet/package.json`, no compile errors in `Editor.log` or `AssetImportWorker*.log`, `Demos/SceneManager` and `Demos/Prediction` folders present.
- **MPPM (Multiplayer Play Mode) setup started but NOT verified working.** Added `com.unity.multiplayer.playmode` to `game/Packages/manifest.json` directly (bypassing GUI since this is a plain Unity Registry package, not Asset-Store-account-gated like FishNet). First attempt used version `3.0.0`, guessed from a WebFetch summary of Unity's docs site — **that was wrong**; 3.0.0 doesn't exist in the registry. Corrected to `2.0.2` (confirmed real and compatible by querying `https://packages.unity.com/com.unity.multiplayer.playmode` directly — registry metadata says `unity: 6000.3`, matching the installed editor exactly). **As of this handoff, resolution is unconfirmed** — `packages-lock.json` and `Library/PackageCache` still showed no trace of `com.unity.multiplayer.playmode` at last check. The user was mid-troubleshooting (asked to close/reopen the Package Manager window to force a re-read) when the session got diverted.

## Uncommitted work right now

`git status` shows, uncommitted:
- `game/Packages/manifest.json` (modified — has the MPPM `2.0.2` line)
- `game/ProjectSettings/ProjectSettings.asset`, `game/ProjectSettings/ShaderGraphSettings.asset` (modified — routine Editor churn from opening the project/importing FishNet, not manually edited)
- `game/Assets/FishNet/` + `game/Assets/FishNet.meta` (untracked — the whole FishNet import)
- `game/Assets/DefaultPrefabObjects.asset` + `.meta` (untracked — FishNet auto-generates this on first import/domain reload)

None of this has been committed. *(Superseded later the same session: everything was committed and pushed, MPPM was validated end-to-end, and the commit-approval rule itself was retired — git operations are autonomous now; see `.claude/napkin.md` directive 6.)*

## Read first, in order

1. `docs/handoffs/2026-07-02-windows-native-migration.md` — the session immediately before this one. Still the prerequisite context.
2. This document.
3. `.claude/napkin.md` — re-read every session per its own rule; may have been updated since this was written.
4. `docs/research/phase0-setup-runbook.md` §4–5 — FishNet and MPPM sections, for whatever remains of MPPM setup and the post-MPPM learning path.

## Immediate next steps

1. **Verify MPPM actually resolved.** Check `game/Packages/packages-lock.json` for `com.unity.multiplayer.playmode`, or `game/Library/PackageCache/` for a matching folder. If still absent, closing/reopening the Package Manager window may not be enough — try fully closing and reopening the whole project via Unity Hub (guaranteed fresh manifest read), or open Package Manager and click directly on the errored entry to retry.
2. Once MPPM resolves: enable 2–3 virtual players (Window > Multiplayer > Multiplayer Play Mode) — first activation builds clones under `Library/VP`, takes minutes. Validate host + clients moving a synced object before going further (runbook §5: timebox one evening; ParrelSync is the documented fallback only if MPPM misbehaves).
3. Decide with the user whether to commit the FishNet import + MPPM manifest change now (one or two logical commits) — ask first, per the project's commit rule.
4. After MPPM is validated, the runbook's Week-1 learning path (`docs/research/phase0-setup-runbook.md` §7) and the vertical-slice-plan's "Week one, concretely" section pick up: Unity Essentials pathway, then progressively toward the Phase 0 exit criteria.

## Non-negotiables (unchanged from prior handoff, still apply)

Autonomy-first questioning (one question at a time, recommended option, never loop on timeout — restate as plain text and end turn instead), English only, `CONTEXT.md` glossary discipline, ADRs 0001–0006 closed. Git is autonomous as of later this session — no commit/push/PR go-aheads (napkin directive 6 supersedes the older rule stated here and in prior handoffs). Full detail in `.claude/napkin.md` — not duplicated here.

## Landmines (new this session, in addition to the prior handoff's list)

- **Unity Hub's New Project wizard defaults "Source control provider" to GitHub and will create a separate repo + push an initial commit if left on.** Always set it to None for this project; git is handled entirely by the existing root repo.
- **Don't trust Unity docs pages for exact package version numbers.** The `@latest` redirect and version-specific manual pages can reference versions that aren't actually published (this session's `3.0.0` vs. the real `2.0.2` for MPPM). Query `https://packages.unity.com/<package-id>` directly for ground truth before hand-editing `manifest.json`.
- **Editing `Packages/manifest.json` externally doesn't reliably trigger a resolve on simple Editor alt-tab/refocus**, unlike C# script changes. Opening the Package Manager window explicitly is a stronger trigger; a full project close/reopen via Unity Hub is the most reliable fallback.
- **Unity Hub's own CLI (`Unity Hub.exe -- --headless ...`) has no project-creation or uninstall commands** — only `editors`, `install-path`, `install`, `install-modules`. Project creation is GUI-only; editor uninstalls go through the registered Windows uninstaller (`<EditorPath>\Editor\Uninstall.exe /S` for silent) or Programs & Features, not Hub itself.
- **Claude Fable 5 is not available on this account/plan** (`/model claude-fable-5` returns "not currently available for your account"). Don't revisit this unless the user says their plan/access has changed — proceed on whatever the default model is.

## Environment gotchas

- Unity Hub's own headless CLI scan (`editors -i`) can return stale/empty results for a few seconds right after an install or uninstall completes — re-running it resolves the discrepancy. Don't treat one empty/wrong result as a real problem without a second check.
- Deleting files under `C:\Program Files\...` requires admin rights this shell doesn't have — expect `Remove-Item` access-denied on anything Unity/Hub installed there, even after the official uninstaller has already done the real work.
- `Editor.log` lives outside the project, at `%LOCALAPPDATA%\Unity\Editor\Editor.log` (not in the project's own `Logs/` folder, which holds per-subsystem logs like `AssetImportWorker*.log` instead).

## Exit criteria (unchanged, from `docs/vertical-slice-plan.md`)

Phase 0: a capsule moves and shoots a projectile in a networked session with a second client connected. Still not started — this session was entirely environment setup.

## Suggested skills for next session

- No specialized skill needed to resume MPPM verification/setup — it's a direct continuation of manual runbook execution, same pattern as this session.
- Once MPPM is validated and the Week-1 learning path begins producing actual code (the graybox top-down movement/shooting work in runbook §7 weeks 2–3), `tdd` becomes relevant for shooter mechanics built test-first, and `run` for actually launching/screenshotting the project once there's something to see.
- When Phase 0's exit criteria are met, run `/handoff` again to close out this phase cleanly before CLAUDE.md's pipeline moves into PRD/Issues for Phase 1.
