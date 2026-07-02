# Light Raiders

## Agent skills

### Issue tracker

Issues and PRDs are tracked in this repo's GitHub Issues (via the `gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Default triage label vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`), plus phase, type, and area labels. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout (`CONTEXT.md` + `docs/adr/` at the repo root). See `docs/agents/domain.md`.

## Workflow

Development follows a pipeline of **phases**. Each phase is a fresh session, started by pasting the previous phase's **kickoff prompt**; the agent reads the corresponding **handoff** in `docs/handoffs/` and picks up from there.

### Phase pipeline (skill order)

1. **Domain** — `/grill-with-docs` → `CONTEXT.md` + `docs/adr/`. ✅ done.
2. **PRD** — `/to-prd` → Product Requirements Document, built on the domain foundation.
3. **Issues** — `/to-issues` → slices the PRD into granular issues (vertical slices) with clear dependencies, **published to GitHub** at that point.
4. **Development** — per issue, the 8-step flow below. During dev, **automatically select** the right skill for each case (e.g. `tdd`, `diagnose`, `review`, `request-refactor-plan`, `verify`, `run`) without the user needing to name it.

### Per-issue implementation flow (8 steps)

Each step runs in a **specialized subagent with fresh context** (fresh spawn per step):

1. **Explore** — look at the codebase and/or external content to learn what to do.
2. **Plan** — create the implementation plan.
3. **Review the plan** — validate correctness; point out adjustments.
4. **Fix the plan** — if needed.
5. **Implement** — real code; may spawn subagents in **parallel** when possible.
6. **Code review** — spawn **multiple** specialized subagents (e.g. bugs/correctness, security, quality/maintainability, performance, adherence to the ADRs and `CONTEXT.md`).
7. **Fix** — apply the review findings.
8. **Validate and close** — if everything is green (tests, lint, types, reviews satisfied): merge the PR and close the related issue.

### Handoff at the end of each phase

Run `/handoff` to generate:

- a self-sufficient **handoff document** in **`docs/handoffs/`** (committed — never `/tmp`), grounded in the **real code/artifacts**: scope, what to read first, order/dependencies, non-negotiable principles, landmines, exit criteria, environment gotchas;
- a copyable **kickoff prompt** (block at the end of the response, unindented and with no line breaks inside paragraphs) pointing to the doc, so the user can start the next session without re-deriving context.
