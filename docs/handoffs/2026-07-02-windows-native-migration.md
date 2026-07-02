# Handoff: Windows-native migration + Phase 0 start

Written 2026-07-02, late in the Phase 0 kickoff session, which ran in WSL2. WSL2 crashed repeatedly (killing background agent work and wiping `/tmp`), and the environment research independently concluded Windows-native is correct for a Unity project regardless — so development moves to **Windows: repo at `C:\dev\lightraiders`, Claude Code native in PowerShell**. This document lets a fresh Windows session pick up with zero context loss. Everything is committed and pushed; there is no state outside this repo worth recovering.

## State at handoff (all pushed to `main`)

- **Design + skills scaffolding done** (see `docs/handoffs/2026-07-02-phase0-kickoff.md` for the design-session handoff — still valid, read it too): `CONTEXT.md`, ADRs 0001–0006, `docs/vertical-slice-plan.md`, `docs/agents/` (issue tracker = GitHub Issues via `gh`; triage/phase/area labels created on the repo), `CLAUDE.md` workflow.
- **Phase 0 research done**: `docs/research/phase0-setup-runbook.md` — a verified, cited runbook covering the environment decision, Unity 6.3 LTS install, repo/git-LFS hygiene, FishNet, MPPM second-client testing, IDE choice, and the 4-week learning path. **This is the script for the next steps; follow it top to bottom.**
- **Git hygiene pre-staged**: `.gitattributes` (LFS rules + Unity YAML merge routing + eol normalization) is already at repo root, deliberately committed before any binary exists. Root `.gitignore` excludes `.claude/settings.local.json`. The Unity-specific `.gitignore` is added inside the Unity project subfolder when the project is created.
- **Nothing implemented yet**: no Unity project, no code. Phase 0 exit criteria (from `docs/vertical-slice-plan.md`): a capsule moves and shoots a projectile in a networked session with a second client connected.

## Read first, in order

1. `.claude/napkin.md` — session-critical rules (user interaction style, domain guardrails, known gotchas). The napkin skill applies every session.
2. `docs/research/phase0-setup-runbook.md` — the setup script.
3. `docs/vertical-slice-plan.md` — Phase 0 definition + "Week one, concretely".
4. `CONTEXT.md` + skim ADRs — the language and the closed decisions.

## Environment expectations on Windows

- Prerequisites the user installs in PowerShell (runbook §1): Git for Windows, GitHub CLI, Unity Hub via winget; Claude Code native; `gh auth login`; clone to `C:\dev\lightraiders`; `git lfs install`. Verify each before relying on it (`git --version`, `gh auth status`, `git lfs version`).
- Native Windows Claude Code has no bash sandboxing; the shell is Git Bash via Git for Windows. Expect Windows paths in Unity-generated `.sln`/`.csproj` — that is correct and wanted.
- The old WSL copy at `/home/ferna/projects/lightraiders` is **stale by definition** from now on. Never edit or pull from it; it may lag `main`.
- Per-path agent memory from the WSL sessions does NOT auto-load on Windows. Rebuild memory from this repo's docs if useful; the napkin carries the durable rules.

## Non-negotiables (the user has corrected agents on these)

- **Async answers**: the user replies to questions in minutes-or-longer. On AskUserQuestion timeout, re-ask the same question and wait. Never proceed on a substituted answer.
- **One question at a time**, recommended option marked, honest plain-language trade-offs.
- **English only** in every artifact.
- **Glossary discipline** (`CONTEXT.md`): canonical terms exactly; multiword headwords are proper nouns (Skill Tree, Safe Pocket). Never write past the Hymn mystery.
- **Commits only with the user's go-ahead.**

## Landmines

- MPPM + FishNet networked-prefab edits while virtual players run → "Prefab Id not found"; unfixed Unity limitation (FishNet #916). Keep Domain Reload enabled; restart virtual players after prefab edits.
- FishNet updates: delete `Assets/FishNet` before re-importing. Never pre-4.6.19 on Unity 6.
- Unity version: latest 6000.3.x patch only (not 6000.0 — support ends Oct 2026; not 6000.3.0f1 — shipped with a URP compile regression).
- LFS: `.gitattributes` is already in place; run `git lfs install` per machine BEFORE any binary is added, or history rewrites await.
- 2022–2023 FishNet YouTube tutorials are v3-era and will not compile on v4; the GitBook docs are canonical.

## Suggested first moves on Windows

1. Verify the environment checklist above; fix anything missing with the user (they run installers).
2. Walk the user through Unity Hub sign-in (free Personal license) + Unity 6.3 LTS install with the runbook §2 module list.
3. Create the Unity project in a subfolder (runbook §3 — suggest `game/`), drop the canonical Unity `.gitignore` inside it, verify Force Text + Visible Meta Files, first commit (with go-ahead).
4. Start the Week-1 learning plan (runbook §7) and Week-one-concretely from the slice plan: FishNet import + example scenes + two local clients via MPPM.
