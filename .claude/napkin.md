# Napkin Runbook

## Curation Rules
- Re-prioritize on every read.
- Keep recurring, high-value notes only.
- Max 10 items per category.
- Each item includes date + "Do instead".

## User Directives (Highest Priority)
1. **[2026-07-02] User answers questions asynchronously — may take minutes+**
   Do instead: on AskUserQuestion timeout, re-ask the same question and wait; never substitute an answer or wind down the turn.
2. **[2026-07-02] English only, everywhere in this project**
   Do instead: write all repo content, issues, commits, and handoffs in English, even when prompted in Portuguese; translate rather than preserve Portuguese drafts.
3. **[2026-07-02] One question at a time, with a recommendation**
   Do instead: ask a single question per AskUserQuestion, mark the recommended option, state honest trade-offs in plain language with concrete numbers.
4. **[2026-07-02] User occasionally misclicks options**
   Do instead: for irreversible or surprising answers, confirm once before acting.

## Domain Behavior Guardrails
1. **[2026-07-02] CONTEXT.md glossary is law**
   Do instead: use canonical terms exactly (Raider not operator; raid not match; Lumen not credits; the Fold not base/hub); check CONTEXT.md before naming anything.
2. **[2026-07-02] ADRs 0001–0006 are closed decisions**
   Do instead: don't re-litigate top-down format, Unity, PvE-first slice, discrete Layers, FishNet, or premium EA; flag conflicts explicitly instead of silently overriding.
3. **[2026-07-02] The deferred list is a contract**
   Do instead: anything proposed for the slice must evict something; check `docs/vertical-slice-plan.md` deferred list before accepting scope.

## Environment & Shell
1. **[2026-07-02] WSL2 on a Windows host; Unity must run Windows-side**
   Do instead: treat cross-boundary file I/O (WSL ↔ Windows) as slow; resolve Unity project location with the user before creating it (see Phase 0 handoff).
2. **[2026-07-02] GitHub repo is fernandolisboa/lightraiders (private), remote via SSH**
   Do instead: use `gh` CLI for issues/labels; issue workflow and label vocabulary live in `docs/agents/`.
