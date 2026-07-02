# Handoff: Light Raiders — Phase 0 kickoff (Unity setup + learning path)

Written 2026-07-02 at the end of the design-interview session. The next session's job: **start Phase 0** — Unity installation, C#/Unity fundamentals, FishNet examples — per the plan below. No code exists yet.

## Read these first (canonical artifacts — do not re-derive, do not duplicate)

All in `/home/ferna/projects/lightraiders/`:

1. `docs/vertical-slice-plan.md` — **the build plan.** Phase 0 definition, exit criteria, and the "Week one, concretely" section are the next session's script.
2. `CONTEXT.md` — glossary of ~30 canonical terms (Raid, Layer, Escalation, the Choir, Hatch, the Fold, Lumens…). Use these words exactly; challenge drift.
3. `docs/adr/0001..0006` — decided and closed: angled top-down; Unity; network-first co-op PvE slice (PvPvE certain later); discrete-Layer interiors; FishNet (Photon fallback documented in 0005); premium Early Access. Do not re-litigate — the user decided each with full trade-off walk-throughs.
4. `docs/research/reference-research.md` — cited research: Arc Raiders systems, top-down design lessons, 2026 netcode landscape (§4), asset shopping list + prices (§5), genre postmortems (§6).

Persistent memory (auto-loaded via MEMORY.md) also covers project state and user interaction style.

## The user (redact-safe profile)

- Zero game-dev experience; full-time on this project; learning is part of the goal but shipping the slice is the point.
- **Answers questions asynchronously — may take minutes or longer.** When AskUserQuestion times out, re-ask the same question and wait; never substitute your own answer or wind down. (This is a hard rule; violating it earned an explicit correction.)
- Wants one question at a time, each with a clear recommended option and honest trade-offs; asks good clarifying questions (e.g., needed CCU explained twice — plain-language explanations with concrete numbers work well).
- Occasionally misclicks an option — for irreversible-feeling locks, confirm before acting on a surprising answer.
- Has strong instincts and contributes lore/design ideas mid-flow (the Light-temptation angle and the Choir's addicted-usurper origin are theirs) — take these seriously and synthesize rather than override.

## Environment facts that matter for Phase 0

- Claude Code runs in **WSL2 (Linux) on a Windows host**; repo path: `/home/ferna/projects/lightraiders` (WSL side).
- Unity editor must run on the **Windows host**.
- **Open question to resolve early in Phase 0:** where the Unity project lives. Cross-boundary file I/O (WSL ↔ Windows) is slow; options are relocating the repo to the Windows filesystem (Claude Code works via /mnt/c or a Windows session) vs. keeping docs in WSL and the Unity project on Windows in a split layout. Discuss with the user before creating the Unity project.
- Repo is **not yet a git repository** — `git init` + first commit of the docs is week-one work (commit only with the user's go-ahead).

## Phase 0 scope (from the plan — details there)

Install Unity 6 LTS + Hub (Windows), an IDE, create the Unity project, git init; beginner Unity/C# course done inside throwaway scenes in this project; import FishNet (free tier) and run its example scenes; connect two local clients. **Exit criteria: a capsule moves and shoots a projectile in a networked session with a second client connected.**

Watch-items already flagged in the plan's risk section: set up a second-client test workflow early (ParrelSync or Unity multiplayer play mode); validate Synty's style at the real top-down camera angle with free sample packs before any purchase; asset spending stays $0 through Phase 5.

## Open threads (small, non-blocking)

- Working names **Matins** (slice map), **Undercroft**, **Waygate** were assistant-proposed and are standing veto offers.
- The Hymn mystery's final answer is deliberately unresolved; Projects reveal it piecewise — never write past it.
- In-raid crafting, PvP, walkable Fold, skins: all explicitly deferred (see plan's deferred list — it's described there as a contract; additions must evict something).

## Suggested skills for the next session

- `napkin` — activates every session; start curating `.claude/napkin.md` with Unity/FishNet gotchas as they're discovered (there will be many in Phase 0).
- `find-skills` — when Unity-specific workflows come up (build automation, editor tooling), check for an installable skill before hand-rolling.
- `grill-with-docs` — re-invoke for any new design branch (e.g., PvP milestone design, map 2); CONTEXT.md and the ADRs are its inputs.
- `run` / `verify` — once there's a runnable project, for exercising changes end-to-end.

## Suggested opening move

Confirm with the user where the Unity project should live (the WSL/Windows question above), then execute "Week one, concretely" from `docs/vertical-slice-plan.md` top to bottom.
