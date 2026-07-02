# Napkin Runbook

## Curation Rules
- Re-prioritize on every read.
- Keep recurring, high-value notes only.
- Max 10 items per category.
- Each item includes date + "Do instead".

## User Directives (Highest Priority)
1. **[2026-07-02] User wants autonomy; answers arrive late — NEVER re-ask in a loop (supersedes earlier re-ask rule)**
   Do instead: on AskUserQuestion timeout, continue independent work; when only the blocked decision remains, restate the question as plain text (options + recommendation) as the final message and end the turn — it waits in the transcript. Never substitute an answer; never poll.
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
4. **[2026-07-02] Multiword glossary headwords are proper nouns**
   Do instead: capitalize them mid-sentence (Skill Tree, Safe Pocket, Shield Cell, Choir Remnant); lowercase reads as drift.
5. **[2026-07-02] Never write past the Hymn mystery**
   Do instead: the Hymn *ignited* the Light; "what answered" is deliberately unresolved — don't imply an answer in any prose, even a README.

## Execution & Validation
1. **[2026-07-02] FishNet updates corrupt if imported over a stale copy**
   Do instead: delete `Assets/FishNet` entirely, then re-import the new version; never run pre-4.6.19 FishNet on Unity 6.
2. **[2026-07-02] MPPM + FishNet: networked-prefab edits break running virtual players (unfixed Unity limitation, FishNet #916)**
   Do instead: keep Domain Reload enabled and restart virtual players after editing any networked prefab.
3. **[2026-07-02] Unity version discipline**
   Do instead: latest 6000.3.x LTS patch only (6.0 LTS dies Oct 2026; 6000.4/5 are non-LTS tech streams); details in `docs/research/phase0-setup-runbook.md`.

## Environment & Shell
1. **[2026-07-02] Dev is Windows-native as of 2026-07-02 (WSL2 retired for this project — it kept crashing)**
   Do instead: work in the Windows clone (C:\Users\ferna\source\repos\lightraiders) with Claude Code native in PowerShell; treat any WSL-side copy at /home/ferna/projects/lightraiders as stale — never edit there.
2. **[2026-07-02] Session knowledge must live in the repo, not /tmp or per-path memory**
   Do instead: durable notes go in this napkin, docs/handoffs/, or docs/research/ (committed); WSL crashes wiped /tmp scratchpads and per-path memory doesn't follow environment moves.
3. **[2026-07-02] GitHub repo is fernandolisboa/lightraiders (private)**
   Do instead: use `gh` CLI for issues/labels; issue workflow and label vocabulary live in `docs/agents/`.
4. **[2026-07-02] LFS rules predate binaries by design**
   Do instead: `.gitattributes` at repo root already tracks art/audio/model formats; run `git lfs install` once per machine clone before adding any binary.
